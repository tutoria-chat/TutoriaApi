using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Tests.Unit.Controllers;

public class CourseLifecycleControllerTests
{
    private readonly Mock<ICourseLifecycleService> _service = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly CourseLifecycleController _controller;
    private readonly User _caller = new()
    {
        UserId = 7, Username = "prof", Email = "p@uni.br", FirstName = "P", LastName = "X", UserType = "professor",
    };

    public CourseLifecycleControllerTests()
    {
        _currentUser.Setup(c => c.GetCurrentUser()).Returns(_caller);
        _controller = new CourseLifecycleController(_service.Object, _currentUser.Object,
            Mock.Of<ILogger<CourseLifecycleController>>());
    }

    [Fact]
    public async Task Get_ReturnsDto()
    {
        _service.Setup(s => s.GetAsync(3, _caller))
            .ReturnsAsync(new CourseLifecycleInfo { CourseId = 3, Status = "running", CanManage = true });

        var result = await _controller.Get(3);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CourseLifecycleDto>(ok.Value);
        Assert.Equal("running", dto.Status);
        Assert.True(dto.CanManage);
    }

    [Fact]
    public async Task Pause_NotAllowed_Returns403()
    {
        _service.Setup(s => s.PauseAsync(3, _caller)).ThrowsAsync(new UnauthorizedAccessException("no"));

        var result = await _controller.Pause(3);

        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Extend_MissingCourse_Returns404()
    {
        _service.Setup(s => s.ExtendAsync(3, _caller)).ThrowsAsync(new KeyNotFoundException());

        var result = await _controller.Extend(3);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ActiveCourse_Returns400()
    {
        _service.Setup(s => s.DeleteAsync(3, _caller)).ThrowsAsync(new InvalidOperationException("deactivate first"));

        var result = await _controller.Delete(3);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Delete_Success_Returns204()
    {
        _service.Setup(s => s.DeleteAsync(3, _caller)).Returns(Task.CompletedTask);

        Assert.IsType<NoContentResult>(await _controller.Delete(3));
    }

    [Fact]
    public async Task Deactivate_UnexpectedError_Returns500()
    {
        _service.Setup(s => s.DeactivateAsync(3, _caller)).ThrowsAsync(new Exception("db down"));

        var result = await _controller.Deactivate(3);

        Assert.Equal(500, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }
}
