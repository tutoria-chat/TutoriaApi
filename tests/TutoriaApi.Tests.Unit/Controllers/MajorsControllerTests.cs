using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

public class MajorsControllerTests
{
    private readonly Mock<IMajorService> _service = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly MajorsController _controller;

    public MajorsControllerTests()
    {
        _controller = new MajorsController(_service.Object, _currentUser.Object, Mock.Of<ILogger<MajorsController>>());
    }

    private void SignInAs(string userType, int? universityId)
        => _currentUser.Setup(c => c.GetCurrentUser())
            .Returns(new User
            {
                UserId = 1, Username = "u", Email = "u@x.com", FirstName = "U", LastName = "U",
                UserType = userType, UniversityId = universityId,
            });

    [Fact]
    public async Task GetMajors_ManagerOfOtherUniversity_ReturnsNotFound()
    {
        SignInAs("manager", universityId: 2);
        var result = await _controller.GetMajors(universityId: 1);
        Assert.IsType<NotFoundObjectResult>(result.Result);
        _service.Verify(s => s.GetByUniversityAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetMajors_ManagerOfOwnUniversity_ReturnsList()
    {
        SignInAs("manager", universityId: 1);
        _service.Setup(s => s.GetByUniversityAsync(1))
            .ReturnsAsync(new List<Major> { new() { Id = 9, UniversityId = 1, Name = "Direito" } });

        var result = await _controller.GetMajors(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<MajorDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("Direito", list[0].Name);
    }

    [Fact]
    public async Task GetMajors_SuperAdmin_CanViewAnyUniversity()
    {
        SignInAs("super_admin", universityId: null);
        _service.Setup(s => s.GetByUniversityAsync(7)).ReturnsAsync(new List<Major>());

        var result = await _controller.GetMajors(7);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateMajor_Duplicate_ReturnsConflict()
    {
        SignInAs("manager", universityId: 1);
        _service.Setup(s => s.CreateAsync(1, "Direito"))
            .ThrowsAsync(new InvalidOperationException("A major with this name already exists at this university"));

        var result = await _controller.CreateMajor(1, new MajorCreateRequest { Name = "Direito" });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteMajor_OtherUniversity_ReturnsNotFound()
    {
        SignInAs("manager", universityId: 2);
        var result = await _controller.DeleteMajor(universityId: 1, majorId: 5);
        Assert.IsType<NotFoundObjectResult>(result);
        _service.Verify(s => s.DeleteAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
