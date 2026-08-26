using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Repositories;
using Xunit;

namespace TutoriaApi.Tests.Unit.Repositories;

/// <summary>
/// Covers the matricula (ExternalId) helpers used when staff set/edit a
/// per-university matricula so they can test the student widget.
/// </summary>
public class UserUniversityRepositoryTests : IDisposable
{
    private readonly TutoriaDbContext _context;
    private readonly UserUniversityRepository _repository;

    public UserUniversityRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TutoriaDbContext(options);
        _repository = new UserUniversityRepository(_context);
    }

    [Fact]
    public async Task AddAsync_WithExternalId_PersistsMatricula()
    {
        await _repository.AddAsync(userId: 1, universityId: 5, externalId: " M-001 ");

        var row = await _context.UserUniversities.SingleAsync();
        Assert.Equal("M-001", row.ExternalId); // trimmed
    }

    [Fact]
    public async Task SetExternalIdAsync_ExistingMembership_UpdatesMatricula()
    {
        await _repository.AddAsync(userId: 1, universityId: 5);

        await _repository.SetExternalIdAsync(userId: 1, universityId: 5, externalId: "NEW-9");

        var row = await _context.UserUniversities.SingleAsync();
        Assert.Equal("NEW-9", row.ExternalId);
    }

    [Fact]
    public async Task SetExternalIdAsync_EmptyString_ClearsMatricula()
    {
        await _repository.AddAsync(userId: 1, universityId: 5, externalId: "OLD");

        await _repository.SetExternalIdAsync(userId: 1, universityId: 5, externalId: "   ");

        var row = await _context.UserUniversities.SingleAsync();
        Assert.Null(row.ExternalId);
    }

    [Fact]
    public async Task SetExternalIdAsync_NoMembership_NoOp()
    {
        // Should not throw or create a row when there is no membership.
        await _repository.SetExternalIdAsync(userId: 99, universityId: 5, externalId: "X");

        Assert.False(await _context.UserUniversities.AnyAsync());
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
