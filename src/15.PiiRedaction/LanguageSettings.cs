using Microsoft.Extensions.Configuration;
using Shared;

namespace PiiRedaction;

/// <summary>
/// Azure Language settings for this sample. Read from appsettings.json, then from the shared
/// user-secrets store, so the key can stay out of git:
/// dotnet user-secrets set "AzureLanguage:ApiKey" "..." --project src/Shared
/// </summary>
public record LanguageSettings(string Endpoint, string ApiKey, string ApiVersion)
{
    /// <summary>The Azure Language MCP server - the same one Foundry adds from its tool catalog.</summary>
    public Uri McpEndpoint => new($"{Endpoint.TrimEnd('/')}/language/mcp?api-version={ApiVersion}");

    /// <summary>Azure Language takes the key in this header, not as a bearer token.</summary>
    public const string KeyHeader = "Ocp-Apim-Subscription-Key";

    public static LanguageSettings Load()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddUserSecrets<Secrets>()
            .Build();

        IConfigurationSection section = config.GetSection("AzureLanguage");
        string endpoint = section["Endpoint"] ?? string.Empty;
        string apiKey = section["ApiKey"] ?? string.Empty;
        string apiVersion = section["ApiVersion"] ?? "2025-11-15-preview";

        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("your-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Set AzureLanguage:Endpoint in appsettings.json to https://{your-foundry-resource}.cognitiveservices.azure.com");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "No AzureLanguage:ApiKey. Put the key from your Foundry project home page in appsettings.json, or run: " +
                "dotnet user-secrets set \"AzureLanguage:ApiKey\" \"...\" --project src/Shared");
        }

        return new LanguageSettings(endpoint, apiKey, apiVersion);
    }
}
