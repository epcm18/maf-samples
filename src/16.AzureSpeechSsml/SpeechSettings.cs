using Microsoft.Extensions.Configuration;
using Shared;

namespace AzureSpeechSsml;

/// <summary>
/// Azure Speech settings for this sample. Read from appsettings.json, then from the shared
/// user-secrets store, so the key can stay out of git:
/// dotnet user-secrets set "AzureSpeech:ApiKey" "..." --project src/Shared
/// </summary>
public record SpeechSettings(string Endpoint, string ApiKey, string OutputFolder)
{
    public static SpeechSettings Load()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .AddUserSecrets<Secrets>()
            .Build();

        IConfigurationSection section = config.GetSection("AzureSpeech");
        string endpoint = section["Endpoint"] ?? string.Empty;
        string apiKey = section["ApiKey"] ?? string.Empty;
        string outputFolder = section["OutputFolder"] ?? "output";

        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("<your-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Set AzureSpeech:Endpoint in appsettings.json to https://{your-foundry-resource}.cognitiveservices.azure.com");
        }

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("<your-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "No AzureSpeech:ApiKey. Put the key from your Foundry project home page in appsettings.json, or run: " +
                "dotnet user-secrets set \"AzureSpeech:ApiKey\" \"...\" --project src/Shared");
        }

        return new SpeechSettings(endpoint, apiKey, outputFolder);
    }
}
