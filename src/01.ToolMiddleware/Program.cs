// Tool middleware: .AsBuilder().Use(...).Build() wraps every tool call.
// Three things you can do from inside it: observe, block, rewrite.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Shared;
using ToolMiddleware;

Out.Init("01 - Tool Middleware");

// The balance of this one is confidential: the middleware will redact it.
const string restrictedAccount = "NL99WXYZ";

AIAgent agent = Clients.Chat()
    .AsAIAgent(
        name: "BankAgent",
        instructions: "You are a banking assistant. Use the tools for anything account related. " +
                      "Never guess a number a tool did not give you.",
        tools:
        [
            AIFunctionFactory.Create(Tools.GetBalance),
            AIFunctionFactory.Create(Tools.DeleteAccount)
        ])
    .AsBuilder()
    .Use(AuditMiddleware)      // runs last (closest to the tool)
    .Use(GuardMiddleware)      // runs first
    .Build();

Out.Step("1. Observe - middleware logs, the tool result passes through untouched");
Console.WriteLine(await agent.RunAsync("What is the balance of account NL12ABCD?"));
Out.Separator();

Out.Step("2. Block - middleware refuses without ever invoking the tool");
Console.WriteLine(await agent.RunAsync("Delete account NL12ABCD."));
Out.Separator();

Out.Step($"3. Rewrite - the balance of {restrictedAccount} is replaced before it reaches the model");
Out.Note("The real balance exists only inside the tool, so the model genuinely cannot report it.");
Console.WriteLine(await agent.RunAsync($"What is the balance of account {restrictedAccount}?"));

return;

// Blocks a tool entirely. Returning a string instead of calling next() means the tool never runs,
// and the model receives that string as the tool result.
async ValueTask<object?> GuardMiddleware(
    AIAgent callingAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    if (context.Function.Name == nameof(Tools.DeleteAccount))
    {
        Out.Warn($"  [guard] blocked {context.Function.Name}");
        return "DENIED: account deletion is not permitted through this assistant.";
    }

    return await next(context, cancellationToken);
}

// Observes on the way in, rewrites on the way out.
async ValueTask<object?> AuditMiddleware(
    AIAgent callingAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    Out.Note($"  [audit] {context.Function.Name}(" +
             string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")) + ")");

    object? result = await next(context, cancellationToken);
    bool isRestricted = false;
    //bool isRestricted = context.Function.Name == nameof(Tools.GetBalance)
    //                    && context.Arguments.TryGetValue("accountNumber", out object? account)
    //                    && account?.ToString() == restrictedAccount;

    if (isRestricted)
    {
        Out.Warn($"  [audit] redacted the result (real value was {result})");
        result = "REDACTED: the balance of this account is not visible to this assistant.";
    }

    return result;
}
