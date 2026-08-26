using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using Xunit;

namespace TutoriaApi.Tests.Unit.Repositories;

/// <summary>
/// Covers MatriculaTakenInUniversityAsync — the per-university uniqueness check
/// that keeps staff matriculas from colliding with students' (or each other's).
/// </summary>
public class UserRepositoryMatriculaTests : IDisposable
{
    private readonly TutoriaDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryMatriculaTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TutoriaDbContext(options);
        _repository = new UserRepository(_context);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Student whose matricula lives on Users.ExternalId
        _context.Users.Add(new User
        {
            UserId = 10,
            Username = "student1",
            Email = "student1@example.com",
            FirstName = "Ana",
            LastName = "Silva",
            UserType = "student",
            UniversityId = 1,
            ExternalId = "S123",
        });

        // Staff member whose matricula lives on the per-university junction row
        _context.Users.Add(new User
        {
            UserId = 30,
            Username = "prof1",
            Email = "prof1@example.com",
            FirstName = "Bruno",
            LastName = "Costa",
            UserType = "professor",
            UniversityId = 1,
        });
        _context.UserUniversities.Add(new UserUniversity
        {
            UserId = 30,
            UniversityId = 1,
            ExternalId = "UU999",
        });

        _context.SaveChanges();
    }

    [Fact]
    public async Task MatriculaTaken_MatchesStudentUserExternalId_ReturnsTrue()
    {
        var taken = await _repository.MatriculaTakenInUniversityAsync("S123", universityId: 1, excludeUserId: 99);
        Assert.True(taken);
    }

    [Fact]
    public async Task MatriculaTaken_MatchesJunctionExternalId_ReturnsTrue()
    {
        var taken = await _repository.MatriculaTakenInUniversityAsync("UU999", universityId: 1, excludeUserId: 99);
        Assert.True(taken);
    }

    [Fact]
    public async Task MatriculaTaken_ExcludesSelf_ReturnsFalse()
    {
        var taken = await _repository.MatriculaTakenInUniversityAsync("S123", universityId: 1, excludeUserId: 10);
        Assert.False(taken);
    }

    [Fact]
    public async Task MatriculaTaken_DifferentUniversity_ReturnsFalse()
    {
        var taken = await _repository.MatriculaTakenInUniversityAsync("S123", universityId: 2, excludeUserId: 99);
        Assert.False(taken);
    }

    [Fact]
    public async Task MatriculaTaken_UnknownMatricula_ReturnsFalse()
    {
        var taken = await _repository.MatriculaTakenInUniversityAsync("DOES-NOT-EXIST", universityId: 1, excludeUserId: 99);
        Assert.False(taken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
