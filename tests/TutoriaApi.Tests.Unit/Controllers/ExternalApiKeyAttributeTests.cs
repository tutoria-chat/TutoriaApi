using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.Auth;
using Xunit;

namespace TutoriaApi.Tests.Unit.Controllers;

/// <summary>
/// The external API (Moodle grading plugin) accepts the legacy shared key for any
/// institution, and an institution's own key only for that institution.
/// </summary>
public class ExternalApiKeyAttributeTests
{
    private const string Shared = "the-legacy-shared-key";
    private readonly Mock<IUniversityApiKeyService> _keys = new();

    private async Task<AuthorizationFilterContext> Run(string? apiKey, int? routeUniversityId, string? sharedConfig = Shared)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ExternalApi:ApiKey"] = sharedConfig })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton(_keys.Object)
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = services };
        if (apiKey != null) http.Request.Headers["X-Api-Key"] = apiKey;

        var route = new RouteData();
        if (routeUniversityId != null) route.Values["universityId"] = routeUniversityId.Value.ToString();

        var context = new AuthorizationFilterContext(
            new ActionContext(http, route, new ActionDescriptor()), new List<IFilterMetadata>());
        await new ExternalApiKeyAttribute().OnAuthorizationAsync(context);
        return context;
    }

    private static int? StatusOf(AuthorizationFilterContext c) => (c.Result as ObjectResult)?.StatusCode;

    [Fact]
    public async Task MissingKey_Is401()
    {
        var c = await Run(null, 1);
        Assert.Equal(401, StatusOf(c));
    }

    [Fact]
    public async Task LegacySharedKey_WorksForAnyUniversity()
    {
        Assert.Null((await Run(Shared, 1)).Result);
        Assert.Null((await Run(Shared, 999)).Result);
        _keys.Verify(k => k.ValidateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InstitutionKey_ForItsOwnUniversity_IsAllowed()
    {
        _keys.Setup(k => k.ValidateAsync("tgk_mine")).ReturnsAsync(1);

        var c = await Run("tgk_mine", 1);

        Assert.Null(c.Result);
        Assert.Equal(1, c.HttpContext.Items[ExternalApiKeyAttribute.UniversityIdItemKey]);
    }

    [Fact]
    public async Task InstitutionKey_ForAnotherUniversity_Is403()
    {
        _keys.Setup(k => k.ValidateAsync("tgk_mine")).ReturnsAsync(1);

        var c = await Run("tgk_mine", 2);

        Assert.Equal(403, StatusOf(c));
    }

    [Fact]
    public async Task UnknownOrRevokedKey_Is401()
    {
        _keys.Setup(k => k.ValidateAsync(It.IsAny<string>())).ReturnsAsync((int?)null);

        var c = await Run("tgk_revoked", 1);

        Assert.Equal(401, StatusOf(c));
    }

    [Fact]
    public async Task InstitutionKeys_WorkEvenWithoutASharedKeyConfigured()
    {
        _keys.Setup(k => k.ValidateAsync("tgk_mine")).ReturnsAsync(1);

        Assert.Null((await Run("tgk_mine", 1, sharedConfig: null)).Result);
        Assert.Equal(401, StatusOf(await Run("anything", 1, sharedConfig: null)));
    }
}
