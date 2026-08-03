// CodeAct: instead of exposing N tools, you expose ONE tool - execute_code - and let the model
// write code that orchestrates the work. Fewer tool definitions in the prompt, fewer round trips.
// You are now running model-written code, so Hyperlight runs it in a micro-VM sandbox.
//
// Wiring: HyperlightCodeActProvider goes into AIContextProviders, not into Tools.
// Your own tools go INSIDE the sandbox options and are reached from the generated code
// with call_tool("name", { arg: value }) - they are never visible to the model directly.
//
// Two behaviours to know (neither is in the guidance the provider injects):
//  - Output is stdout only; the value of the last expression is discarded. Use console.log(...).
//  - Tool PARAMETER NAMES are not published to the model, only tool names and descriptions.
//    So put the calling contract in [Description] - see Tools.cs.

using CodeActHyperlight;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hyperlight;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Shared;

Out.Init("12 - CodeAct with Hyperlight");

// Had to use a reasoning model, others not work as expected. But not mentioned in the doc.
const string model = Models.Reasoning;

//await JavaScriptAsync();
await JavaScriptWithToolsAsync();

return;

async Task JavaScriptAsync()
{
    Out.Step("A. Sandboxed code execution, no tools");

    // IDisposable - releases the sandbox and its native resources.
    using HyperlightCodeActProvider provider = new();

    ChatClientAgent agent = Clients.Chat(model).AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions
        {
            Instructions = "Always use the 'execute_code' tool for any math calculations " +
                           "(write the code in JavaScript). Show result + code you used."
        },
        AIContextProviders = [provider]
    });

    AgentResponse response = await agent.RunAsync(
        "What is the 25th Fibonacci number divided by 7, rounded to 2 decimals?");

    Console.WriteLine(response);
    response.Usage.Print();
    Out.Separator();
}

// Your C# tools, callable from inside the sandboxed code. One execute_code call loops over all
// three parts and makes six tool calls - classic tool calling would need six round trips.
async Task JavaScriptWithToolsAsync()
{
    Out.Step("B. Sandboxed code that can call your C# tools");

    HyperlightCodeActProviderOptions options = HyperlightCodeActProviderOptions.CreateForJavaScript();
    options.Tools =
    [
        AIFunctionFactory.Create(InventoryTools.GetStock, "get_stock"),
        AIFunctionFactory.Create(InventoryTools.GetPrice, "get_price")
    ];

    using HyperlightCodeActProvider provider = new(options); // attach the tools to the sandbox

    ChatClientAgent agent = Clients.Chat(model).AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions
        {
            Instructions = "Always use the 'execute_code' tool for any requests " +
                           "(write the code in JavaScript). Show result + code you used."
        },
        AIContextProviders = [provider]
    });

    AgentResponse response = await agent.RunAsync(
        "For parts BRG-4410, SEAL-220 and FLT-901: compute the total value of stock on hand " +
        "for each, and tell me the grand total.");

    Console.WriteLine(response);
    response.Usage.Print();

    Out.Separator();
    Out.Note("Python is also supported via HyperlightCodeActProviderOptions.CreateForWasm(\"python-sandbox.aot\"),");
    Out.Note("which needs the python-sandbox.aot module from Hyperlight.HyperlightSandbox.Guest.Python.");
}
