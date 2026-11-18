using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PiiRedaction;

/// <summary>
/// Calls the Azure Language MCP server from our own process - no LLM involved.
/// Used by the MaskToolOutput case, where the redaction must happen before the model sees anything.
/// </summary>
public sealed class AzureLanguageMcp(McpClient client) : IAsyncDisposable
{
    public const string RedactTool = "redact_pii_from_text";

    public static async Task<AzureLanguageMcp> ConnectAsync(LanguageSettings settings)
    {
        HttpClientTransport transport = new(new HttpClientTransportOptions
        {
            Name = "azure-language",
            Endpoint = settings.McpEndpoint,
            AdditionalHeaders = new Dictionary<string, string> { [LanguageSettings.KeyHeader] = settings.ApiKey }
        });

        return new AzureLanguageMcp(await McpClient.CreateAsync(transport));
    }

    /// <summary>
    /// Masks PII with '*'. Fails closed: if Azure's answer can't be read, the caller gets an
    /// exception, never the original text.
    /// </summary>
    public async Task<RedactionResult> RedactAsync(string text, CancellationToken cancellationToken = default)
    {
        CallToolResult result = await client.CallToolAsync(
            RedactTool,
            new Dictionary<string, object?> { ["message"] = text, ["language"] = "en" },
            cancellationToken: cancellationToken);

        string json = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"{RedactTool} failed: {json}");
        }

        // Shape (seen in the Foundry lab): [{ "redactedText": "...", "entities": [{ "text", "category", ... }] }]
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement first = doc.RootElement[0];

        string redacted = first.GetProperty("redactedText").GetString()
                          ?? throw new InvalidOperationException($"{RedactTool} returned no redactedText.");

        List<string> categories = first.TryGetProperty("entities", out JsonElement entities)
            ? entities.EnumerateArray().Select(e => e.GetProperty("category").GetString() ?? "?").ToList()
            : [];

        return new RedactionResult(redacted, categories);
    }

    public ValueTask DisposeAsync() => client.DisposeAsync();
}

public record RedactionResult(string RedactedText, IReadOnlyList<string> Categories);
