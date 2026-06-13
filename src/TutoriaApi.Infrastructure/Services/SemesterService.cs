using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class SemesterService : ISemesterService
{
    private readonly ISemesterRepository _repository;

    public SemesterService(ISemesterRepository repository)
    {
        _repository = repository;
    }

    public Task<List<Semester>> GetByUniversityAsync(int universityId) =>
        _repository.GetByUniversityIdAsync(universityId);

    public Task<Semester?> GetByIdAsync(int id) => _repository.GetByIdWithUniversityAsync(id);

    public async Task<Semester> CreateAsync(Semester semester)
    {
        Validate(semester.StartsAtUtc, semester.EndsAtUtc);
        return await _repository.AddAsync(semester);
    }

    public async Task<Semester> UpdateAsync(int id, string? label, DateTime? startsAtUtc, DateTime? endsAtUtc)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Semester not found");

        if (!string.IsNullOrWhiteSpace(label)) existing.Label = label.Trim();
        if (startsAtUtc.HasValue) existing.StartsAtUtc = DateTime.SpecifyKind(startsAtUtc.Value, DateTimeKind.Utc);
        if (endsAtUtc.HasValue) existing.EndsAtUtc = DateTime.SpecifyKind(endsAtUtc.Value, DateTimeKind.Utc);
        Validate(existing.StartsAtUtc, existing.EndsAtUtc);

        await _repository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Semester not found");
        await _repository.DeleteAsync(existing);
    }

    private static void Validate(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        if (endsAtUtc <= startsAtUtc)
            throw new InvalidOperationException("Semester end must be after its start");
    }
}
