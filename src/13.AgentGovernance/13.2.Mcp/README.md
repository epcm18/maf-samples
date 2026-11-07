# 13.2 - AGT and MCP

AGT's [MCP trust guide](https://github.com/microsoft/agent-governance-toolkit/blob/main/docs/integrations/mcp-trust-guide.md)
is written for Python. Each 13.2.x sample takes one layer of it and does it in .NET on MAF, using the
AGT .NET types where they exist (Microsoft.AgentGovernance 5.0.0) and a small port where they do not.

| Sample | Guide layer |
|--------|-------------|
| [13.2.1](13.2.1.TrustProxy) | Layer 1 - trust proxy: a check in front of every MCP tool call, decided by who is calling |
| [13.2.2](13.2.2.RuntimeControl) | Layer 1 - managing agents at runtime: rate limit, block a DID mid-session, change a policy, audit log |
| [13.2.3](13.2.3.ToolFiltering) | Not in the guide - filter the tool list up front vs deny at call time, and why you keep both |

`TrustProxy.cs` in 13.2.1 is the C# port of the guide's Python class (.NET AGT 5.0.0 has none), built on
AGT's `AgentIdentity` and `RateLimiter`. 13.2.2 and 13.2.3 link that one file rather than copy it.

## McpMock - the clinic MCP server, shared by every 13.2.x

Each sample is a normal MCP client: `McpClient` -> `ListToolsAsync()` -> `McpClientTool`s into the agent,
with real `tools/list` and `tools/call` JSON-RPC underneath. Only the server is fake: it runs in-process
over in-memory pipes and answers from a folder of JSON, so there is no second process, port or URL.
The folder name is the server name.

```
servers/<name>/tools.json              a tools/list result, as-is: { "tools": [ { name, description, inputSchema } ] }
servers/<name>/responses/<tool>.json   [ { "when": { "arg": "value" }, "result": ... }, { "result": ... } ]
```

```csharp
await using McpClient client = await JsonMcpServer.ConnectAsync("servers/clinic");
IList<McpClientTool> tools = await client.ListToolsAsync();   // straight into AsAIAgent(tools: ...)
```

A `tools/call` is answered by the first canned response whose `when` matches the arguments; `when` left
out is the fallback. No match is an `isError` result, so a missing mock shows up in the run.

`servers/clinic` ships with McpMock and lands in every sample's output: the 13.1 clinic tools plus a
write tool (`prescribe_medication`) and an irreversible one (`discharge_patient`).
