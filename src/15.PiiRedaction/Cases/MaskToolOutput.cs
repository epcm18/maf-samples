using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Shared;

namespace PiiRedaction.Cases;

/// <summary>
/// Case 2: a local tool returns a customer record full of personal details. Tool middleware
/// sends the result through Azure Language and swaps in the masked text before the model
/// sees it. The model never receives the real name, email, phone or address.
///
/// This is sample 01's "rewrite" middleware, with Azure Language deciding what to hide
/// instead of a hard-coded rule. Our own process calls the MCP server here (McpClient),
/// because OpenAI must not be the one doing the masking.
///
/// It works because the PII exists only inside the tool. Anything the user types in the
/// prompt still reaches the model - the prompt here only carries a customer id.
/// </summary>
public static class MaskToolOutput
{
    public static async Task RunAsync(LanguageSettings settings, string question)
    {
        await using AzureLanguageMcp language = await AzureLanguageMcp.ConnectAsync(settings);

        AIAgent agent = Clients.Chat()
            .AsAIAgent(
                name: "SupportAgent",
                instructions: "You are a customer support assistant. Use the tools to look up customers. " +
                              "Personal details in tool results are masked with '*'. Never guess or " +
                              "reconstruct them; refer to the person as 'the customer'.",
                tools: [AIFunctionFactory.Create(GetCustomerProfile)])
            .AsBuilder()
            .Use(MaskPiiMiddleware)
            .Build();

        Out.Step($"Q: {question}");
        AgentResponse response = await agent.RunAsync(question);

        Out.Separator();
        Out.Step("Agent output");
        Console.WriteLine(response);
        response.Usage.Print();
        return;

        // Every tool result goes through Azure Language before it goes back to the model.
        async ValueTask<object?> MaskPiiMiddleware(
            AIAgent callingAgent,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
            CancellationToken cancellationToken)
        {
            object? result = await next(context, cancellationToken);
            string raw = result?.ToString() ?? string.Empty;
            if (raw.Length == 0)
            {
                return result;
            }

            RedactionResult redacted = await language.RedactAsync(raw, cancellationToken);

            Out.Warn($"  [mask] {context.Function.Name} returned (stays in this process):");
            Out.Note(Indent(raw));
            Out.Warn($"  [mask] sent to the model instead ({string.Join(", ", redacted.Categories.Distinct())} masked):");
            Out.Note(Indent(redacted.RedactedText));

            return redacted.RedactedText;
        }
    }

    [Description("Gets a customer's profile and recent order by customer id.")]
    private static string GetCustomerProfile(string customerId) => customerId switch
    {
        "C-1001" => """
                    Customer: Amelia Fernando
                    Email: amelia.fernando@example.com
                    Phone: +44 20 7946 0958
                    Address: 221 Baker Street, London NW1 6XE
                    Date of birth: 14 March 1988
                    Plan: Premium (since 2021)
                    Last order: #48213, wireless headphones, shipped 3 November, status: delayed at courier depot
                    Open tickets: 0
                    """,
        _ => $"No customer found with id {customerId}."
    };

    private static string Indent(string text) =>
        string.Join(Environment.NewLine, text.ReplaceLineEndings("\n").Split('\n').Select(l => "      " + l));
}
