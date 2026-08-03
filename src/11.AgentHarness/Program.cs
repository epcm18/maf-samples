// The Agent Harness - this is the "build your own Copilot" piece.
//
// AsHarnessAgent() gives you, out of the box, the things a coding agent has:
//   - a todo list the model maintains itself      (TodoProvider)
//   - plan / execute modes                        (AgentModeProvider)
//   - file-based memory across turns              (FileMemoryProvider)
//   - background subagents it can delegate to     (BackgroundAgents)
//   - context-window compaction when it fills up  (CompactionStrategy)
//   - tool approval flow + OpenTelemetry
//
// You mostly configure it by *disabling* things, which tells you how much is on by default.

using AgentHarness;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Shared;

Out.Init("11 - Agent Harness");

// A subagent. It needs a Name - that is how the harness exposes it for delegation.
AIAgent researchAgent = Clients.Chat(Models.Cheap)
    .AsAIAgent(
        name: "failure-analyst",
        description: "Looks up the failure history of a single asset and summarises the risk in two sentences.",
        instructions: "You analyse maintenance failure history. Be brief and concrete.",
        tools: [AIFunctionFactory.Create(FleetTools.GetLastFailure)]);

HarnessAgentOptions options = new()
{
    Name = "MaintenancePlanner",

    ChatOptions = new ChatOptions
    {
        Instructions =
            "You plan maintenance for an industrial fleet. " +
            "Start by writing a todo list, then work through it. " +
            "Delegate per-asset failure analysis to the failure-analyst background agent.",
        Tools =
        [
            AIFunctionFactory.Create(FleetTools.ListFleet),
            AIFunctionFactory.Create(FleetTools.GetServiceInterval)
        ]
    },

    // Subagents the harness can spin up on the fly.
    BackgroundAgents = [researchAgent],

    // Compaction needs both numbers to build a default strategy.
    MaxContextWindowTokens = 128_000,
    MaxOutputTokens = 16_000,

    // Hosted web search needs the Responses API; this sample uses Chat Completions.
    DisableWebSearch = true,

    // Writes to {cwd}/agent-file-memory/... - turn it on to see the agent keep notes between turns.
    DisableFileMemory = true,

    // Left ON so you can watch them work:
    //   DisableTodoProvider      - the model's own todo list
    //   DisableAgentModeProvider - "plan" and "execute" modes

    MaximumIterationsPerRequest = 20
};

AIAgent harness = Clients.Chat(Models.Standard)
    .AsIChatClient()
    .AsHarnessAgent(options)
    .AsBuilder()
    .Use(ToolLogging.LogToolCalls)
    .Build();

Out.Note("Harness default instructions (truncated):");
Out.Note(HarnessAgent.DefaultInstructions[..Math.Min(600, HarnessAgent.DefaultInstructions.Length)] + "...");
Out.Separator();

Out.Step("Task: plan maintenance across the fleet");

AgentSession session = await harness.CreateSessionAsync();

AgentResponse response = await harness.RunAsync(
    "Review the whole fleet. For every asset that is past or close to its service interval, " +
    "get the failure history through the failure-analyst agent, then give me a prioritised " +
    "maintenance plan with a one-line justification per asset.",
    session);

Console.WriteLine(response);
response.Usage.Print();

Out.Separator();
Out.Note("Now ask a follow-up and watch it reuse the session state.");
await ChatLoop.RunAsync(harness);
