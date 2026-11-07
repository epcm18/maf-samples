// AGT MCP trust guide, Layer 1 - "Managing Agents at Runtime". 13.2.1 was the gate; this is changing the
// gate while agents are running: a rate limit that fills up, a DID blocked mid-session, a tool policy
// tightened, and the audit log that recorded all of it. No restart, no rebuild of the agent.

using AgentGovernance.Trust;
using McpMock;
using McpTrustProxy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using Shared;

Out.Init("13.2.2 - MCP Trust Proxy, runtime control");

await using McpClient mcp = await JsonMcpServer.ConnectAsync("servers/clinic");
IList<McpClientTool> mcpTools = await mcp.ListToolsAsync();

Caller doctor = new(AgentIdentity.Create("Doctor", capabilities: ["read", "prescribe"]), TrustScore: 850);

TrustProxy proxy = new(
    defaultMinTrust: 300,
    toolPolicies: new()
    {
        ["get_medical_report"] = new ToolPolicy(MaxCallsPerMinute: 2)
    });

// One agent for the whole run - every change below lands on it without rebuilding it.
AIAgent agent = CreateAgent(doctor);

Out.Step("1. Rate limit - 2 reports a minute, the third call is refused. Only allowed calls use up the allowance");
for (int i = 1; i <= 3; i++)
{
    await Ask(agent, "Read the Male Ward medical report.");
}

Out.Step("2. Block mid-session - say the doctor's key leaked. Same agent, same tools, now refused");
proxy.BlockAgent(doctor.Identity.Did);
await Ask(agent, "Which ward is John Smith in?");

proxy.UnblockAgent(doctor.Identity.Did);
Out.Note($"  unblocked {doctor.Identity.Did}");
await Ask(agent, "Which ward is John Smith in?");

Out.Step("3. Tighten a policy - get_patient_ward now needs trust 900, the doctor has 850");
proxy.SetToolPolicy("get_patient_ward", new ToolPolicy(MinTrust: 900));
await Ask(agent, "Which ward is Mary Perera in?");

Out.Step("Audit log - every decision the proxy took, in order");
foreach (AuthResult entry in proxy.AuditLog)
{
    Console.WriteLine($"  {entry.Timestamp:HH:mm:ss.fff} {(entry.Allowed ? "allow" : "deny ")} {entry.ToolName,-20} trust {entry.TrustScore}  {entry.Reason}");
}

(int total, int allowed, int denied) = proxy.Stats;
Out.Note($"{total} decisions, {allowed} allowed, {denied} denied");
return;

AIAgent CreateAgent(Caller caller) => Clients.Chat()
    .AsAIAgent(
        name: caller.Identity.Name,
        instructions: $"You are the clinic's {caller.Identity.Name}. Use the clinic tools. " +
                      "If a tool call is denied, say so and why - do not retry it.",
        tools: [.. mcpTools])
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
