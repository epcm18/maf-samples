// Two places to apply the same trust rules. 13.2.1 checks at call time: the model sees every tool,
// tries one, the proxy refuses, the model explains. This sample also filters up front: the model is
// only handed the tools the caller may use, so it never tries. Not in the guide - its authorize() is
// call-time only - but the question comes up as soon as MCP tools go into an agent.
//
// Same nurse, same request, both ways. Then why the call-time check stays even with filtering.

using AgentGovernance.Trust;
using McpMock;
using McpTrustProxy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using Shared;

Out.Init("13.2.3 - MCP tools: filter up front vs deny at call time");

await using McpClient mcp = await JsonMcpServer.ConnectAsync("servers/clinic");
IList<McpClientTool> mcpTools = await mcp.ListToolsAsync();

Caller nurse = new(AgentIdentity.Create("Nurse", capabilities: ["read"]), TrustScore: 850);

TrustProxy proxy = new(
    defaultMinTrust: 300,
    toolPolicies: new()
    {
        ["get_patient_ward"] = new ToolPolicy(RequiredCapabilities: ["read"]),
        ["get_medical_report"] = new ToolPolicy(RequiredCapabilities: ["read"]),
        ["prescribe_medication"] = new ToolPolicy(MinTrust: 800, RequiredCapabilities: ["prescribe"]),
        ["discharge_patient"] = new ToolPolicy(Blocked: true)
    });

const string request = "Mary Perera has a headache. Prescribe her paracetamol, 500 mg twice a day.";

Out.Step("1. Deny at call time - all four tools, the proxy refuses the one the model picks");
await Ask(CreateAgent(nurse, mcpTools), request);

Out.Step("2. Filter up front - the nurse only gets the tools the proxy permits. No call, no denial");
IList<McpClientTool> nurseTools = [.. mcpTools.Where(t => proxy.Permits(nurse.Identity, nurse.TrustScore, t.Name))];
Out.Note($"  tools handed to the nurse: {string.Join(", ", nurseTools.Select(t => t.Name))}");
await Ask(CreateAgent(nurse, nurseTools), request);

Out.Step("3. Both - filtering sets the menu once, the proxy still judges every call");
Out.Note("  The filter is a snapshot from when the agent was built. A block, a policy change or a rate limit after that, only the call-time check sees.");
AIAgent agent = CreateAgent(nurse, nurseTools);
await Ask(agent, "Which ward is Mary Perera in?");

proxy.BlockAgent(nurse.Identity.Did);
Out.Warn($"  blocked {nurse.Identity.Did} after the agent was built - its tool list is unchanged");
await Ask(agent, "Which ward is John Smith in?");

return;

AIAgent CreateAgent(Caller caller, IList<McpClientTool> tools) => Clients.Chat()
    .AsAIAgent(
        name: caller.Identity.Name,
        instructions: $"You are the clinic's {caller.Identity.Name}. Use the clinic tools. " +
                      "If you have no tool for a request, or a tool call is denied, say so and why.",
        tools: [.. tools])
    .AsBuilder()
    .Use(ToolLogging.LogToolCalls)
    .Use((_, context, next, ct) => AuthorizeThenCall(caller, context, next, ct))
    .Build();

async ValueTask<object?> AuthorizeThenCall(
    Caller caller,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    AuthResult result = proxy.Authorize(caller.Identity, caller.TrustScore, context.Function.Name);

    string line = $"  [proxy] {(result.Allowed ? "allow" : "deny ")} {result.ToolName} by {caller.Identity.Name} - {result.Reason}";
    if (result.Allowed)
    {
        Out.Note(line);
        return await next(context, cancellationToken);
    }

    Out.Warn(line);
    return $"Denied by the trust proxy: {result.Reason}.";
}

async Task Ask(AIAgent agent, string prompt)
{
    Console.WriteLine($"{agent.Name}> {prompt}");
    AgentResponse response = await agent.RunAsync(prompt);
    Console.WriteLine(response.Text);
    Out.Separator();
}

internal sealed record Caller(AgentIdentity Identity, int TrustScore);
