using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using Xunit;

namespace TutoriaApi.Tests.Unit.Repositories;

public class MajorRepositoryTests : IDisposable
{
    private readonly TutoriaDbContext _context;
    private readonly MajorRepository _repository;

    public MajorRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TutoriaDbContext(options);
        _repository = new MajorRepository(_context);

        _context.Majors.AddRange(
            new Major { Id = 1, UniversityId = 1, Name = "Direito" },
            new Major { Id = 2, UniversityId = 1, Name = "Medicina" },
            new Major { Id = 3, UniversityId = 2, Name = "Direito" } // other university
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetByUniversityIdAsync_ReturnsOnlyThatUniversityOrderedByName()
    {
        var result = (await _repository.GetByUniversityIdAsync(1)).ToList();
        Assert.Equal(2, result.Count);
        Assert.Equal("Direito", result[0].Name);
        Assert.Equal("Medicina", result[1].Name);
    }

    [Fact]
    public async Task ExistsByNameInUniversityAsync_IsCaseInsensitiveAndScoped()
    {
        Assert.True(await _repository.ExistsByNameInUniversityAsync("direito", 1));
        Assert.False(await _repository.ExistsByNameInUniversityAsync("Direito", 99));
    }

    [Fact]
    public async Task GetValidMajorIdsAsync_ReturnsOnlyIdsInUniversity()
    {
        var valid = await _repository.GetValidMajorIdsAsync(new[] { 1, 2, 3, 999 }, 1);
        Assert.Equal(new[] { 1, 2 }, valid.OrderBy(x => x));
    }

    [Fact]
    public async Task SetCourseMajorsAsync_ReplacesExistingSet()
    {
        await _repository.SetCourseMajorsAsync(50, new[] { 1, 2 });
        Assert.Equal(2, await _context.CourseMajors.CountAsync(cm => cm.CourseId == 50));

        // Replace with just {2}: 1 removed, 2 kept, no dup.
        await _repository.SetCourseMajorsAsync(50, new[] { 2 });
        var rows = await _context.CourseMajors.Where(cm => cm.CourseId == 50).Select(cm => cm.MajorId).ToListAsync();
        Assert.Equal(new[] { 2 }, rows);
    }

    [Fact]
    public async Task GetMajorsForCourseAsync_ReturnsTaggedMajors()
    {
        await _repository.SetCourseMajorsAsync(60, new[] { 1 });
        var majors = (await _repository.GetMajorsForCourseAsync(60)).ToList();
        Assert.Single(majors);
        Assert.Equal("Direito", majors[0].Name);
    }

    [Fact]
    public async Task ExistingNamesLowerAsync_ReturnsLowercasedNames()
    {
        var names = await _repository.ExistingNamesLowerAsync(1);
        Assert.Contains("direito", names);
        Assert.Contains("medicina", names);
        Assert.DoesNotContain("Direito", names); // lowercased
    }

    public void Dispose() => _context.Dispose();
}
