using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Auth;
using TutoriaApi.Web.API.Controllers;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

/// <summary>GET api/external/grading/{universityId}/config — what the Moodle plugin reads.</summary>
public class ExternalGradingConfigControllerTests
{
    private readonly Mock<IGradingModeService> _modes = new();

    private ExternalGradingController Controller(bool institutionKey)
    {
        var http = new DefaultHttpContext();
        if (institutionKey) http.Items[ExternalApiKeyAttribute.UniversityIdItemKey] = 10;
        return new ExternalGradingController(Mock.Of<IGradingJobService>(), _modes.Object,
            Mock.Of<ILogger<ExternalGradingController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static object? Prop(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value);

    [Fact]
    public async Task GetConfig_InstitutionKey_PassesFlagAndReturnsEffectiveMode()
    {
        _modes.Setup(m => m.GetForExternalAsync(10, true))
            .ReturnsAsync(new GradingModeState(10, "corporate", "score", "score", null, null, "c"));

        var ok = Assert.IsType<OkObjectResult>(await Controller(true).GetConfig(10));
        Assert.Equal("score", Prop(ok.Value!, "gradingMode"));
        Assert.Equal(false, Prop(ok.Value!, "needsInstitutionKey"));
    }

    [Fact]
    public async Task GetConfig_SharedKeyOnScoreInstitution_SaysItNeedsInstitutionKey()
    {
        _modes.Setup(m => m.GetForExternalAsync(10, false))
            .ReturnsAsync(new GradingModeState(10, "corporate", "score", "feedback", null, null, "c"));

        var ok = Assert.IsType<OkObjectResult>(await Controller(false).GetConfig(10));
        Assert.Equal("feedback", Prop(ok.Value!, "gradingMode"));
        Assert.Equal(true, Prop(ok.Value!, "needsInstitutionKey"));
    }

    [Fact]
    public async Task GetConfig_UnknownUniversity_Is404()
    {
        _modes.Setup(m => m.GetForExternalAsync(10, It.IsAny<bool>())).ThrowsAsync(new KeyNotFoundException("nope"));
        Assert.IsType<NotFoundObjectResult>(await Controller(true).GetConfig(10));
    }

    [Fact]
    public async Task GetConfig_UnexpectedError_Is500()
    {
        _modes.Setup(m => m.GetForExternalAsync(10, It.IsAny<bool>())).ThrowsAsync(new Exception("db"));
        var result = Assert.IsType<ObjectResult>(await Controller(true).GetConfig(10));
        Assert.Equal(500, result.StatusCode);
    }
}
