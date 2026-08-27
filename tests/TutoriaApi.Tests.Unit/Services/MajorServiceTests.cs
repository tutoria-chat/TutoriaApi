using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class MajorServiceTests
{
    private readonly Mock<IMajorRepository> _repo = new();
    private readonly MajorService _service;

    public MajorServiceTests()
    {
        _service = new MajorService(_repo.Object);
    }

    [Fact]
    public async Task CreateAsync_NewName_AddsAndReturnsMajor()
    {
        _repo.Setup(r => r.ExistsByNameInUniversityAsync("Direito", 1)).ReturnsAsync(false);
        _repo.Setup(r => r.AddAsync(It.IsAny<Major>()))
            .ReturnsAsync((Major m) => { m.Id = 42; return m; });

        var result = await _service.CreateAsync(1, "  Direito  ");

        Assert.Equal(42, result.Id);
        Assert.Equal("Direito", result.Name); // trimmed
        _repo.Verify(r => r.AddAsync(It.Is<Major>(m => m.UniversityId == 1 && m.Name == "Direito")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsInvalidOperation()
    {
        _repo.Setup(r => r.ExistsByNameInUniversityAsync("Direito", 1)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(1, "Direito"));
        Assert.Contains("already exists", ex.Message);
        _repo.Verify(r => r.AddAsync(It.IsAny<Major>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_BlankName_ThrowsArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(1, "   "));
    }

    [Fact]
    public async Task DeleteAsync_WrongUniversity_ThrowsKeyNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(new Major { Id = 5, UniversityId = 999, Name = "X" });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteAsync(1, 5));
        _repo.Verify(r => r.DeleteAsync(It.IsAny<Major>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_OwnedMajor_Deletes()
    {
        var major = new Major { Id = 5, UniversityId = 1, Name = "X" };
        _repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(major);
        _repo.Setup(r => r.DeleteAsync(major)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(1, 5);

        _repo.Verify(r => r.DeleteAsync(major), Times.Once);
    }

    [Fact]
    public async Task SeedDefaultsAsync_AddsOnlyMissing()
    {
        // University already has the first standard major (lowercased set).
        var already = new HashSet<string> { StandardMajors.Names[0].ToLower() };
        _repo.Setup(r => r.ExistingNamesLowerAsync(1)).ReturnsAsync(already);
        _repo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Major>>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.GetByUniversityIdAsync(1)).ReturnsAsync(new List<Major>());

        await _service.SeedDefaultsAsync(1);

        _repo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<Major>>(
            list => list.Count() == StandardMajors.Names.Count - 1)), Times.Once);
    }

    [Fact]
    public async Task SetCourseMajorsAsync_OnlyKeepsValidIds()
    {
        // Ids 1 and 2 requested; only 1 belongs to the university.
        _repo.Setup(r => r.GetValidMajorIdsAsync(It.IsAny<IEnumerable<int>>(), 7))
            .ReturnsAsync(new List<int> { 1 });
        _repo.Setup(r => r.SetCourseMajorsAsync(100, It.IsAny<IEnumerable<int>>())).Returns(Task.CompletedTask);

        await _service.SetCourseMajorsAsync(100, 7, new[] { 1, 2 });

        _repo.Verify(r => r.SetCourseMajorsAsync(100, It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1 }))), Times.Once);
    }
}
