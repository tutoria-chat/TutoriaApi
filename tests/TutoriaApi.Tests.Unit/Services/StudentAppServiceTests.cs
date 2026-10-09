using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Exceptions;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class StudentAppServiceTests : IDisposable
{
    private readonly StudentAppTestKit _kit = new();

    public void Dispose() => _kit.Dispose();

    private static async Task<StudentAppException> Fails(Func<Task> act)
        => await Assert.ThrowsAsync<StudentAppException>(act);

    // ─── Accounts ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_ValidInput_CreatesConsumerStudentEnrolledInEnem()
    {
        var user = await _kit.RegisterAsync();

        Assert.Equal(UserTypes.Student, user.UserType);
        Assert.Equal(_kit.Consumer.Id, user.UniversityId);
        Assert.Equal("ana@example.com", user.Username);
        Assert.True(_kit.Db.StudentCourses.Any(sc => sc.StudentId == user.UserId && sc.CourseId == _kit.Enem.Id));
        Assert.True(_kit.Db.UserUniversities.Any(uu => uu.UserId == user.UserId && uu.UniversityId == _kit.Consumer.Id));
        Assert.NotNull(_kit.Db.StudentProfiles.Single(p => p.UserId == user.UserId).TermsAcceptedAt);
        Assert.Equal(2, _kit.Db.Consents.Count(c => c.UserId == user.UserId));
    }

    [Fact]
    public async Task RegisterAsync_Under13_ThrowsTooYoung()
    {
        var ex = await Fails(() => _kit.RegisterAsync(age: 11));
        Assert.Equal("too_young", ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_TermsNotAccepted_Throws()
    {
        var ex = await Fails(() => _kit.Service.RegisterAsync(new StudentRegisterInput("Ana", "a@x.com", "senha-forte-1", new DateOnly(2000, 1, 1), false)));
        Assert.Equal("terms_required", ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Throws409()
    {
        await _kit.RegisterAsync();
        var ex = await Fails(() => _kit.RegisterAsync(email: "ANA@example.com"));
        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_Throws401()
    {
        await _kit.RegisterAsync();
        var ex = await Fails(() => _kit.Service.AuthenticateAsync("ana@example.com", "errada123"));
        Assert.Equal(401, ex.Status);
    }

    [Fact]
    public async Task AuthenticateAsync_InstitutionalStudent_IsRejected()
    {
        _kit.Db.Users.Add(new User
        {
            Username = "inst", Email = "inst@uni.br", FirstName = "I", LastName = "S", UserType = "student",
            UniversityId = 999, HashedPassword = BCrypt.Net.BCrypt.HashPassword("senha-forte-1"),
        });
        _kit.Db.SaveChanges();
        var ex = await Fails(() => _kit.Service.AuthenticateAsync("inst@uni.br", "senha-forte-1"));
        Assert.Equal("not_student_app_account", ex.Code);
    }

    // ─── Profile / plan ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMeAsync_NoPlan_NotReady_WithAreaModules()
    {
        var user = await _kit.RegisterAsync();
        var me = await _kit.Service.GetMeAsync(user.UserId);
        Assert.Null(me.Plan);
        Assert.False(me.Ready);
        Assert.Equal(4, me.AreaModules.Count);
        Assert.Equal(25, me.Age);
    }

    [Fact]
    public async Task SetPlan_Pro_ReadyWithFourDefaultAgents()
    {
        var user = await _kit.RegisterAsync();
        await _kit.SetPlanAsync(user.UserId, "pro");
        var me = await _kit.Service.GetMeAsync(user.UserId);
        Assert.True(me.Ready);
        Assert.Equal(StudentApp.AreaIds, me.Areas);
        var agents = await _kit.Service.GetAgentsAsync(user.UserId);
        Assert.Equal(4, agents.Count);
        Assert.All(agents, a => Assert.False(a.Locked));
    }

    [Fact]
    public async Task SetAreasAsync_Simples_FirstChoiceThenOneSwitchPerCycle()
    {
        var user = await _kit.RegisterAsync();
        await _kit.SetPlanAsync(user.UserId, "simples");
        Assert.True((await _kit.Service.GetMeAsync(user.UserId)).NeedsAreaChoice);

        Assert.Equal("invalid_areas", (await Fails(() => _kit.Service.SetAreasAsync(user.UserId, new[] { "humanas" }))).Code);
        var me = await _kit.Service.SetAreasAsync(user.UserId, new[] { "matematica", "humanas" });
        Assert.True(me.Ready);
        Assert.Equal(new[] { "humanas", "matematica" }, me.Areas);

        me = await _kit.Service.SetAreasAsync(user.UserId, new[] { "matematica", "natureza" });
        Assert.False(me.CanChangeAreas);
        var ex = await Fails(() => _kit.Service.SetAreasAsync(user.UserId, new[] { "linguagens", "natureza" }));
        Assert.Equal("areas_change_used", ex.Code);

        var agents = await _kit.Service.GetAgentsAsync(user.UserId);
        Assert.True(agents.Single(a => a.Area == "humanas").Locked); // switched out
        Assert.False(agents.Single(a => a.Area == "natureza").Locked);
    }

    [Fact]
    public async Task UpdateProfileAsync_UniversityFieldsAndClearing()
    {
        var user = await _kit.RegisterAsync();
        var me = await _kit.Service.UpdateProfileAsync(user.UserId, new StudentProfileUpdateInput(
            null, null, null, "university", "UFC", "Engenharia Civil", 3, null, false,
            HasUniversity: true, HasMajor: true, HasSemester: true));
        Assert.Equal("university", me.Track);
        Assert.Equal("Engenharia Civil", me.Major);
        Assert.False(me.NotifyUpdates);
        me = await _kit.Service.UpdateProfileAsync(user.UserId, new StudentProfileUpdateInput(
            null, null, null, null, null, null, null, null, null, HasUniversity: true));
        Assert.Null(me.University);
        Assert.Equal("Engenharia Civil", me.Major); // untouched
    }

    // ─── Guardian / age signal ───────────────────────────────────────────────

    [Fact]
    public async Task GuardianFlow_MinorRequestsThenGuardianApproves()
    {
        var user = await _kit.RegisterAsync(email: "teen@example.com", age: 16);
        await _kit.SetPlanAsync(user.UserId, "pro");
        Assert.False((await _kit.Service.GetMeAsync(user.UserId)).Ready);

        string? link = null;
        _kit.Email.Setup(e => e.SendGuardianConsentEmailAsync("mae@example.com", "Maria", "Ana Souza", It.IsAny<string>()))
            .Callback((string _, string _, string _, string l) => link = l)
            .Returns(Task.CompletedTask);
        var me = await _kit.Service.RequestGuardianConsentAsync(user.UserId, "Maria", "mae@example.com", "https://api.test");
        Assert.Equal("pending", me.Guardian.Status);
        Assert.StartsWith("https://api.test/api/student-app/guardian/consent?token=", link);

        var token = link!.Split("token=")[1];
        Assert.NotNull(await _kit.Service.GetGuardianRequestAsync(token));
        Assert.Equal("Ana Souza", await _kit.Service.DecideGuardianConsentAsync(token, true, "1.2.3.4", "UA"));
        Assert.Null(await _kit.Service.DecideGuardianConsentAsync(token, false, null, null)); // single use

        me = await _kit.Service.GetMeAsync(user.UserId);
        Assert.Equal("approved", me.Guardian.Status);
        Assert.True(me.Ready);
        Assert.True(_kit.Db.Consents.Any(c => c.UserId == user.UserId && c.ConsentType == "guardian_consent" && c.IpAddress == "1.2.3.4"));
    }

    [Fact]
    public async Task GuardianLink_ExpiresAfterSevenDays()
    {
        var user = await _kit.RegisterAsync(email: "teen@example.com", age: 16);
        string? link = null;
        _kit.Email.Setup(e => e.SendGuardianConsentEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string _, string _, string _, string l) => link = l).Returns(Task.CompletedTask);
        await _kit.Service.RequestGuardianConsentAsync(user.UserId, "Maria", "mae@example.com", "https://api.test");
        _kit.Clock.Now = _kit.Clock.Now.AddDays(8);
        Assert.Null(await _kit.Service.GetGuardianRequestAsync(link!.Split("token=")[1]));
    }

    [Fact]
    public async Task RequestGuardian_Adult_ThrowsNotMinor()
    {
        var user = await _kit.RegisterAsync();
        var ex = await Fails(() => _kit.Service.RequestGuardianConsentAsync(user.UserId, "Maria", "m@x.com", "https://x"));
        Assert.Equal("not_minor", ex.Code);
    }

    [Fact]
    public async Task AgeSignal_Under18TurnsGuardianOn_Under13Blocks()
    {
        var user = await _kit.RegisterAsync();
        await _kit.SetPlanAsync(user.UserId, "pro");
        var me = await _kit.Service.RecordAgeSignalAsync(user.UserId, "ios", 13, 15, "guardianDeclared");
        Assert.True(me.Guardian.Required);
        Assert.False(me.Ready);
        Assert.True(me.AgeSignalChecked);
        me = await _kit.Service.RecordAgeSignalAsync(user.UserId, "android", 0, 12, null);
        Assert.True(me.Blocked);
        var ex = await Fails(() => _kit.Service.CreateAgentAsync(user.UserId, new StudentAgentInput("X", null, "humanas", null, null, null)));
        Assert.Equal("too_young", ex.Code);
    }

    // ─── Agents ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAgentAsync_AreaOutsideSimples_IsLocked()
    {
        var user = await _kit.RegisterAsync();
        await _kit.SetPlanAsync(user.UserId, "simples");
        await _kit.Service.SetAreasAsync(user.UserId, new[] { "matematica", "humanas" });
        var ex = await Fails(() => _kit.Service.CreateAgentAsync(user.UserId, new StudentAgentInput("Bio", null, "natureza", null, null, null)));
        Assert.Equal("area_locked", ex.Code);

        var created = await _kit.Service.CreateAgentAsync(user.UserId, new StudentAgentInput("Prof. Bia", "rocket", "humanas", null, "socratico", " Use futebol. "));
        Assert.Equal(_kit.AreaModuleId("humanas"), created.ModuleId);
        Assert.Equal("Use futebol.", created.Instructions);
    }

    [Fact]
    public async Task CreateAgentAsync_NoPlan_ThrowsPlanRequired()
    {
        var user = await _kit.RegisterAsync();
        var ex = await Fails(() => _kit.Service.CreateAgentAsync(user.UserId, new StudentAgentInput("X", null, "humanas", null, null, null)));
        Assert.Equal(402, ex.Status);
    }

    [Fact]
    public async Task UpdateAndDeleteAgent_OtherStudentsAgent_NotFound()
    {
        var a = await _kit.RegisterAsync();
        var b = await _kit.RegisterAsync(email: "b@example.com");
        await _kit.SetPlanAsync(a.UserId, "pro");
        var agentId = (await _kit.Service.GetAgentsAsync(a.UserId)).First().Id;
        Assert.Equal(404, (await Fails(() => _kit.Service.DeleteAgentAsync(b.UserId, agentId))).Status);
        var updated = await _kit.Service.UpdateAgentAsync(a.UserId, agentId, new StudentAgentInput(null, null, null, null, "divertido", null));
        Assert.Equal("divertido", updated.Tone);
    }

    // ─── Disciplines / uploads ───────────────────────────────────────────────

    [Fact]
    public async Task CreateDisciplineAsync_RequiresUniversityPlan()
    {
        var user = await _kit.RegisterAsync();
        await _kit.SetPlanAsync(user.UserId, "pro");
        var ex = await Fails(() => _kit.Service.CreateDisciplineAsync(user.UserId, "Cálculo I", null));
        Assert.Equal("plan_feature", ex.Code);
    }

    [Fact]
    public async Task CreateDisciplineAsync_CreatesPersonalCourseModuleAndMonitorAgent()
    {
        var user = await _kit.RegisterAsync();
        await _kit.Service.UpdateProfileAsync(user.UserId, new StudentProfileUpdateInput(
            null, null, null, "university", "UFC", "Engenharia Civil", 3, null, null, HasUniversity: true, HasMajor: true, HasSemester: true));
        await _kit.SetPlanAsync(user.UserId, "universitario");
        Assert.True((await _kit.Service.GetMeAsync(user.UserId)).NeedsDisciplines);

        var d = await _kit.Service.CreateDisciplineAsync(user.UserId, "Cálculo I", "Profa. Ana");

        var profile = _kit.Db.StudentProfiles.Single(p => p.UserId == user.UserId);
        var module = _kit.Db.Modules.Single(m => m.Id == d.Id);
        Assert.Equal(profile.PersonalCourseId, module.CourseId);
        Assert.Contains("Engenharia Civil", module.SystemPrompt);
        Assert.Contains("Profa. Ana", module.SystemPrompt);
        Assert.True(_kit.Db.StudentCourses.Any(sc => sc.StudentId == user.UserId && sc.CourseId == module.CourseId));
        Assert.NotNull(d.AgentId);
        var agent = (await _kit.Service.GetAgentsAsync(user.UserId)).Single();
        Assert.Equal("disciplina", agent.Area);
        Assert.Equal("Monitor de Cálculo I", agent.Name);
        Assert.Equal("Cálculo I", agent.DisciplineName);
        Assert.True((await _kit.Service.GetMeAsync(user.UserId)).Ready);
    }

    private async Task<(User User, int DisciplineId)> UniversityStudentAsync()
    {
        var user = await _kit.RegisterAsync();
        await _kit.SetPlanAsync(user.UserId, "universitario");
        var d = await _kit.Service.CreateDisciplineAsync(user.UserId, "Cálculo I", null);
        return (user, d.Id);
    }

    [Fact]
    public async Task UploadDisciplineFileAsync_ValidPdf_StoresAndQueuesExtraction()
    {
        var (user, did) = await UniversityStudentAsync();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var file = await _kit.Service.UploadDisciplineFileAsync(user.UserId, did, stream, "Aula 3.pdf", "application/pdf", 3, true);

        Assert.Equal("pdf", file.Kind);
        Assert.Equal("processing", file.Status);
        _kit.Sqs.Verify(s => s.SendExtractionJobAsync(file.Id, did), Times.Once);
        Assert.Equal(1, _kit.Db.StudentDailyUsage.Single(u => u.UserId == user.UserId).Uploads);
        var listed = await _kit.Service.GetDisciplineFilesAsync(user.UserId, did);
        Assert.Single(listed);
        Assert.Equal(1, (await _kit.Service.GetDisciplinesAsync(user.UserId)).Single().DocumentsProcessing);
    }

    [Theory]
    [InlineData("a.exe", 3L, false, "rights_required")]
    [InlineData("a.exe", 3L, true, "unsupported_file")]
    [InlineData("a.pdf", 0L, true, "empty_file")]
    [InlineData("a.pdf", 21L * 1024 * 1024, true, "file_too_large")]
    public async Task UploadDisciplineFileAsync_InvalidInput_Throws(string name, long size, bool rights, string code)
    {
        var (user, did) = await UniversityStudentAsync();
        var ex = await Fails(() => _kit.Service.UploadDisciplineFileAsync(user.UserId, did, new MemoryStream(), name, "x", size, rights));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task UploadDisciplineFileAsync_DailyLimit_Throws429()
    {
        var (user, did) = await UniversityStudentAsync();
        _kit.Db.StudentDailyUsage.Add(new StudentDailyUsage { UserId = user.UserId, Day = StudentApp.TodayBrazil(StudentAppTestKit.Now), Uploads = 20 });
        _kit.Db.SaveChanges();
        var ex = await Fails(() => _kit.Service.UploadDisciplineFileAsync(user.UserId, did, new MemoryStream(new byte[] { 1 }), "a.pdf", "application/pdf", 1, true));
        Assert.Equal(429, ex.Status);
    }

    [Fact]
    public async Task DisciplineOfAnotherStudent_IsNotFound()
    {
        var (_, did) = await UniversityStudentAsync();
        var other = await _kit.RegisterAsync(email: "other@example.com");
        await _kit.SetPlanAsync(other.UserId, "universitario");
        var ex = await Fails(() => _kit.Service.GetDisciplineFilesAsync(other.UserId, did));
        Assert.Equal(404, ex.Status);
        Assert.Equal(404, (await Fails(() => _kit.Service.DeleteDisciplineAsync(other.UserId, did))).Status);
    }

    [Fact]
    public async Task DeleteDisciplineFileAsync_DeletesBlobAndRow()
    {
        var (user, did) = await UniversityStudentAsync();
        var file = await _kit.Service.UploadDisciplineFileAsync(user.UserId, did, new MemoryStream(new byte[] { 1 }), "a.pdf", "application/pdf", 1, true);
        await _kit.Service.DeleteDisciplineFileAsync(user.UserId, file.Id);
        _kit.Blobs.Verify(b => b.DeleteFileAsync(It.Is<string>(p => p.EndsWith("a.pdf"))), Times.Once);
        Assert.Empty(await _kit.Service.GetDisciplineFilesAsync(user.UserId, did));
    }

    // ─── Devices ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterDeviceAsync_MovesTokenToCurrentUser()
    {
        var a = await _kit.RegisterAsync();
        var b = await _kit.RegisterAsync(email: "b@example.com");
        await _kit.Service.RegisterDeviceAsync(a.UserId, "ExponentPushToken[abc123]", "ios");
        await _kit.Service.RegisterDeviceAsync(b.UserId, "ExponentPushToken[abc123]", "ios");
        Assert.Equal(b.UserId, _kit.Db.DeviceTokens.Single().UserId);
        await _kit.Service.RemoveDeviceAsync(b.UserId, "ExponentPushToken[abc123]");
        Assert.Empty(_kit.Db.DeviceTokens);
    }
}
