using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

/// <summary>
/// Regression: a user with no university (open self-registration creates these)
/// must not be treated like a super admin when reading modules by id.
/// </summary>
public class ModulesControllerOwnershipTests
{
    private readonly Mock<IModuleService> _moduleService = new();
    private readonly Mock<ICourseRepository> _courseRepository = new();
    private readonly Mock<IModuleRepository> _moduleRepository = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly ModulesController _controller;

    public ModulesControllerOwnershipTests()
    {
        _controller = new ModulesController(
            _moduleService.Object,
            _courseRepository.Object,
            _moduleRepository.Object,
            Mock.Of<ISubscriptionRepository>(),
            Mock.Of<IAIModelService>(),
            _currentUser.Object,
            Mock.Of<ISqsMessagingService>(),
            Mock.Of<ILogger<ModulesController>>());

        _moduleRepository.Setup(r => r.GetByIdAsync(7))
            .ReturnsAsync(new Module { Id = 7, Name = "M", Code = "M", SystemPrompt = "secret", CourseId = 3 });
        _courseRepository.Setup(r => r.GetByIdAsync(3))
            .ReturnsAsync(new Course { Id = 3, Name = "C", Code = "C", UniversityId = 1 });
    }

    private void SignInAs(string userType, int? universityId)
        => _currentUser.Setup(c => c.GetCurrentUser()).Returns(new User
        {
            UserId = 1, Username = "u", Email = "u@x.com", FirstName = "U", LastName = "U",
            UserType = userType, UniversityId = universityId,
        });

    [Fact]
    public async Task GetModule_UserWithoutUniversity_ReturnsNotFound()
    {
        SignInAs("professor", universityId: null);

        var result = await _controller.GetModule(7);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        _moduleService.Verify(s => s.GetWithDetailsAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetModule_UserOfOtherUniversity_ReturnsNotFound()
    {
        SignInAs("manager", universityId: 2);

        var result = await _controller.GetModule(7);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
