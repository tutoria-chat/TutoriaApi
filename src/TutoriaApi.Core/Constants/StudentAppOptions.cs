namespace TutoriaApi.Core.Constants;

/// <summary>TutorIA Estudantes settings. Bound from the "StudentApp" section (SSM in AWS).</summary>
public class StudentAppOptions
{
    public const string SectionName = "StudentApp";

    /// <summary>Public base URL of this API (guardian consent links in emails). Empty = derive from the request.</summary>
    public string? PublicApiUrl { get; set; }

    /// <summary>RevenueCat secret (sk_…) API v1 key — the server re-reads customers after webhooks/app syncs.</summary>
    public string? RevenueCatSecretKey { get; set; }
    /// <summary>Exact Authorization header value configured on the RevenueCat webhook.</summary>
    public string? RevenueCatWebhookAuth { get; set; }
    /// <summary>Optional HMAC signing secret of the RevenueCat webhook (recommended).</summary>
    public string? RevenueCatWebhookHmacSecret { get; set; }

    /// <summary>QA only: lets the app turn plans on without a store purchase. Keep false in production.</summary>
    public bool BillingDevMode { get; set; }

    /// <summary>Optional Expo access token (only if "enhanced push security" is on in the Expo account).</summary>
    public string? ExpoAccessToken { get; set; }

    /// <summary>Contact shown on the guardian consent page and legal pages.</summary>
    public string DpoEmail { get; set; } = "privacidade@tutoria.tec.br";
    public string CompanyName { get; set; } = "[preencher: razão social]";
    public string CompanyCnpj { get; set; } = "[preencher: CNPJ]";
}
