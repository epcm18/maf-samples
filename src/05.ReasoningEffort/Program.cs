// Reasoning models reason by default (medium). You override it through ChatOptions.RawRepresentationFactory,
// which is the escape hatch for provider-specific settings the abstraction does not model.
//
// ChatCompletions   -> ChatCompletionOptions.ReasoningEffortLevel   (no reasoning summary available)
// Responses API     -> ResponseReasoningOptions                     (summary available)
//
// Watch the reasoning token count in the output - that is what the setting actually buys or saves.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Responses;
using Shared;

Out.Init("05 - Reasoning Effort");

const string question =
    "A farmer has chickens and cows, 30 heads and 74 legs in total. How many of each? Answer in one sentence.";

await ChatCompletionsAsync("default (medium, not set by us)", null);
await ChatCompletionsAsync("minimal", ChatReasoningEffortLevel.Minimal); // almost no reasoning at all.
await ChatCompletionsAsync("high", ChatReasoningEffortLevel.High);
await ResponsesWithSummaryAsync();

return;

async Task ChatCompletionsAsync(string label, ChatReasoningEffortLevel? effort)
{
    Out.Step($"Chat Completions - reasoning: {label}");

    ChatClientAgentOptions options = new()
    {
        ChatOptions = new ChatOptions
        {
            RawRepresentationFactory = _ => effort is null
                ? new ChatCompletionOptions()
                : new ChatCompletionOptions { ReasoningEffortLevel = effort }
        }
    };

    AIAgent agent = Clients.Chat(Models.Reasoning).AsAIAgent(options);

    AgentResponse response = await agent.RunAsync(question);
    Console.WriteLine(response);
    response.Usage.Print();
    Out.Separator();
}

// Only the Responses API gives you the reasoning summary back.
async Task ResponsesWithSummaryAsync()
{
    Out.Step("Responses API - reasoning: high, with summary");

    AIAgent agent = Clients.Responses().AsAIAgent(
        model: Models.Reasoning,
        options: new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                RawRepresentationFactory = _ => new CreateResponseOptions
                {
                    ReasoningOptions = new ResponseReasoningOptions
                    {
                        ReasoningEffortLevel = ResponseReasoningEffortLevel.High,
                        ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Detailed
                    }
                }
            }
        });

    AgentResponse response = await agent.RunAsync(question);

    foreach (TextReasoningContent reasoning in response.Messages
                 .SelectMany(m => m.Contents)
                 .OfType<TextReasoningContent>())
    {
        Out.Warn("Reasoning summary:");
        Out.Note(reasoning.Text);
    }

    Console.WriteLine(response);
    response.Usage.Print();
}
