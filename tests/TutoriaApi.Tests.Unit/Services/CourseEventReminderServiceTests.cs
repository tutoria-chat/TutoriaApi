using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class CourseEventReminderServiceTests
{
    private readonly Mock<ICourseEventRepository> _eventRepoMock = new();
    private readonly Mock<IStudentCourseRepository> _studentCourseRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly CourseEventReminderService _service;

    public CourseEventReminderServiceTests()
    {
        _service = new CourseEventReminderService(
            _eventRepoMock.Object,
            _studentCourseRepoMock.Object,
            _userRepoMock.Object,
            _emailMock.Object,
            Mock.Of<ILogger<CourseEventReminderService>>());
    }

    private static CourseEvent EventIn(TimeSpan untilStart, bool r7 = false, bool r3 = false, bool r2 = false, bool r24 = false)
        => new()
        {
            Id = 1,
            CourseId = 5,
            Course = new Course { Id = 5, Name = "Course X", Code = "CX", UniversityId = 10 },
            Title = "Prova 1",
            EventType = "test",
            StartsAtUtc = DateTime.UtcNow.Add(untilStart),
            Remind7Days = r7,
            Remind3Days = r3,
            Remind2Days = r2,
            Remind24Hours = r24,
        };

    private static User Student(int id = 100, string email = "aluno@uni.edu", string lang = "pt-br") => new()
    {
        UserId = id, Username = $"s{id}", Email = email,
        FirstName = "Aluno", LastName = "Teste", UserType = "student",
        LanguagePreference = lang,
    };

    private void SetupEnrollment(params User[] students)
    {
        _studentCourseRepoMock.Setup(r => r.GetStudentIdsByCourseIdAsync(5))
            .ReturnsAsync(students.Select(s => s.UserId).ToList());
        _userRepoMock.Setup(r => r.GetActiveStudentsByIdsAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(students.ToList());
    }

    [Fact]
    public async Task ProcessRemindersAsync_EventInsideSlotWindow_SendsAndLogs()
    {
        var courseEvent = EventIn(TimeSpan.FromHours(20), r24: true);
        _eventRepoMock.Setup(r => r.GetUpcomingWithRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CourseEvent> { courseEvent });
        _eventRepoMock.Setup(r => r.HasReminderBeenSentAsync(1, "24h")).ReturnsAsync(false);
        SetupEnrollment(Student());

        await _service.ProcessRemindersAsync();

        _emailMock.Verify(e => e.SendCourseEventReminderEmailAsync(
            "aluno@uni.edu", "Aluno", "Prova 1", "test", "Course X",
            It.IsAny<string>(), It.IsAny<string>(), "pt-br"), Times.Once);
        _eventRepoMock.Verify(r => r.AddReminderLogAsync(It.Is<CourseEventReminderLog>(
            l => l.ReminderKey == "24h" && l.RecipientsCount == 1)), Times.Once);
    }

    [Fact]
    public async Task ProcessRemindersAsync_AlreadySent_DoesNotResend()
    {
        var courseEvent = EventIn(TimeSpan.FromHours(20), r24: true);
        _eventRepoMock.Setup(r => r.GetUpcomingWithRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CourseEvent> { courseEvent });
        _eventRepoMock.Setup(r => r.HasReminderBeenSentAsync(1, "24h")).ReturnsAsync(true);

        await _service.ProcessRemindersAsync();

        _emailMock.Verify(e => e.SendCourseEventReminderEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRemindersAsync_MultipleSlotsEligible_OnlyClosestSendsOthersSkipped()
    {
        // Event created 20h before start with 7d AND 24h checked:
        // exactly ONE email (24h), the 7d slot logged as skipped (0 recipients).
        var courseEvent = EventIn(TimeSpan.FromHours(20), r7: true, r24: true);
        _eventRepoMock.Setup(r => r.GetUpcomingWithRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CourseEvent> { courseEvent });
        _eventRepoMock.Setup(r => r.HasReminderBeenSentAsync(1, It.IsAny<string>())).ReturnsAsync(false);
        SetupEnrollment(Student());

        await _service.ProcessRemindersAsync();

        _emailMock.Verify(e => e.SendCourseEventReminderEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _eventRepoMock.Verify(r => r.AddReminderLogAsync(It.Is<CourseEventReminderLog>(
            l => l.ReminderKey == "7d" && l.RecipientsCount == 0)), Times.Once);
        _eventRepoMock.Verify(r => r.AddReminderLogAsync(It.Is<CourseEventReminderLog>(
            l => l.ReminderKey == "24h" && l.RecipientsCount == 1)), Times.Once);
    }

    [Fact]
    public async Task ProcessRemindersAsync_WindowNotEnteredYet_DoesNothing()
    {
        // 5 days out with only the 24h reminder enabled — too early
        var courseEvent = EventIn(TimeSpan.FromDays(5), r24: true);
        _eventRepoMock.Setup(r => r.GetUpcomingWithRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CourseEvent> { courseEvent });

        await _service.ProcessRemindersAsync();

        _emailMock.Verify(e => e.SendCourseEventReminderEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _eventRepoMock.Verify(r => r.AddReminderLogAsync(It.IsAny<CourseEventReminderLog>()), Times.Never);
    }

    [Fact]
    public async Task ProcessRemindersAsync_StudentLanguagePreference_DrivesEmailLanguage()
    {
        var courseEvent = EventIn(TimeSpan.FromHours(20), r24: true);
        _eventRepoMock.Setup(r => r.GetUpcomingWithRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CourseEvent> { courseEvent });
        _eventRepoMock.Setup(r => r.HasReminderBeenSentAsync(1, "24h")).ReturnsAsync(false);
        SetupEnrollment(Student(email: "gringo@uni.edu", lang: "en"));

        await _service.ProcessRemindersAsync();

        _emailMock.Verify(e => e.SendCourseEventReminderEmailAsync(
            "gringo@uni.edu", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), "tomorrow", "en"), Times.Once);
    }
}
