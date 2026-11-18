using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using Shared;

namespace PiiRedaction.Cases;

/// <summary>
/// Case 1: the agent gets the Azure Language MCP server as a hosted tool and redacts the text
/// the user pastes in.
///
/// Be clear about what this protects: the raw text is still sent to OpenAI (it's the prompt).
/// This is redaction for what you show, store or forward afterwards - not hiding data from
/// the model.
/// </summary>
public static class RedactUserInput
{
    public static async Task RunAsync(LanguageSettings settings, string input)
    {
        HostedMcpServerTool azureLanguage = new("azure_language", settings.McpEndpoint)
        {
            Headers = new Dictionary<string, string> { [LanguageSettings.KeyHeader] = settings.ApiKey },
            ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire,

            // The server has 11 tools (sentiment, summarise, healthcare entities, ...). Without this,
            // every schema goes into the prompt on every model pass - ~2-3k extra input tokens.
            AllowedTools = [AzureLanguageMcp.RedactTool]
        };

        AIAgent agent = Clients.Responses()
            .AsAIAgent(
                model: Models.Cheap,
                name: "RedactionAgent",
                // some enforcement due to the model doesn't know the tool's schema, and the model may try to return the original values instead of the redacted text
                instructions: "You redact personal information. Always pass the user's full text to the " +
                              $"{AzureLanguageMcp.RedactTool} tool as the message argument, and set no other " +
                              "arguments - the defaults are correct. Reply with the redacted text exactly " +
                              "as the tool returned it, followed by a short list of the kinds of information " +
                              "that were removed. Never repeat the original values.",
                tools: [azureLanguage]);

        Out.Step("Input (sent to OpenAI as-is)");
        Out.Note(input);
        Out.Separator();

        AgentResponse response = await agent.RunAsync(input);

        Out.Step("Agent output");
        Console.WriteLine(response);
        Out.Separator();

        PrintMcpTraffic(response);
        response.Usage.Print();
    }

    // The hosted MCP call and its result come back as content items on the response.
    private static void PrintMcpTraffic(AgentResponse response)
    {
        foreach (AIContent content in response.Messages.SelectMany(m => m.Contents))
        {
            switch (content)
            {
                case McpServerToolCallContent call:
                    Out.Note($"  -> {call.ServerName}.{call.Name} called by OpenAI with " +
                             string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>())
                                 .Select(a => $"{a.Key}={Truncate(a.Value?.ToString() ?? "null", 60)}")));
                    break;
                case McpServerToolResultContent result:
                    foreach (AIContent output in result.Outputs ?? [])
                    {
                        switch (output)
                        {
                            case TextContent text:
                                Out.Note($"  <- Azure Language returned: {Truncate(text.Text)}");
                                break;
                            case ErrorContent error:
                                // The model gets this too, and may quietly redact by itself instead - check here
                                // before trusting the output.
                                Out.Warn($"  <- MCP call FAILED: {error.Message} {error.Details}");
                                break;
                            default:
                                Out.Note($"  <- {output.GetType().Name}");
                                break;
                        }
                    }

                    break;
            }
        }
    }

    private static string Truncate(string value, int max = 300)
    {
        value = value.ReplaceLineEndings(" ");
        return value.Length <= max ? value : value[..max] + "...";
    }
}
