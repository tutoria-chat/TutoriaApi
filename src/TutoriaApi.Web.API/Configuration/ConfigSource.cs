using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

namespace TutoriaApi.Web.API.Configuration;

public enum ConfigSourceKind { Local, AwsSsm, AzureKeyVault }

/// <summary>
/// Where deployed settings come from, picked by environment variable so one image
/// runs on both clouds:
///   AWS_SSM_PREFIX      → AWS SSM Parameter Store (ECS).
///   AZURE_KEYVAULT_URL  → Azure Key Vault (Container Apps); "Jwt--Secret" is Jwt:Secret.
///   neither             → appsettings only (local runs).
/// Both at once is a misconfiguration and refuses to start: on Azure nothing may try SSM.
/// Either remote source must load at startup (a missing vault or prefix is fatal) and
/// is re-read every <see cref="ReloadInterval"/>.
/// </summary>
public static class ConfigSource
{
    public const string SsmPrefixVariable = "AWS_SSM_PREFIX";
    public const string KeyVaultUrlVariable = "AZURE_KEYVAULT_URL";
    /// <summary>The user-assigned managed identity's client id (optional).</summary>
    public const string ClientIdVariable = "AZURE_CLIENT_ID";

    public static readonly TimeSpan ReloadInterval = TimeSpan.FromMinutes(5);

    public static ConfigSourceKind Select(string? ssmPrefix, string? keyVaultUrl)
    {
        var ssm = !string.IsNullOrWhiteSpace(ssmPrefix);
        var keyVault = !string.IsNullOrWhiteSpace(keyVaultUrl);
        if (ssm && keyVault)
            throw new InvalidOperationException(
                $"Both {SsmPrefixVariable} and {KeyVaultUrlVariable} are set: set only one " +
                $"({SsmPrefixVariable} on AWS, {KeyVaultUrlVariable} on Azure).");
        return ssm ? ConfigSourceKind.AwsSsm : keyVault ? ConfigSourceKind.AzureKeyVault : ConfigSourceKind.Local;
    }

    /// <summary>Adds the chosen source to <paramref name="configuration"/>. Returns the line to log (no values).</summary>
    public static string Apply(IConfigurationBuilder configuration, Func<string, string?> environment)
    {
        var ssmPrefix = environment(SsmPrefixVariable);
        var keyVaultUrl = environment(KeyVaultUrlVariable);
        switch (Select(ssmPrefix, keyVaultUrl))
        {
            case ConfigSourceKind.AwsSsm:
                var ssmRegion = environment("AWS_REGION") ?? "us-east-2";
                configuration.AddSystemsManager(configSource =>
                {
                    configSource.Path = ssmPrefix;
                    configSource.Optional = false;
                    configSource.ReloadAfter = ReloadInterval;
                    configSource.AwsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions
                    {
                        Region = Amazon.RegionEndpoint.GetBySystemName(ssmRegion)
                    };
                });
                return $"[Config] Loaded from AWS SSM: {ssmPrefix}";

            case ConfigSourceKind.AzureKeyVault:
                if (!Uri.TryCreate(keyVaultUrl!.Trim(), UriKind.Absolute, out var vaultUri) || vaultUri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException($"{KeyVaultUrlVariable} must be the vault's https URL, e.g. https://<name>.vault.azure.net/");
                // The provider reads every secret at startup and throws if it can't
                // (same as SSM's Optional = false).
                configuration.AddAzureKeyVault(vaultUri, KeyVaultCredential(environment), new AzureKeyVaultConfigurationOptions
                {
                    Manager = new KeyVaultSecretManager(),
                    ReloadInterval = ReloadInterval,
                });
                return $"[Config] Loaded from Azure Key Vault: {vaultUri.Host}";

            default:
                return $"[Config] Neither {SsmPrefixVariable} nor {KeyVaultUrlVariable} set — using local appsettings";
        }
    }

    /// <summary>DefaultAzureCredential, pinned to the user-assigned identity when AZURE_CLIENT_ID is set.</summary>
    internal static DefaultAzureCredential KeyVaultCredential(Func<string, string?> environment)
    {
        var options = new DefaultAzureCredentialOptions();
        if (environment(ClientIdVariable) is { Length: > 0 } clientId)
            options.ManagedIdentityClientId = clientId;
        return new DefaultAzureCredential(options);
    }
}
