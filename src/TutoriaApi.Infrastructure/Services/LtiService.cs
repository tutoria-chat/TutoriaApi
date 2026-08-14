using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Lti;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Tutoria as an LTI 1.3 Advantage tool.
///
/// SECURITY: every launch arrives as a JWT signed by the platform. Before any of it
/// is trusted we check, in order: the state we issued is known and unconsumed; the
/// signature verifies against the platform's published JWKS; the issuer and audience
/// match the registration; the token is within its validity window; the LTI version
/// is 1.3; the deployment is one we know; and any module referenced by custom
/// parameters belongs to this registration's university.
///
/// That last check matters because an LMS administrator can hand-edit the custom
/// parameters of a placed tool. The platform will faithfully sign whatever they typed,
/// so a valid signature proves the value came from that LMS — not that the LMS was
/// entitled to it.
/// </summary>
public class LtiService : ILtiService
{
    private const string LtiVersion = "1.3.0";
    private static readonly TimeSpan HandshakeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PlatformKeyCacheLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Tolerance for clock drift between the platform and us. Deliberately tight —
    /// the default five minutes is far more than an LMS launch needs.
    /// </summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly ILtiRegistrationRepository _registrations;
    private readonly ILtiToolKeyRepository _keys;
    private readonly ILtiNonceRepository _nonces;
    private readonly ILtiContextMappingRepository _contextMappings;
    private readonly IModuleRepository _modules;
    private readonly IModuleAccessTokenRepository _moduleTokens;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly LtiOptions _options;
    private readonly ILogger<LtiService> _logger;

    public LtiService(
        ILtiRegistrationRepository registrations,
        ILtiToolKeyRepository keys,
        ILtiNonceRepository nonces,
        ILtiContextMappingRepository contextMappings,
        IModuleRepository modules,
        IModuleAccessTokenRepository moduleTokens,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<LtiOptions> options,
        ILogger<LtiService> logger)
    {
        _registrations = registrations;
        _keys = keys;
        _nonces = nonces;
        _contextMappings = contextMappings;
        _modules = modules;
        _moduleTokens = moduleTokens;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateEphemeralModuleTokenAsync(int moduleId, string subject)
    {
        var token = new ModuleAccessToken
        {
            Token = GenerateSecureToken(48),
            Name = $"LTI launch ({subject})",
            Description = "Auto-issued for an LTI 1.3 launch. Short-lived and single-module.",
            ModuleId = moduleId,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_options.EphemeralTokenMinutes),
            AllowChat = true,
            AllowFileAccess = true,
        };

        await _moduleTokens.AddAsync(token);
        return token.Token;
    }

    // ---------------------------------------------------------------------
    // Step 1: third-party-initiated login
    // ---------------------------------------------------------------------

