using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Utilities;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class OriginNormalizerTests
{
    [Theory]
    [InlineData("https://moodle.uni.edu", "https://moodle.uni.edu")]
    [InlineData("moodle.uni.edu", "https://moodle.uni.edu")]                        // bare host → https
    [InlineData("https://moodle.uni.edu/course/view.php?id=4", "https://moodle.uni.edu")] // path stripped
    [InlineData("HTTPS://Moodle.UNI.edu", "https://moodle.uni.edu")]                // lowercased
    [InlineData("http://lms.uni.edu:8080", "http://lms.uni.edu:8080")]             // explicit port kept
    [InlineData("https://moodle.uni.edu:443", "https://moodle.uni.edu")]           // default port dropped
    public void Normalize_ValidInputs_ReturnsOrigin(string input, string expected)
    {
        Assert.Equal(expected, OriginNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://files.uni.edu")]   // non-http scheme rejected
    public void Normalize_InvalidInputs_ReturnsNull(string input)
    {
        Assert.Null(OriginNormalizer.Normalize(input));
    }

    [Fact]
    public void NormalizeMany_SplitsAndDedupes()
    {
        var raw = "https://a.edu\nhttps://a.edu, b.edu\n\nbad url";
        var result = OriginNormalizer.NormalizeMany(raw);
        Assert.Equal(new[] { "https://a.edu", "https://b.edu" }, result);
    }
}

public class UniversityTrustedOriginsServiceTests
{
    private readonly Mock<IUniversityRepository> _repo = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly UniversityService _service;

    public UniversityTrustedOriginsServiceTests()
    {
        _service = new UniversityService(
            _repo.Object, new Mock<IUserUniversityRepository>().Object,
            _audit.Object, new Mock<ILogger<UniversityService>>().Object);
    }

    private static University Uni(int id) => new() { Id = id, Name = "U", Code = "U" };
    private static User User(string type, int? uni) => new()
    {
        UserId = 1, UserType = type, UniversityId = uni,
        Username = "u", Email = "u@t.com", FirstName = "F", LastName = "L",
    };

    [Fact]
    public async Task UpdateAllowedOriginsAsync_NormalizesAndDedupes_AndSaves()
    {
        _repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Uni(5));
        _repo.Setup(r => r.UpdateAsync(It.IsAny<University>())).Returns(Task.CompletedTask);

        var result = await _service.UpdateAllowedOriginsAsync(
            5, new[] { "moodle.uni.edu", "https://moodle.uni.edu/x", "bad url" }, User(UserTypes.SuperAdmin, null));

        Assert.Equal(new[] { "https://moodle.uni.edu" }, result);   // deduped + normalized, invalid dropped
        _repo.Verify(r => r.UpdateAsync(It.Is<University>(u => u.AllowedOrigins == "https://moodle.uni.edu")), Times.Once);
    }

    [Fact]
    public async Task UpdateAllowedOriginsAsync_EmptyList_StoresNull()
    {
        _repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(Uni(5));
        _repo.Setup(r => r.UpdateAsync(It.IsAny<University>())).Returns(Task.CompletedTask);

        var result = await _service.UpdateAllowedOriginsAsync(5, new List<string>(), User(UserTypes.SuperAdmin, null));

        Assert.Empty(result);
        _repo.Verify(r => r.UpdateAsync(It.Is<University>(u => u.AllowedOrigins == null)), Times.Once);
    }

    [Fact]
    public async Task UpdateAllowedOriginsAsync_ManagerOtherUniversity_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdateAllowedOriginsAsync(5, new[] { "a.edu" }, User(UserTypes.Manager, 99)));
    }

    [Fact]
    public async Task GetAllowedOriginsAsync_ProfessorRole_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetAllowedOriginsAsync(5, User(UserTypes.Professor, 5)));
    }
}
