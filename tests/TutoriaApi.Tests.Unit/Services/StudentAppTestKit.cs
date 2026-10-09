using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using TutoriaApi.Infrastructure.Services;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>A clock frozen at a given instant (tests never depend on the real time).</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; }
    public FixedTimeProvider(DateTime utc) => Now = new DateTimeOffset(utc, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Student-app services on the EF in-memory provider with the real repositories;
/// only the edges (S3, SQS, email, clock) are mocked. Seeds the consumer tenant.
/// </summary>
public sealed class StudentAppTestKit : IDisposable
{
    public static readonly DateTime Now = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc); // a Wednesday

    public TutoriaDbContext Db { get; }
    public FixedTimeProvider Clock { get; } = new(Now);
    public Mock<IBlobStorageService> Blobs { get; } = new();
    public Mock<ISqsMessagingService> Sqs { get; } = new();
    public Mock<IEmailService> Email { get; } = new();
    public StudentAppOptions Options { get; } = new() { BillingDevMode = true };
    public StudentAppRepository Repo { get; }
    public StudentAppService Service { get; }
    public University Consumer { get; }
    public Course Enem { get; }

    public StudentAppTestKit()
    {
        Db = new TutoriaDbContext(new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        Consumer = new University { Name = "TutorIA Estudantes", Code = StudentApp.ConsumerUniversityCode, IsConsumer = true };
        Db.Universities.Add(Consumer);
        Db.SaveChanges();
        Enem = new Course { Name = "ENEM", Code = StudentApp.EnemCourseCode, UniversityId = Consumer.Id, EnableEnem = true };
        Db.Courses.Add(Enem);
        Db.SaveChanges();
        foreach (var (area, code) in StudentApp.AreaModuleCodes)
            Db.Modules.Add(new Module { Name = StudentApp.AreaLabels[area], Code = code, SystemPrompt = "p", CourseId = Enem.Id });
        Db.SaveChanges();

        Blobs.Setup(b => b.GenerateBlobPath(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns((int u, int c, int m, string f) => $"universities/{u}/courses/{c}/modules/{m}/{f}");
        Blobs.Setup(b => b.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string path, string _) => $"https://s3/{path}");
        Sqs.Setup(s => s.SendExtractionJobAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);

        Repo = new StudentAppRepository(Db);
        Service = new StudentAppService(
            Repo, new UserRepository(Db), new UserUniversityRepository(Db), new StudentCourseRepository(Db),
            new ConsentRepository(Db), new CourseRepository(Db), new ModuleRepository(Db), new FileRepository(Db),
            Blobs.Object, Sqs.Object, Email.Object, Microsoft.Extensions.Options.Options.Create(Options), Clock,
            Mock.Of<ILogger<StudentAppService>>());
    }

    public int AreaModuleId(string area)
        => Db.Modules.Single(m => m.Code == StudentApp.AreaModuleCodes[area]).Id;

    public async Task<User> RegisterAsync(string email = "ana@example.com", int age = 25)
        => await Service.RegisterAsync(new Core.DTOs.StudentRegisterInput(
            "Ana Souza", email, "senha-forte-1", new DateOnly(2026 - age, 1, 15), true));

    public async Task SetPlanAsync(int userId, string plan, int daysLeft = 20)
    {
        await Repo.UpsertSubscriptionAsync(new StudentSubscription
        {
            UserId = userId, Plan = plan, Source = "app_store",
            PeriodStart = Now.AddDays(-10), PeriodEnd = Now.AddDays(daysLeft), WillRenew = true,
        });
        await Service.EnsureDefaultAgentsAsync(userId);
    }

    public void Dispose() => Db.Dispose();
}
