using Microsoft.EntityFrameworkCore;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class StudentServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ICourseRepository> _courseRepositoryMock;
    private readonly Mock<IStudentCourseRepository> _studentCourseRepositoryMock;
    private readonly StudentService _service;

    public StudentServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _courseRepositoryMock = new Mock<ICourseRepository>();
        _studentCourseRepositoryMock = new Mock<IStudentCourseRepository>();

        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new TutoriaDbContext(options);

        _service = new StudentService(
            _userRepositoryMock.Object,
            _courseRepositoryMock.Object,
            _studentCourseRepositoryMock.Object,
            context);
    }

    private Course CreateTestCourse(int id = 1, int universityId = 10)
    {
        return new Course
        {
            Id = id,
            Name = "Test Course",
            Code = "CS101",
            UniversityId = universityId
        };
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SetsExternalIdAndUniversityAndEnrolls()
    {
        // Arrange
        var course = CreateTestCourse();
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(course.Id)).ReturnsAsync(course);
        _userRepositoryMock.Setup(r => r.ExistsByUsernameAsync("joao")).ReturnsAsync(false);
        _userRepositoryMock.Setup(r => r.ExistsByEmailAsync("joao@uni.edu")).ReturnsAsync(false);
        _userRepositoryMock.Setup(r => r.StudentExistsByExternalIdAsync("MAT123", course.UniversityId))
            .ReturnsAsync(false);
        _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User u) => { u.UserId = 42; return u; });
        _studentCourseRepositoryMock.Setup(r => r.EnrollStudentInCourseAsync(It.IsAny<int>(), course.Id))
            .Returns(Task.CompletedTask);

        // Act
        var student = await _service.CreateAsync("joao", "joao@uni.edu", "Joao", "Silva", " MAT123 ", course.Id);

        // Assert
        Assert.Equal("MAT123", student.ExternalId);
        Assert.Equal(course.UniversityId, student.UniversityId);
        Assert.Equal("student", student.UserType);
        Assert.Null(student.HashedPassword);
        _studentCourseRepositoryMock.Verify(r => r.EnrollStudentInCourseAsync(42, course.Id), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_MissingMatricula_ThrowsInvalidOperationException(string externalId)
    {
        // Arrange
        var course = CreateTestCourse();
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(course.Id)).ReturnsAsync(course);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync("joao", "joao@uni.edu", "Joao", "Silva", externalId, course.Id));
        Assert.Contains("Matricula", ex.Message);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateMatriculaInUniversity_ThrowsInvalidOperationException()
    {
        // Arrange
        var course = CreateTestCourse();
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(course.Id)).ReturnsAsync(course);
        _userRepositoryMock.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>())).ReturnsAsync(false);
        _userRepositoryMock.Setup(r => r.StudentExistsByExternalIdAsync("MAT123", course.UniversityId))
            .ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync("joao", "joao@uni.edu", "Joao", "Silva", "MAT123", course.Id));
        Assert.Contains("matricula", ex.Message);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_NonExistentCourse_ThrowsKeyNotFoundException()
    {
        // Arrange
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Course?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.CreateAsync("joao", "joao@uni.edu", "Joao", "Silva", "MAT123", 999));
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        var course = CreateTestCourse();
        _courseRepositoryMock.Setup(r => r.GetByIdAsync(course.Id)).ReturnsAsync(course);
        _userRepositoryMock.Setup(r => r.ExistsByUsernameAsync("joao")).ReturnsAsync(true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync("joao", "joao@uni.edu", "Joao", "Silva", "MAT123", course.Id));
        Assert.Contains("Username", ex.Message);
    }
}
