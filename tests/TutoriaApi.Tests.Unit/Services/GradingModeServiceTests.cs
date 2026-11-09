using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Utilities;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Grading modes: score (AI-suggested grades) must never reach an institution under
/// MEC/CNE, must never be turned on by anyone but a super_admin, and must never run
/// through the legacy shared API key.
/// </summary>
public class GradingModeServiceTests
{
    private readonly Mock<IUniversityRepository> _universities = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly GradingModeService _service;

    private static readonly User SuperAdmin = new() { UserId = 1, Username = "root", UserType = "super_admin", Email = "r@x", FirstName = "R", LastName = "X" };
    private static readonly User Manager = new() { UserId = 2, Username = "mgr", UserType = "manager", Email = "m@x", FirstName = "M", LastName = "X", UniversityId = 10 };

    public GradingModeServiceTests()
    {
        _service = new GradingModeService(_universities.Object, _audit.Object, new Mock<ILogger<GradingModeService>>().Object);
    }

    private static University Uni(string type = "university", string mode = "feedback") => new()
    {
        Id = 10, Name = "Empresa", Code = "EMP", InstitutionType = type, GradingMode = mode,
    };

    // ── GradingModes.Effective ────────────────────────────────────────────────

    [Theory]
    [InlineData("corporate", "score", true, "score")]
    [InlineData("corporate", "score", false, "feedback")]   // legacy shared key
    [InlineData("corporate", "feedback", true, "feedback")]
    [InlineData("university", "score", true, "feedback")]   // can't happen in DB, still refused
    [InlineData("university", "feedback", true, "feedback")]
    public void Effective_OnlyCorporateScoreWithTrustedCaller_Scores(string type, string mode, bool trusted, string expected)
    {
        Assert.Equal(expected, GradingModes.Effective(Uni(type, mode), trusted));
    }

    [Fact]
    public void Effective_NullUniversity_IsFeedback()
    {
        Assert.Equal(GradingModes.Feedback, GradingModes.Effective(null, true));
    }

    [Theory]
    [InlineData("university", "score", false)]
    [InlineData("university", "feedback", true)]
    [InlineData("corporate", "score", true)]
    [InlineData("corporate", "feedback", true)]
    public void IsAllowedCombination_MatchesDbConstraint(string type, string mode, bool allowed)
    {
        Assert.Equal(allowed, GradingModes.IsAllowedCombination(type, mode));
    }

    // ── SetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_NotSuperAdmin_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SetAsync(10, "corporate", "score", "Contrato 1", Manager));
        _universities.Verify(r => r.SetGradingModeAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_ScoreOnUniversity_IsRefused()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetAsync(10, "university", "score", "Contrato 1", SuperAdmin));
        Assert.Contains("corporate", ex.Message);
        _universities.Verify(r => r.SetGradingModeAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_ScoreWithoutContract_IsRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetAsync(10, "corporate", "score", "  ", SuperAdmin));
    }

    [Theory]
    [InlineData("school", "feedback")]
    [InlineData("corporate", "auto")]
    public async Task SetAsync_UnknownValues_AreRefused(string type, string mode)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetAsync(10, type, mode, "c", SuperAdmin));
    }

    [Fact]
    public async Task SetAsync_UniversityMissing_ThrowsNotFound()
    {
        _universities.Setup(r => r.GetByIdAsync(10)).ReturnsAsync((University?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.SetAsync(10, "corporate", "score", "Contrato 1", SuperAdmin));
    }

    [Fact]
    public async Task SetAsync_CorporateScore_SavesAndAuditLogs()
    {
        _universities.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(Uni());

        var state = await _service.SetAsync(10, " Corporate ", "SCORE", " Contrato 2026-11 ", SuperAdmin);

        Assert.Equal("corporate", state.InstitutionType);
        Assert.Equal("score", state.GradingMode);
        Assert.Equal("score", state.EffectiveMode);
        Assert.Equal("Contrato 2026-11", state.ContractRef);
        _universities.Verify(r => r.SetGradingModeAsync(10, "corporate", "score", "Contrato 2026-11", 1, It.IsAny<DateTime>()), Times.Once);
        _audit.Verify(a => a.LogAsync(1, "root", 10, "UpdateGradingMode", "University", 10, "Empresa",
            It.Is<Dictionary<string, (object? OldValue, object? NewValue)>>(d =>
                (string?)d["GradingMode"].OldValue == "feedback" && (string?)d["GradingMode"].NewValue == "score")), Times.Once);
    }

    [Fact]
    public async Task SetAsync_BackToFeedback_NeedsNoContract()
    {
        _universities.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(Uni("corporate", "score"));
        var state = await _service.SetAsync(10, "university", "feedback", null, SuperAdmin);
        Assert.Equal("feedback", state.EffectiveMode);
    }

    // ── GetForExternalAsync / GetAsync ────────────────────────────────────────

    [Fact]
    public async Task GetForExternalAsync_SharedKey_NeverScores()
    {
        _universities.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(Uni("corporate", "score"));
        var state = await _service.GetForExternalAsync(10, institutionKey: false);
        Assert.Equal("score", state.GradingMode);
        Assert.Equal("feedback", state.EffectiveMode);
    }

    [Fact]
    public async Task GetForExternalAsync_InstitutionKey_Scores()
    {
        _universities.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(Uni("corporate", "score"));
        var state = await _service.GetForExternalAsync(10, institutionKey: true);
        Assert.Equal("score", state.EffectiveMode);
    }

    [Fact]
    public async Task GetForExternalAsync_Missing_ThrowsNotFound()
    {
        _universities.Setup(r => r.GetByIdAsync(10)).ReturnsAsync((University?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetForExternalAsync(10, true));
    }

    [Fact]
    public async Task GetAsync_NotSuperAdmin_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetAsync(10, Manager));
    }
}

/// <summary>The grading mode is snapshotted on each job when it is created.</summary>
public class GradingJobModeSnapshotTests
{
    private readonly Mock<IGradingJobRepository> _jobRepo = new();
    private readonly Mock<ICourseRepository> _courseRepo = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<ISqsMessagingService> _sqs = new();
    private readonly GradingJobService _service;

    public GradingJobModeSnapshotTests()
    {
        _service = new GradingJobService(_jobRepo.Object, _courseRepo.Object, new Mock<IUniversityRepository>().Object,
            _blob.Object, _sqs.Object, new Mock<ILogger<GradingJobService>>().Object);
        _jobRepo.Setup(r => r.AddAsync(It.IsAny<GradingJob>())).ReturnsAsync((GradingJob j) => { j.Id = 42; return j; });
        _jobRepo.Setup(r => r.UpdateAsync(It.IsAny<GradingJob>())).Returns(Task.CompletedTask);
        _blob.Setup(b => b.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("key");
        _sqs.Setup(s => s.SendGradingJobAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
    }

    private void CourseOf(string type, string mode) =>
        _courseRepo.Setup(r => r.GetByExternalCourseIdAsync(999, 1)).ReturnsAsync(new Course
        {
            Id = 5, Name = "C", Code = "C", UniversityId = 1, ExternalCourseId = 999,
            University = new University { Id = 1, Name = "U", Code = "U", HasAssignments = true, InstitutionType = type, GradingMode = mode },
        });

    private static MemoryStream Body() => new(Encoding.UTF8.GetBytes("[{\"name\":\"x\"}]"));

    [Theory]
    [InlineData("corporate", "score", true, "score")]
    [InlineData("corporate", "score", false, "feedback")]
    [InlineData("university", "feedback", true, "feedback")]
    public async Task CreateExternalJobAsync_SnapshotsEffectiveMode(string type, string mode, bool institutionKey, string expected)
    {
        CourseOf(type, mode);
        var job = await _service.CreateExternalJobAsync(1, 999, Body(), null, null, null, institutionKey);
        Assert.Equal(expected, job.GradingMode);
    }
}
