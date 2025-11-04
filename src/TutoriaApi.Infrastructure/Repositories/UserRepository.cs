using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly TutoriaDbContext _context;

    public UserRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User?> GetByIdWithIncludesAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByUsernameWithIncludesAsync(string username)
    {
        return await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail)
    {
        return await _context.Users
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByPasswordResetTokenAsync(string token)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
    }

    public async Task<IEnumerable<User>> GetByTypeAsync(string userType)
    {
        return await _context.Users
            .Where(u => u.UserType == userType)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetByUniversityIdAsync(int universityId)
    {
        return await _context.Users
            .Where(u => u.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<(List<User> Items, int Total)> GetPagedAsync(
        string? userType,
        int? universityId,
        bool? isAdmin,
        bool? isActive,
        string? search,
        int page,
        int pageSize)
    {
        var query = _context.Users
            .Include(u => u.University)
            .AsQueryable();

        // Filter by user type
        if (!string.IsNullOrWhiteSpace(userType))
        {
            query = query.Where(u => u.UserType == userType);
        }

        // Filter by university
        if (universityId.HasValue)
        {
            query = query.Where(u => u.UniversityId == universityId.Value);
        }

        // Filter by isAdmin
        if (isAdmin.HasValue)
        {
            query = query.Where(u => u.IsAdmin == isAdmin.Value);
        }

        // Filter by isActive
        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, total);
    }

    public async Task<User> AddAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(User user)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByUsernameOrEmailAsync(string username, string email)
    {
        return await _context.Users.AnyAsync(u => u.Username == username || u.Email == email);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<bool> ExistsByUsernameExcludingUserAsync(string username, int excludeUserId)
    {
        return await _context.Users.AnyAsync(u => u.Username == username && u.UserId != excludeUserId);
    }

    public async Task<bool> ExistsByEmailExcludingUserAsync(string email, int excludeUserId)
    {
        return await _context.Users.AnyAsync(u => u.Email == email && u.UserId != excludeUserId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<(List<User> Items, int Total)> GetProfessorsPagedAsync(
        int? universityId,
        List<int>? filterByProfessorIds,
        bool? isAdmin,
        string? search,
        int page,
        int pageSize)
    {
        var query = _context.Users
            .Where(u => u.UserType == "professor")
            .Include(u => u.University)
            .AsQueryable();

        // Filter by professor IDs (for course-specific queries)
        if (filterByProfessorIds != null && filterByProfessorIds.Any())
        {
            query = query.Where(u => filterByProfessorIds.Contains(u.UserId));
        }

        if (universityId.HasValue)
        {
            query = query.Where(u => u.UniversityId == universityId.Value);
        }

        if (isAdmin.HasValue)
        {
            query = query.Where(u => u.IsAdmin == isAdmin.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var professors = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (professors, total);
    }

    public async Task<(List<User> Items, int Total)> GetStudentsPagedAsync(
        List<int>? filterByStudentIds,
        string? search,
        int page,
        int pageSize)
    {
        var query = _context.Users
            .Where(u => u.UserType == "student")
            .AsQueryable();

        // Filter by student IDs (for course-specific queries)
        if (filterByStudentIds != null && filterByStudentIds.Any())
        {
            query = query.Where(u => filterByStudentIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.Username.Contains(search) ||
                u.FirstName.Contains(search) ||
                u.LastName.Contains(search) ||
                u.Email.Contains(search));
        }

        var total = await query.CountAsync();
        var students = await query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (students, total);
    }

    public async Task<User?> GetProfessorByIdWithUniversityAsync(int professorId)
    {
        return await _context.Users
            .Where(u => u.UserType == "professor")
            .Include(u => u.University)
            .FirstOrDefaultAsync(u => u.UserId == professorId);
    }

    public async Task<User?> GetStudentByIdAsync(int studentId)
    {
        return await _context.Users
            .Where(u => u.UserType == "student")
            .FirstOrDefaultAsync(u => u.UserId == studentId);
    }
}
