using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class StudentImportJobRepository : Repository<StudentImportJob>, IStudentImportJobRepository
{
    public StudentImportJobRepository(TutoriaDbContext context) : base(context) { }

    public async Task<IEnumerable<StudentImportJob>> GetByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .Include(j => j.Course)
            .Include(j => j.UploadedBy)
            .Where(j => j.CourseId == courseId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StudentImportJob>> GetByUniversityIdAsync(int universityId)
    {
        return await _dbSet
            .Include(j => j.Course)
            .Include(j => j.UploadedBy)
            .Where(j => j.UniversityId == universityId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }
}
