// HostedWebSearchTool: search runs on OpenAI's side, not yours - no tool implementation to write.
//
// Two things to notice:
//  1. GetResponsesClient(), not GetChatClient(). Hosted tools only exist on the Responses API.
//  2. The model name moves into AsAIAgent(model: ...) instead of GetChatClient("model").
//
// Billed on top of tokens (~$10 per 1k calls). Not available on Azure OpenAI.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using Shared;

Out.Init("03 - Hosted Web Search");

AIAgent agent = Clients.Responses()
    .AsAIAgent(
        model: Models.Standard,
        instructions: "You report on current events. Always state the publication date of what you cite.",
        tools: [new HostedWebSearchTool()]);

//const string question = "What happened in .NET / C# tooling news in the last week? Give me 3 bullets with sources.";
//const string question = "Who's the new manager in france national football team?";

const string question = "Is there a medium author named Pasindu Madusha who writes articles on tech stuffs and how many articles does he have?"; // even this worked really well; impressive
Out.Step($"Q: {question}");

List<AgentResponseUpdate> updates = [];
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(question))
{
    updates.Add(update);
    Console.Write(update);
}

Console.WriteLine();
Out.Separator();

AgentResponse full = updates.ToAgentResponse();
full.Usage.Print();

// Citations come back as annotations on the response content.
foreach (CitationAnnotation citation in full.Messages
             .SelectMany(m => m.Contents)
             .SelectMany(c => c.Annotations ?? [])
             .OfType<CitationAnnotation>())
{
    Out.Note($"citation: {citation.Title} - {citation.Url}");
}
