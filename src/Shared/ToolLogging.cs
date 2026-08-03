using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Shared;

public static class ToolLogging
{
    /// <summary>Drop-in middleware that prints every tool call. Wire it with .AsBuilder().Use(...).Build().</summary>
    public static async ValueTask<object?> LogToolCalls(
        AIAgent callingAgent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        string args = context.Arguments.Count > 0
            ? " (" + string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")) + ")"
            : string.Empty;

        Out.Note($"  -> {callingAgent.Name ?? "agent"} calls {context.Function.Name}{args}");

        object? result = await next(context, cancellationToken);

        Out.Note($"  <- {context.Function.Name} returned: {Truncate(result?.ToString())}");
        return result;
    }

    private static string Truncate(string? value, int max = 120)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(null)";
        }

        value = value.ReplaceLineEndings(" ");
        return value.Length <= max ? value : value[..max] + "...";
    }
}
