using OpenAI.Decisions;
using Shared;

namespace DecisionApi;

public static class DecisionUsage
{
    /// <summary>
    /// Output tokens are not billed on Decisions; cached input is the number to watch.
    /// The response schema carries output_tokens_details.reasoning_tokens like every other OpenAI
    /// endpoint - printed here so you can see whether it is ever non-zero.
    /// </summary>
    public static void PrintUsage(this DecisionResource decision)
    {
        DecisionsUsage u = decision.Usage;
        Out.Note($"{decision.Model} | input: {u.InputTokenCount} (cached: {u.InputTokenDetails?.CachedTokenCount ?? 0}) | " +
                 $"output: {u.OutputTokenCount} (reasoning: {u.OutputTokenDetails?.ReasoningTokenCount ?? 0}) | total: {u.TotalTokenCount}");
    }
}
