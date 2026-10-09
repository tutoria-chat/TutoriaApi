using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Exceptions;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Controllers;
using TutoriaApi.Web.API.DTOs;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

public class StudentAppControllerTests
{
    private readonly Mock<IStudentAppService> _service = new();
    private readonly Mock<IStudentBillingService> _billing = new();
    private readonly Mock<IUserTokenService> _tokens = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly StudentAppController _controller;

    public StudentAppControllerTests()
    {
        _currentUser.Setup(c => c.GetUserId()).Returns(7);
        _controller = new StudentAppController(_service.Object, _billing.Object, _tokens.Object, _currentUser.Object,
            Microsoft.Extensions.Options.Options.Create(new StudentAppOptions { PublicApiUrl = "https://api.test" }),
            Mock.Of<ILogger<StudentAppController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static (int Status, JsonElement Body) Read(IActionResult result)
    {
        var obj = Assert.IsAssignableFrom<ObjectResult>(result);
        return (obj.StatusCode ?? 200, JsonSerializer.SerializeToElement(obj.Value));
    }

    [Fact]
    public async Task Register_Success_Returns201WithTokensAndProfile()
    {
        var user = new User { UserId = 7, Username = "a", Email = "a@x.com", FirstName = "A", LastName = "", UserType = "student" };
        _service.Setup(s => s.RegisterAsync(It.IsAny<StudentRegisterInput>())).ReturnsAsync(user);
        _service.Setup(s => s.GetMeAsync(7)).ReturnsAsync(new StudentMeResponse { Id = 7 });
        _tokens.Setup(t => t.IssueAsync(user)).ReturnsAsync(("access", "refresh"));

        var result = await _controller.Register(new StudentRegisterRequest
        {
            Name = "Ana", Email = "a@x.com", Password = "senha-forte-1", BirthDate = new DateOnly(2000, 1, 1), AcceptTerms = true,
        });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
        var body = Assert.IsType<StudentAuthResponse>(created.Value);
        Assert.Equal("access", body.AccessToken);
        Assert.Equal(7, body.User.Id);
    }

    [Fact]
    public async Task ServiceStudentAppException_MapsToStatusAndCode()
    {
        _service.Setup(s => s.GetMeAsync(7)).ThrowsAsync(new StudentAppException(402, "plan_required", "Escolha um plano."));
        var (status, body) = Read(await _controller.GetMe());
        Assert.Equal(402, status);
        Assert.Equal("plan_required", body.GetProperty("code").GetString());
        Assert.Equal("Escolha um plano.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithoutDetails()
    {
        _service.Setup(s => s.GetAgentsAsync(7)).ThrowsAsync(new Exception("db password leaked?"));
        var (status, body) = Read(await _controller.GetAgents());
        Assert.Equal(500, status);
        Assert.DoesNotContain("password", body.ToString());
    }

    [Fact]
    public async Task UpdateMe_PassesOnlyPresentFields()
    {
        StudentProfileUpdateInput? captured = null;
        _service.Setup(s => s.UpdateProfileAsync(7, It.IsAny<StudentProfileUpdateInput>()))
            .Callback((int _, StudentProfileUpdateInput i) => captured = i)
            .ReturnsAsync(new StudentMeResponse());
        var body = JsonDocument.Parse("""{"major":"Direito","university":null,"notifyStreak":false}""").RootElement;

        await _controller.UpdateMe(body);

        Assert.NotNull(captured);
        Assert.Equal("Direito", captured!.Major);
        Assert.True(captured.HasMajor);
        Assert.True(captured.HasUniversity);
        Assert.Null(captured.University);
        Assert.False(captured.HasSemester);
        Assert.False(captured.NotifyStreak);
    }

    [Fact]
    public async Task RequestGuardian_UsesConfiguredPublicUrl()
    {
        _service.Setup(s => s.RequestGuardianConsentAsync(7, "Maria", "m@x.com", "https://api.test")).ReturnsAsync(new StudentMeResponse());
        var (status, _) = Read(await _controller.RequestGuardian(new StudentGuardianRequest { GuardianName = "Maria", GuardianEmail = "m@x.com" }));
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task Webhook_ReturnsServiceStatus()
    {
        _billing.Setup(b => b.HandleWebhookAsync("{}", "Bearer x", "")).ReturnsAsync(401);
        _controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        _controller.ControllerContext.HttpContext.Request.Headers.Authorization = "Bearer x";
        var result = Assert.IsType<ObjectResult>(await _controller.RevenueCatWebhook());
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GuardianConsentPage_InvalidToken_ShowsInvalidLink()
    {
        _service.Setup(s => s.GetGuardianRequestAsync("bad")).ReturnsAsync((GuardianRequestInfo?)null);
        var page = Assert.IsType<ContentResult>(await _controller.GuardianConsentPage("bad"));
        Assert.Contains("Link inválido", page.Content);
    }

    [Fact]
    public async Task GuardianConsentPage_ValidToken_EscapesNames()
    {
        _service.Setup(s => s.GetGuardianRequestAsync("ok")).ReturnsAsync(new GuardianRequestInfo("<b>Ana</b>", "Maria", "dpo@x.com"));
        var page = Assert.IsType<ContentResult>(await _controller.GuardianConsentPage("ok"));
        Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", page.Content);
        Assert.Contains("Autorizo", page.Content);
    }

    [Fact]
    public async Task GuardianConsentDecide_UnknownDecision_ShowsInvalidLink()
    {
        var page = Assert.IsType<ContentResult>(await _controller.GuardianConsentDecide("ok", "maybe"));
        Assert.Contains("Link inválido", page.Content);
        _service.Verify(s => s.DecideGuardianConsentAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void GetPlans_ReturnsCatalog()
    {
        var ok = Assert.IsType<OkObjectResult>(_controller.GetPlans());
        Assert.Equal(4, ((IEnumerable<StudentPlanResponse>)ok.Value!).Count());
    }
}
