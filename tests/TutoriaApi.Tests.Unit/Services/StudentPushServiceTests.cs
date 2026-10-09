using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class StudentPushServiceTests : IDisposable
{
    private readonly StudentAppTestKit _kit = new();
    private readonly List<JsonElement> _sent = new();
    private string _tickets = """{"data":[]}""";
    private readonly StudentPushService _push;

    public StudentPushServiceTests()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken _) =>
            {
                Assert.Equal(StudentPushService.ExpoPushUrl, req.RequestUri!.ToString());
                using var doc = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                foreach (var m in doc.RootElement.EnumerateArray()) _sent.Add(m.Clone());
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_tickets) };
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));
        _push = new StudentPushService(_kit.Repo, factory.Object, Microsoft.Extensions.Options.Options.Create(_kit.Options),
            _kit.Clock, Mock.Of<ILogger<StudentPushService>>());
    }

    public void Dispose() => _kit.Dispose();

    [Fact]
    public async Task SendAsync_RespectsPreferences()
    {
        var user = await _kit.RegisterAsync();
        await _kit.Service.RegisterDeviceAsync(user.UserId, "ExponentPushToken[aaa111]", "ios");
        await _push.SendAsync(new[] { user.UserId }, "Redação corrigida", "Nota sugerida: 720",
            new Dictionary<string, object> { ["url"] = "/essay/1" }, "updates");
        Assert.Single(_sent);
        Assert.Equal("/essay/1", _sent[0].GetProperty("data").GetProperty("url").GetString());

        _kit.Db.StudentProfiles.Single(p => p.UserId == user.UserId).NotifyUpdates = false;
        _kit.Db.SaveChanges();
        await _push.SendAsync(new[] { user.UserId }, "x", "y", null, "updates");
        Assert.Single(_sent);
    }

    [Fact]
    public async Task SendAsync_DeviceNotRegistered_RemovesToken()
    {
        var user = await _kit.RegisterAsync();
        await _kit.Service.RegisterDeviceAsync(user.UserId, "ExponentPushToken[dead00]", "android");
        _tickets = """{"data":[{"status":"error","details":{"error":"DeviceNotRegistered"}}]}""";
        await _push.SendAsync(new[] { user.UserId }, "x", "y", null, "updates");
        Assert.Empty(_kit.Db.DeviceTokens);
    }

    [Fact]
    public async Task SendStreakRemindersAsync_OncePerDay_OnlyStudiedYesterday()
    {
        var yesterday = StudentApp.TodayBrazil(StudentAppTestKit.Now).AddDays(-1);
        var studied = await _kit.RegisterAsync();
        var idle = await _kit.RegisterAsync(email: "idle@example.com");
        foreach (var u in new[] { studied, idle })
            await _kit.Service.RegisterDeviceAsync(u.UserId, $"ExponentPushToken[t{u.UserId}xyz]", "ios");
        _kit.Db.StudentProgress.Add(new StudentProgress { StudentId = studied.UserId, CurrentStreakDays = 4, LastActivityDate = yesterday });
        _kit.Db.StudentProgress.Add(new StudentProgress { StudentId = idle.UserId, CurrentStreakDays = 2, LastActivityDate = yesterday.AddDays(-3) });
        _kit.Db.SaveChanges();

        Assert.Equal(1, await _push.SendStreakRemindersAsync());
        Assert.Equal(0, await _push.SendStreakRemindersAsync());
        Assert.Single(_sent);
        Assert.Contains("4 dias", _sent[0].GetProperty("body").GetString());
    }
}
