using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

/// <summary>
/// Covers PUT /api/auth/me (UpdateCurrentUser) birthdate handling: it must persist
/// a picked date as a UTC calendar date, leave an omitted birthdate untouched, and
/// echo the stored value back in the response DTO.
/// </summary>
public class AuthControllerUpdateProfileTests : IDisposable
{
    private readonly TutoriaDbContext _context;
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly AuthController _controller;

    public AuthControllerUpdateProfileTests()
    {
        var options = new DbContextOptionsBuilder<TutoriaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TutoriaDbContext(options);

        _controller = new AuthController(
            Mock.Of<IApiClientRepository>(),
            _userRepository.Object,
            Mock.Of<IJwtService>(),
            Mock.Of<IEmailService>(),
            _context,
            Mock.Of<IUniversityRepository>(),
            Mock.Of<IPlanRepository>(),
            Mock.Of<ISubscriptionRepository>(),
            Mock.Of<IPermissionService>(),
            Mock.Of<IUserUniversityService>(),
            Mock.Of<IUserUniversityRepository>(),
            Mock.Of<IUserInvitationService>(),
            Mock.Of<IMajorService>(),
            Mock.Of<ILogger<AuthController>>());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = SignedInUser(1) }
        };
    }

    private static ClaimsPrincipal SignedInUser(int userId)
        => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"));

    private User ArrangeCurrentUser(DateTime? existingBirthdate = null)
    {
        var user = new User
        {
            UserId = 1,
            Username = "prof1",
            Email = "prof1@uni.edu",
            FirstName = "Ana",
            LastName = "Souza",
            UserType = "professor",
            UniversityId = 1,
            IsActive = true,
            Birthdate = existingBirthdate,
        };

        _userRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _userRepository.Setup(r => r.GetByIdWithIncludesAsync(1)).ReturnsAsync(user);
        _userRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        return user;
    }

    [Fact]
    public async Task UpdateCurrentUser_WithBirthdate_PersistsAsUtcCalendarDate()
    {
        var user = ArrangeCurrentUser();
        // A date picker sends a plain date; simulate a value with a stray time/kind.
        var request = new UpdateProfileRequest
        {
            Birthdate = new DateTime(2000, 1, 15, 13, 45, 0, DateTimeKind.Local)
        };

        var result = await _controller.UpdateCurrentUser(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<UserDto>(ok.Value);

        Assert.NotNull(user.Birthdate);
        Assert.Equal(new DateTime(2000, 1, 15), user.Birthdate!.Value);
        Assert.Equal(DateTimeKind.Utc, user.Birthdate.Value.Kind); // Npgsql timestamptz requires UTC
        Assert.Equal(new TimeSpan(0, 0, 0), user.Birthdate.Value.TimeOfDay); // time stripped
        Assert.Equal(user.Birthdate, dto.Birthdate); // echoed back in the response
        _userRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateCurrentUser_WithoutBirthdate_LeavesExistingUntouched()
    {
        var existing = new DateTime(1990, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var user = ArrangeCurrentUser(existingBirthdate: existing);
        var request = new UpdateProfileRequest { FirstName = "Ana Maria" }; // no birthdate

        var result = await _controller.UpdateCurrentUser(request);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(existing, user.Birthdate);
    }

    public void Dispose() => _context.Dispose();
}