    public async Task<string> BuildLoginRedirectAsync(LtiLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Iss))
        {
            throw new ArgumentException("iss is required", nameof(request));
        }

        var registration = await _registrations.GetByIssuerAndClientIdAsync(request.Iss, request.ClientId)
            ?? throw new KeyNotFoundException(
                $"No LTI registration for issuer '{request.Iss}' and client_id '{request.ClientId ?? "(none)"}'.");

        if (!registration.IsActive)
        {
            throw new InvalidOperationException($"LTI registration {registration.Id} is disabled.");
        }

        var nonce = GenerateSecureToken();
        var state = GenerateSecureToken();

        await _nonces.AddAsync(new LtiNonce
        {
            Nonce = nonce,
            State = state,
            LtiRegistrationId = registration.Id,
            TargetLinkUri = request.TargetLinkUri,
            ExpiresAt = DateTime.UtcNow.Add(HandshakeLifetime),
        });

        // response_mode=form_post and id_token are mandated by the LTI 1.3 spec;
        // prompt=none because the platform has already authenticated the user.
        var parameters = new Dictionary<string, string?>
        {
            ["scope"] = "openid",
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["prompt"] = "none",
            ["client_id"] = registration.ClientId,
            ["redirect_uri"] = GetLaunchUri(),
            ["state"] = state,
            ["nonce"] = nonce,
            ["login_hint"] = request.LoginHint,
            ["lti_message_hint"] = request.LtiMessageHint,
        };

        var query = string.Join("&", parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

        var separator = registration.AuthLoginUrl.Contains('?') ? "&" : "?";
        return $"{registration.AuthLoginUrl}{separator}{query}";
    }

    // ---------------------------------------------------------------------
    // Step 2: validate the launch
    // ---------------------------------------------------------------------

    public async Task<LtiLaunchResult> ValidateLaunchAsync(string idToken, string state)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(state))
        {
            throw new UnauthorizedAccessException("Missing id_token or state.");
        }

        // Resolve the registration from OUR state rather than from the token's own
        // claims: the token is untrusted until we know which key should have signed it.
        var pending = await _nonces.GetByStateAsync(state)
            ?? throw new UnauthorizedAccessException("Unknown or expired LTI state.");

        var registration = await _registrations.GetWithDeploymentsAsync(pending.LtiRegistrationId)
            ?? throw new UnauthorizedAccessException("Registration for this launch no longer exists.");

        if (!registration.IsActive)
        {
            throw new UnauthorizedAccessException($"LTI registration {registration.Id} is disabled.");
        }

        var signingKeys = await GetPlatformSigningKeysAsync(registration);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = registration.Issuer,
            ValidateAudience = true,
            ValidAudience = registration.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidateLifetime = true,
            ClockSkew = ClockSkew,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
        };

        JwtSecurityToken jwt;
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(idToken, validationParameters, out var validated);
            jwt = (JwtSecurityToken)validated;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex,
                "LTI launch rejected: id_token failed validation for registration {RegistrationId}",
                registration.Id);
            throw new UnauthorizedAccessException("The LTI id_token could not be validated.", ex);
        }

        // Replay protection. The nonce lives inside the now-verified token, so this
        // also binds the token to the handshake we started.
        var nonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
        if (string.IsNullOrEmpty(nonce) || !await _nonces.TryConsumeAsync(nonce, state))
        {
            _logger.LogWarning(
                "LTI launch rejected: nonce missing, expired or already used (registration {RegistrationId})",
                registration.Id);
            throw new UnauthorizedAccessException("This LTI launch has already been used or has expired.");
        }

        var version = GetClaim(jwt, LtiClaims.Version);
        if (version != LtiVersion)
        {
            throw new UnauthorizedAccessException($"Unsupported LTI version '{version}'. Expected {LtiVersion}.");
        }

        var deploymentId = GetClaim(jwt, LtiClaims.DeploymentId);
        if (string.IsNullOrEmpty(deploymentId)
            || !await _registrations.HasActiveDeploymentAsync(registration.Id, deploymentId))
        {
            _logger.LogWarning(
                "LTI launch rejected: unknown deployment '{DeploymentId}' for registration {RegistrationId}",
                deploymentId, registration.Id);
            throw new UnauthorizedAccessException("This LTI deployment is not registered with Tutoria.");
        }

        var messageType = GetClaim(jwt, LtiClaims.MessageType)
            ?? throw new UnauthorizedAccessException("Launch is missing a message_type claim.");

        var roles = GetJsonArray(jwt, LtiClaims.Roles);
        var context = GetJsonObject(jwt, LtiClaims.Context);
        var resourceLink = GetJsonObject(jwt, LtiClaims.ResourceLink);
        var custom = GetCustomParameters(jwt);

        var result = new LtiLaunchResult
        {
            MessageType = messageType,
            Registration = registration,
            Subject = jwt.Subject ?? throw new UnauthorizedAccessException("Launch is missing sub."),
            Name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value,
            Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
            Roles = roles,
            IsStaff = roles.Any(r => LtiRoles.StaffRoles.Contains(r)),
            ContextId = ReadString(context, "id"),
            ContextTitle = ReadString(context, "title"),
            ContextLabel = ReadString(context, "label"),
            ResourceLinkId = ReadString(resourceLink, "id"),
            Custom = custom,
            TargetLinkUri = GetClaim(jwt, LtiClaims.TargetLinkUri) ?? pending.TargetLinkUri,
        };

        // Record (or refresh) the LMS course so an admin can link it to a Tutoria
        // course. Never invent a mapping here.
        if (!string.IsNullOrEmpty(result.ContextId))
        {
            result.ContextMapping = await _contextMappings.GetOrCreateAsync(
                registration.Id, result.ContextId, result.ContextTitle, result.ContextLabel);
        }

        if (result.IsDeepLinkingRequest)
        {
            var settings = GetJsonObject(jwt, LtiClaims.DeepLinkingSettings);
            result.DeepLinkingReturnUrl = ReadString(settings, "deep_link_return_url");
            result.DeepLinkingData = ReadString(settings, "data");
        }

        result.ModuleId = await ResolveAuthorisedModuleAsync(custom, registration);

        _logger.LogInformation(
            "LTI launch accepted: registration {RegistrationId}, university {UniversityId}, " +
            "type {MessageType}, context {ContextId}, module {ModuleId}, staff {IsStaff}",
            registration.Id, registration.UniversityId, messageType,
            result.ContextId, result.ModuleId, result.IsStaff);

        return result;
    }

    /// <summary>
    /// Reads the module id from the custom parameters and confirms it belongs to the
    /// launching institution. Returns null when absent; throws when it points
    /// somewhere the platform has no right to reach.
    /// </summary>
    private async Task<int?> ResolveAuthorisedModuleAsync(
        IReadOnlyDictionary<string, string> custom,
        LtiRegistration registration)
    {
        if (!custom.TryGetValue("module_id", out var raw) || !int.TryParse(raw, out var moduleId))
        {
            return null;
        }

        var module = await _modules.GetWithDetailsAsync(moduleId);
        if (module == null || !module.IsActive)
        {
            _logger.LogWarning("LTI launch referenced unknown or inactive module {ModuleId}", moduleId);
            throw new UnauthorizedAccessException("The module referenced by this launch does not exist.");
        }

        if (module.Course.UniversityId != registration.UniversityId)
        {
            // A cross-tenant reference: the LMS signed it, but this institution is
            // not entitled to that module.
            _logger.LogError(
                "LTI cross-tenant module access blocked: registration {RegistrationId} " +
                "(university {RegistrationUniversity}) requested module {ModuleId} " +
                "belonging to university {ModuleUniversity}",
                registration.Id, registration.UniversityId, moduleId, module.Course.UniversityId);
            throw new UnauthorizedAccessException("This module does not belong to the launching institution.");
        }

        return moduleId;
    }

    // ---------------------------------------------------------------------
    // Tool key set
    // ---------------------------------------------------------------------

    public async Task<object> GetPublicKeySetAsync()
    {
        var keys = (await _keys.GetPublishableAsync()).ToList();

        if (keys.Count == 0)
        {
            keys.Add(await GenerateSigningKeyAsync());
        }

        return new
        {
            keys = keys.Select(ToJwk).ToArray(),
        };
    }

    /// <summary>Creates and stores a fresh RSA key pair, and makes it the active one.</summary>
    private async Task<LtiToolKey> GenerateSigningKeyAsync()
    {
        using var rsa = RSA.Create(2048);

        var key = new LtiToolKey
        {
            Kid = GenerateSecureToken(16),
            PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem(),
            PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
            IsActive = true,
        };

        _logger.LogInformation("Generated new LTI tool signing key {Kid}", key.Kid);
        return await _keys.AddAsync(key);
    }

    private static object ToJwk(LtiToolKey key)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PublicKeyPem);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        return new
        {
            kty = "RSA",
            use = "sig",
            alg = "RS256",
            kid = key.Kid,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent),
        };
    }

    // ---------------------------------------------------------------------
    // Deep Linking response
    // ---------------------------------------------------------------------

    public async Task<string> BuildDeepLinkingResponseAsync(LtiLaunchResult launch, int moduleId, string? title)
    {
        if (!launch.IsDeepLinkingRequest)
        {
            throw new InvalidOperationException("This launch is not a Deep Linking request.");
        }

        // Re-run the tenant check: the module id here comes from our own picker, but
        // the guarantee should not depend on the caller having validated it.
        var module = await _modules.GetWithDetailsAsync(moduleId)
            ?? throw new KeyNotFoundException($"Module {moduleId} not found.");

        if (module.Course.UniversityId != launch.Registration.UniversityId)
        {
            throw new UnauthorizedAccessException("This module does not belong to the launching institution.");
        }

        var signingKey = await _keys.GetActiveAsync() ?? await GenerateSigningKeyAsync();

        using var rsa = RSA.Create();
        rsa.ImportFromPem(signingKey.PrivateKeyPem);

        // The RSA instance is disposed with this method, so let the key own a copy.
        var securityKey = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true))
        {
            KeyId = signingKey.Kid,
        };

        var contentItem = new Dictionary<string, object>
        {
            ["type"] = "ltiResourceLink",
            ["title"] = title ?? module.Name,
            ["url"] = GetLaunchUri(),
            // Echoed back on every subsequent resource launch, which is how we know
            // which module a placed link points at.
            ["custom"] = new Dictionary<string, string>
            {
                ["module_id"] = moduleId.ToString(),
            },
        };

        var now = DateTime.UtcNow;
        var payload = new JwtPayload
        {
            { "iss", launch.Registration.ClientId },
            { "aud", launch.Registration.Issuer },
            { "exp", EpochTime.GetIntDate(now.AddMinutes(5)) },
            { "iat", EpochTime.GetIntDate(now) },
            { "nonce", GenerateSecureToken(16) },
            { LtiClaims.MessageType, LtiMessageTypes.DeepLinkingResponse },
            { LtiClaims.Version, LtiVersion },
            { LtiClaims.DeploymentId, GetClaimFromLaunchDeployment(launch) },
            { LtiClaims.ContentItems, new[] { contentItem } },
        };

        if (!string.IsNullOrEmpty(launch.DeepLinkingData))
        {
            payload.Add("https://purl.imsglobal.org/spec/lti-dl/claim/data", launch.DeepLinkingData);
        }

        var header = new JwtHeader(new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    private static string GetClaimFromLaunchDeployment(LtiLaunchResult launch)
    {
        // The deployment was verified during ValidateLaunchAsync; a registration
        // always has at least one active deployment by that point.
        return launch.Registration.Deployments.FirstOrDefault(d => d.IsActive)?.DeploymentId
               ?? throw new InvalidOperationException("Registration has no active deployment.");
    }

    // ---------------------------------------------------------------------
    // Platform keys
    // ---------------------------------------------------------------------

    /// <summary>
    /// Fetches and caches the platform's signing keys. Cached per key set URL so a
    /// launch does not cost an outbound round-trip, while still picking up platform
    /// key rotation within the cache lifetime.
    /// </summary>
    private async Task<IEnumerable<SecurityKey>> GetPlatformSigningKeysAsync(LtiRegistration registration)
    {
        var cacheKey = $"lti:jwks:{registration.KeySetUrl}";

        if (_cache.TryGetValue<IEnumerable<SecurityKey>>(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(LtiService));
            client.Timeout = TimeSpan.FromSeconds(10);

            var json = await client.GetStringAsync(registration.KeySetUrl);
            var keys = new JsonWebKeySet(json).GetSigningKeys();

            _cache.Set(cacheKey, keys, PlatformKeyCacheLifetime);
            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to fetch LTI platform JWKS from {KeySetUrl} for registration {RegistrationId}",
                registration.KeySetUrl, registration.Id);
            throw new UnauthorizedAccessException("Could not retrieve the platform's signing keys.", ex);
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// The absolute URL of our launch endpoint, which must match the redirect_uri
    /// registered with every platform.
    /// </summary>
    private string GetLaunchUri()
    {
        if (string.IsNullOrWhiteSpace(_options.ToolBaseUrl))
        {
            throw new InvalidOperationException(
                "Lti:ToolBaseUrl is not configured — required to build the LTI redirect_uri.");
        }

        return $"{_options.ToolBaseUrl.TrimEnd('/')}/api/lti/launch";
    }

    private static string GenerateSecureToken(int bytes = 32)
    {
        return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(bytes));
    }

    private static string? GetClaim(JwtSecurityToken jwt, string type)
    {
        return jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
    }

    private static IReadOnlyList<string> GetJsonArray(JwtSecurityToken jwt, string type)
    {
        // A multi-valued claim arrives as repeated entries rather than one JSON array.
        var values = jwt.Claims.Where(c => c.Type == type).Select(c => c.Value).ToList();

        if (values.Count == 1 && values[0].TrimStart().StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(values[0]) ?? [];
            }
            catch (JsonException)
            {
                return values;
            }
        }

        return values;
    }

    private static JsonElement? GetJsonObject(JwtSecurityToken jwt, string type)
    {
        var raw = GetClaim(jwt, type);
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(raw).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> GetCustomParameters(JwtSecurityToken jwt)
    {
        var element = GetJsonObject(jwt, LtiClaims.Custom);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (element is not { ValueKind: JsonValueKind.Object })
        {
            return result;
        }

        foreach (var property in element.Value.EnumerateObject())
        {
            // Platforms may substitute numbers as well as strings.
            result[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.ToString();
        }

        return result;
    }

    private static string? ReadString(JsonElement? element, string property)
    {
        if (element is not { ValueKind: JsonValueKind.Object })
        {
            return null;
        }

        return element.Value.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
