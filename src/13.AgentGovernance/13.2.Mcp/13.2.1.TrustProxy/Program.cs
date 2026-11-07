// AGT MCP trust guide, Layer 1 - the Trust Proxy: an authorization check in front of every MCP tool call.
// 13.1 decided by what the arguments said. This one decides by WHO is calling: DID, trust score, capabilities.
// A denied call never reaches the MCP server; the model gets the reason back as the tool result.
//
// The tools come from the clinic MCP server (McpMock) through a normal McpClient. TrustProxy.cs is a
// C# port of the guide's Python class - .NET AGT 5.0.0 has no TrustProxy of its own.

using AgentGovernance.Trust;
using McpMock;
using McpTrustProxy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI.Chat;
using Shared;

Out.Init("13.2.1 - MCP Trust Proxy");

await using McpClient mcp = await JsonMcpServer.ConnectAsync("servers/clinic");
IList<McpClientTool> mcpTools = await mcp.ListToolsAsync();
Out.Note($"{mcp.ServerInfo.Name} MCP server: {string.Join(", ", mcpTools.Select(t => t.Name))}");

// Who calls. The trust score is a fixed number here
Caller doctor = new(AgentIdentity.Create("Doctor", capabilities: ["read", "prescribe"]), TrustScore: 850);
Caller nurse = new(AgentIdentity.Create("Nurse", capabilities: ["read"]), TrustScore: 850);

// The rules. A capability is a label on the identity, not a tool name: a tool lists the labels a caller
// must hold. A tool with no policy only needs the default trust of 300.
TrustProxy proxy = new(
    defaultMinTrust: 300,
    toolPolicies: new()
    {
        ["get_patient_ward"] = new ToolPolicy(RequiredCapabilities: ["read"]),
        ["get_medical_report"] = new ToolPolicy(RequiredCapabilities: ["read"]),
        ["prescribe_medication"] = new ToolPolicy(MinTrust: 800, RequiredCapabilities: ["prescribe"]),
        ["discharge_patient"] = new ToolPolicy(Blocked: true, Description: "irreversible - humans only")
    });

Out.Step("1. Allow - the doctor has the trust and the capability to prescribe");
await Ask(CreateAgent(doctor), "Mary Perera has a headache. Prescribe her paracetamol, 500 mg twice a day.");

Out.Step("2. Deny by who - same request, the nurse: trusted enough, holds 'read' but not 'prescribe'");
await Ask(CreateAgent(nurse), "Mary Perera has a headache. Prescribe her paracetamol, 500 mg twice a day.");

Out.Step("3. Deny by tool - discharge is blocked for everyone, whoever asks");
await Ask(CreateAgent(doctor), "Discharge John Smith, he is fine now.");

(int total, int allowed, int denied) = proxy.Stats;
Out.Note($"Audit: {total} decisions, {allowed} allowed, {denied} denied. The other checks - trust threshold, " +
         "blocked DIDs, rate limit - are in 13.2.2.");
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

// The guide's "run each call through authorize() before forwarding it to the MCP server", as MAF middleware.
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
