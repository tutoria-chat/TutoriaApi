using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _courseRepository;
    private readonly TutoriaDbContext _context;

    public CourseService(ICourseRepository courseRepository, TutoriaDbContext context)
    {
        _courseRepository = courseRepository;
        _context = context;
    }

    public async Task<Course?> GetByIdAsync(int id)
    {
        return await _courseRepository.GetByIdAsync(id);
    }

    public async Task<Course?> GetWithDetailsAsync(int id)
    {
        return await _courseRepository.GetWithDetailsAsync(id);
    }

    public async Task<(IEnumerable<Course> Items, int Total)> GetPagedAsync(
        int? universityId,
        string? search,
        int page,
        int pageSize)
    {
        return await _courseRepository.SearchAsync(universityId, search, page, pageSize);
    }

    public async Task<Course> CreateAsync(Course course)
    {
        // Validate: Check if course with same code exists in university
        var exists = await _courseRepository.ExistsByCodeAndUniversityAsync(course.Code, course.UniversityId);
        if (exists)
        {
            throw new InvalidOperationException("Course with this code already exists in this university");
        }

        return await _courseRepository.AddAsync(course);
    }

    public async Task<Course> UpdateAsync(int id, Course course)
    {
        var existing = await _courseRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        existing.Name = course.Name;
        existing.Code = course.Code;
        existing.Description = course.Description;
        existing.UniversityId = course.UniversityId;

        await _courseRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var course = await _courseRepository.GetByIdAsync(id);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        await _courseRepository.DeleteAsync(course);
    }

    public async Task AssignProfessorAsync(int courseId, int professorId)
    {
        // Check if course exists
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course == null)
        {
            throw new KeyNotFoundException("Course not found");
        }

        // Check if professor exists
        var professor = await _context.Professors.FindAsync(professorId);
        if (professor == null)
        {
            // Try Users table
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == professorId && u.UserType == "professor");
            if (user == null)
            {
                throw new KeyNotFoundException("Professor not found");
            }
        }

        // Check if assignment already exists
        var exists = await _context.ProfessorCourses
            .AnyAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);

        if (exists)
        {
            throw new InvalidOperationException("Professor is already assigned to this course");
        }

        // Create assignment
        var professorCourse = new ProfessorCourse
        {
            ProfessorId = professorId,
            CourseId = courseId
        };

        await _context.ProfessorCourses.AddAsync(professorCourse);
        await _context.SaveChangesAsync();
    }

    public async Task UnassignProfessorAsync(int courseId, int professorId)
    {
        var professorCourse = await _context.ProfessorCourses
            .FirstOrDefaultAsync(pc => pc.ProfessorId == professorId && pc.CourseId == courseId);

        if (professorCourse == null)
        {
            throw new KeyNotFoundException("Professor is not assigned to this course");
        }

        _context.ProfessorCourses.Remove(professorCourse);
        await _context.SaveChangesAsync();
    }
}
