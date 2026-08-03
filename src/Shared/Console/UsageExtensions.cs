using Microsoft.Extensions.AI;

namespace Shared;

public static class UsageExtensions
{
    /// <summary>Prints the token breakdown. Cached + reasoning counts are the interesting ones.</summary>
    public static void Print(this UsageDetails? usage, string? label = null)
    {
        if (usage is null)
        {
            Out.Note("(no usage reported)");
            return;
        }

        Out.Note($"{label}Input: {usage.InputTokenCount} (cached: {usage.CachedInputTokenCount ?? 0}) | " +
                 $"Output: {usage.OutputTokenCount} (reasoning: {usage.ReasoningTokenCount ?? 0})");
    }
}
