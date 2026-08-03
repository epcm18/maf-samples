using Microsoft.Extensions.Configuration;

namespace Shared;

public record Secrets(string OpenAiApiKey);

/// <summary>
/// Reads .NET user-secrets. Set them once from the repo root:
/// dotnet user-secrets set "OpenAiApiKey" "sk-..." --project src/Shared
/// </summary>
public static class SecretsManager
{
    public static Secrets Get()
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddUserSecrets<Secrets>()
            .Build();

        string apiKey = config["OpenAiApiKey"]
                        ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                        ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "No 'OpenAiApiKey' found. Run: dotnet user-secrets set \"OpenAiApiKey\" \"sk-...\" --project src/Shared");
        }

        return new Secrets(apiKey);
    }
}
