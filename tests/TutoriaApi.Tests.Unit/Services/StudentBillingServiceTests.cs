using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Exceptions;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class StudentBillingServiceTests : IDisposable
{
    private const string Auth = "Bearer test-webhook-secret";
    private readonly StudentAppTestKit _kit = new();
    /// <summary>RevenueCat customers by app user id ("u{id}"); missing = no entitlements.</summary>
    private readonly Dictionary<string, string> _customers = new();
    private bool _revenueCatDown;
    private readonly StudentBillingService _billing;

    public StudentBillingServiceTests()
    {
        _kit.Options.RevenueCatSecretKey = "sk_test";
        _kit.Options.RevenueCatWebhookAuth = Auth;

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (_revenueCatDown) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                Assert.Equal("Bearer sk_test", req.Headers.Authorization!.ToString());
                var appUser = req.RequestUri!.Segments.Last();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_customers.GetValueOrDefault(appUser, Customer(null))),
                };
            });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler.Object));

        _billing = new StudentBillingService(_kit.Repo, _kit.Service, factory.Object,
            Microsoft.Extensions.Options.Options.Create(_kit.Options), _kit.Clock, Mock.Of<ILogger<StudentBillingService>>());
    }

    public void Dispose() => _kit.Dispose();

    private static string Iso(DateTime d) => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string Customer(string? plan, int daysLeft = 20, bool cancelled = false, bool billingIssue = false,
        string product = "tutoria_pro_monthly", int? graceDays = null)
    {
        var entitlements = new Dictionary<string, object?>();
        var subscriptions = new Dictionary<string, object?>();
        if (plan != null)
        {
            var now = StudentAppTestKit.Now;
            var expires = Iso(now.AddDays(daysLeft));
            entitlements[plan] = new Dictionary<string, object?>
            {
                ["expires_date"] = expires,
                ["grace_period_expires_date"] = graceDays == null ? null : Iso(now.AddDays(graceDays.Value)),
                ["product_identifier"] = product,
                ["purchase_date"] = Iso(now.AddDays(-10)),
            };
            subscriptions[product] = new Dictionary<string, object?>
            {
                ["store"] = "app_store",
                ["expires_date"] = expires,
                ["purchase_date"] = Iso(now.AddDays(-10)),
                ["unsubscribe_detected_at"] = cancelled ? Iso(now) : null,
                ["billing_issues_detected_at"] = billingIssue ? Iso(now) : null,
            };
        }
        return JsonSerializer.Serialize(new { subscriber = new { entitlements, subscriptions } });
    }

    private Task<int> Hook(object evt, string? auth = Auth, string? signature = null)
        => _billing.HandleWebhookAsync(JsonSerializer.Serialize(new { api_version = "1.0", @event = evt }), auth, signature);

    // ─── ApplyCustomerInfo (pure) ────────────────────────────────────────────

    [Fact]
    public void ApplyCustomerInfo_ActiveEntitlement_SetsPlanAndPeriod()
    {
        var sub = new StudentSubscription { UserId = 1 };
        var active = StudentBillingService.ApplyCustomerInfo(sub, JsonDocument.Parse(Customer("max", cancelled: true, billingIssue: true)).RootElement, StudentAppTestKit.Now);
        Assert.True(active);
        Assert.Equal("max", sub.Plan);
        Assert.Equal("app_store", sub.Source);
        Assert.Equal(StudentAppTestKit.Now.AddDays(-10), sub.PeriodStart);
        Assert.False(sub.WillRenew);
        Assert.True(sub.BillingIssue);
    }

    [Fact]
    public void ApplyCustomerInfo_ExpiredButInGrace_KeepsAccess()
    {
        var sub = new StudentSubscription();
        Assert.True(StudentBillingService.ApplyCustomerInfo(sub, JsonDocument.Parse(Customer("pro", daysLeft: -1, graceDays: 3)).RootElement, StudentAppTestKit.Now));
        Assert.Equal(StudentAppTestKit.Now.AddDays(3), sub.PeriodEnd);
    }

    [Fact]
    public void ApplyCustomerInfo_Expired_EndsStorePlanButKeepsDevPlan()
    {
        var store = new StudentSubscription { Plan = "pro", Source = "app_store", PeriodEnd = StudentAppTestKit.Now.AddDays(5) };
        Assert.False(StudentBillingService.ApplyCustomerInfo(store, JsonDocument.Parse(Customer("pro", daysLeft: -1)).RootElement, StudentAppTestKit.Now));
        Assert.Equal(StudentAppTestKit.Now, store.PeriodEnd);

        var dev = new StudentSubscription { Plan = "pro", Source = "dev", PeriodEnd = StudentAppTestKit.Now.AddDays(5) };
        StudentBillingService.ApplyCustomerInfo(dev, JsonDocument.Parse(Customer(null)).RootElement, StudentAppTestKit.Now);
        Assert.Equal(StudentAppTestKit.Now.AddDays(5), dev.PeriodEnd);
    }

    [Theory]
    [InlineData("u42", 42)]
    [InlineData("$RCAnonymousID:abc", null)]
    [InlineData("u", null)]
    public void UserIdFrom_ParsesOnlyOurIds(string appUser, int? expected) => Assert.Equal(expected, StudentBillingService.UserIdFrom(appUser));

    // ─── Sync / webhook ──────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAsync_StorePurchase_ActivatesPlanAndDefaultAgents()
    {
        var user = await _kit.RegisterAsync();
        _customers[$"u{user.UserId}"] = Customer("max", product: "tutoria_max_monthly");

        var me = await _billing.SyncAsync(user.UserId);

        Assert.Equal("max", me.Plan!.Id);
        Assert.Equal("app_store", me.PlanSource);
        Assert.True(me.Ready);
        Assert.Equal(4, (await _kit.Service.GetAgentsAsync(user.UserId)).Count);
    }

    [Fact]
    public async Task SyncAsync_RevenueCatDown_Throws503()
    {
        var user = await _kit.RegisterAsync();
        _revenueCatDown = true;
        var ex = await Assert.ThrowsAsync<StudentAppException>(() => _billing.SyncAsync(user.UserId));
        Assert.Equal("billing_unavailable", ex.Code);
    }

    [Fact]
    public async Task Webhook_BadAuth_Returns401()
        => Assert.Equal(401, await Hook(new { id = "e1", type = "TEST" }, auth: "Bearer nope"));

    [Fact]
    public async Task Webhook_ValidEvent_SyncsAndIsIdempotent()
    {
        var user = await _kit.RegisterAsync();
        _customers[$"u{user.UserId}"] = Customer("pro");
        var evt = new { id = "evt-1", type = "INITIAL_PURCHASE", app_user_id = $"u{user.UserId}", environment = "SANDBOX" };

        Assert.Equal(200, await Hook(evt));
        Assert.Equal("pro", (await _kit.Service.GetMeAsync(user.UserId)).Plan!.Id);
        Assert.Single(_kit.Db.StudentBillingEvents);

        _customers[$"u{user.UserId}"] = Customer(null);
        Assert.Equal(200, await Hook(evt)); // duplicate: not reprocessed
        Assert.Equal("pro", (await _kit.Service.GetMeAsync(user.UserId)).Plan!.Id);
    }

    [Fact]
    public async Task Webhook_DowngradeToSimples_RequiresAreaChoice()
    {
        var user = await _kit.RegisterAsync();
        _customers[$"u{user.UserId}"] = Customer("pro");
        await Hook(new { id = "e1", type = "INITIAL_PURCHASE", app_user_id = $"u{user.UserId}" });
        _customers[$"u{user.UserId}"] = Customer("simples", product: "tutoria_simples_monthly");
        await Hook(new { id = "e2", type = "PRODUCT_CHANGE", app_user_id = $"u{user.UserId}" });
        var me = await _kit.Service.GetMeAsync(user.UserId);
        Assert.Equal("simples", me.Plan!.Id);
        Assert.True(me.NeedsAreaChoice);
        Assert.False(me.Ready);
    }

    [Fact]
    public async Task Webhook_Transfer_MovesPlanBetweenAccounts()
    {
        var a = await _kit.RegisterAsync();
        var b = await _kit.RegisterAsync(email: "b@example.com");
        _customers[$"u{a.UserId}"] = Customer("pro");
        await _billing.SyncAsync(a.UserId);
        _customers[$"u{b.UserId}"] = Customer("pro");
        _customers[$"u{a.UserId}"] = Customer(null);

        await Hook(new { id = "t1", type = "TRANSFER", transferred_from = new[] { $"u{a.UserId}" }, transferred_to = new[] { $"u{b.UserId}" } });

        Assert.Null((await _kit.Service.GetMeAsync(a.UserId)).Plan);
        Assert.Equal("pro", (await _kit.Service.GetMeAsync(b.UserId)).Plan!.Id);
    }

    [Fact]
    public async Task Webhook_RevenueCatDown_Returns503AndDoesNotRecordEvent()
    {
        var user = await _kit.RegisterAsync();
        _revenueCatDown = true;
        Assert.Equal(503, await Hook(new { id = "e9", type = "RENEWAL", app_user_id = $"u{user.UserId}" }));
        Assert.Empty(_kit.Db.StudentBillingEvents);
    }

    [Fact]
    public async Task Webhook_HmacSignature_IsVerified()
    {
        _kit.Options.RevenueCatWebhookHmacSecret = "whsec";
        var body = JsonSerializer.Serialize(new { @event = new { id = "h1", type = "TEST", app_user_id = "anon" } });
        var ts = _kit.Clock.GetUtcNow().ToUnixTimeSeconds().ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("whsec"));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{body}"))).ToLowerInvariant();

        Assert.Equal(200, await _billing.HandleWebhookAsync(body, Auth, $"t={ts},v1={sig}"));
        Assert.Equal(401, await _billing.HandleWebhookAsync(body, Auth, $"t={ts},v1=deadbeef"));
        var old = (_kit.Clock.GetUtcNow().ToUnixTimeSeconds() - 3600).ToString();
        var oldSig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{old}.{body}"))).ToLowerInvariant();
        Assert.Equal(401, await _billing.HandleWebhookAsync(body, Auth, $"t={old},v1={oldSig}")); // replayed
    }

    // ─── Dev mode ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DevActivateAsync_DevModeOff_NotFound()
    {
        var user = await _kit.RegisterAsync();
        _kit.Options.BillingDevMode = false;
        var ex = await Assert.ThrowsAsync<StudentAppException>(() => _billing.DevActivateAsync(user.UserId, "pro"));
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task DevActivateAsync_DevPlanSurvivesEmptyStoreSync()
    {
        var user = await _kit.RegisterAsync();
        await _billing.DevActivateAsync(user.UserId, "pro");
        var me = await _billing.SyncAsync(user.UserId);
        Assert.Equal("pro", me.Plan!.Id);
        Assert.Equal("dev", me.PlanSource);
    }
}
