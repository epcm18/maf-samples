// A real MCP server whose tools come from JSON, run in-process over in-memory pipes. The sample talks
// to it through a normal McpClient - tools/list and tools/call are real JSON-RPC - but there is no
// second process, port or URL to manage. The folder name is the server name.
//
//   servers/<name>/tools.json                 a tools/list result, as-is: { "tools": [ { name, description, inputSchema } ] }
//   servers/<name>/responses/<tool>.json      [ { "when": { "arg": "value" }, "result": ... }, { "result": ... } ]

using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpMock;

public static class JsonMcpServer
{
    /// <summary>Starts the server for a folder and returns a client connected to it. Disposing the client stops the server.</summary>
    public static async Task<McpClient> ConnectAsync(string folder)
    {
        string root = Path.GetFullPath(folder, AppContext.BaseDirectory);
        string name = new DirectoryInfo(root).Name;

        ListToolsResult tools = JsonSerializer.Deserialize<ListToolsResult>(
                                    File.ReadAllText(Path.Combine(root, "tools.json")), McpJsonUtilities.DefaultOptions)
                                ?? throw new InvalidDataException($"tools.json in {root} has no tools.");
        Dictionary<string, List<CannedResponse>> responses = LoadResponses(Path.Combine(root, "responses"));

        Pipe clientToServer = new();
        Pipe serverToClient = new();

        McpServer server = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream(), name),
            new McpServerOptions
            {
                ServerInfo = new Implementation { Name = name, Version = "1.0.0" },
                Capabilities = new ServerCapabilities { Tools = new ToolsCapability() },
                Handlers = new McpServerHandlers
                {
                    ListToolsHandler = (_, _) => ValueTask.FromResult(tools),
                    CallToolHandler = (request, _) => ValueTask.FromResult(Call(name, responses, request.Params!))
                }
            });

        // Ends when the client disposes and its pipe closes.
        _ = Task.Run(async () =>
        {
            await using (server)
            {
                await server.RunAsync();
            }
        });

        return await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));
    }

    /// <summary>
    /// First canned response whose "when" matches the arguments, as text content. No match is an
    /// isError result - how a real server reports a failed call - so a missing mock shows up in the run.
    /// </summary>
    private static CallToolResult Call(string server, Dictionary<string, List<CannedResponse>> responses, CallToolRequestParams call)
    {
        Dictionary<string, string> args = (call.Arguments ?? new Dictionary<string, JsonElement>())
            .ToDictionary(a => a.Key, a => a.Value.ValueKind == JsonValueKind.String ? a.Value.GetString()! : a.Value.GetRawText());

        CannedResponse? match = responses.GetValueOrDefault(call.Name)?.FirstOrDefault(r => r.When is null || r.When.All(w =>
            args.TryGetValue(w.Key, out string? value) && string.Equals(value, w.Value, StringComparison.OrdinalIgnoreCase)));

        if (match is null)
        {
            string shown = string.Join(", ", args.Select(a => $"{a.Key}={a.Value}"));
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"{server}/{call.Name} has no canned response for ({shown})." }]
            };
        }

        string text = match.Result.ValueKind == JsonValueKind.String ? match.Result.GetString()! : match.Result.GetRawText();
        return new CallToolResult { Content = [new TextContentBlock { Text = text }] };
    }

    private static Dictionary<string, List<CannedResponse>> LoadResponses(string dir) => !Directory.Exists(dir)
        ? []
        : Directory.EnumerateFiles(dir, "*.json").ToDictionary(
            file => Path.GetFileNameWithoutExtension(file),
            file => JsonSerializer.Deserialize<List<CannedResponse>>(File.ReadAllText(file)) ?? []);

    /// <summary>"when": every entry must equal the call argument of the same name. No "when" = fallback; first match wins.</summary>
    private sealed record CannedResponse(
        [property: JsonPropertyName("when")] Dictionary<string, string>? When,
        [property: JsonPropertyName("result")] JsonElement Result);
}
