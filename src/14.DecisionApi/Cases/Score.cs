using OpenAI.Decisions;
using Shared;

namespace DecisionApi.Cases;

/// <summary>
/// Score question: an ordered scale. Level order defines the index (first = 0).
/// The score is the probability-weighted position on that scale, so 1.4 means "between
/// Degraded and Blocking, leaning Degraded" - more information than a single label.
/// Try: "The login page shows a typo" vs "Nobody in the company can log in since this morning"
/// </summary>
public static class Score
{
    public static async Task RunAsync(DecisionsClient client, string input)
    {
        Out.Step("Score - how severe is this support ticket?");

        DecisionsScoreQuestion question = new(
            [new DecisionsInputTextInstructionsPart("How severely does the reported problem affect the customer's ability to use the product?")],
            [
                new DecisionsScoreLevel("Minor") { Description = "A minor issue with no meaningful impact on using the product" },
                new DecisionsScoreLevel("Degraded") { Description = "Some functionality is impaired, but the product remains usable" },
                new DecisionsScoreLevel("Blocking") { Description = "The customer cannot use the product at all" }
            ])
        {
            Name = "severity"
        };

        DecisionResource decision = await client.CreateDecisionAsync(
            new DecisionCreationOptions([new DecisionsUserMessageItem([new DecisionsInputTextContentPart(input)])], Models.Decision, [question]));

        if (decision.Answers[0] is not DecisionsScoreAnswer answer)
        {
            Out.Warn($"Not a score answer: {decision.Answers[0].Kind}");
            return;
        }

        Console.WriteLine($"Score: {answer.Score:F2} on 0..{answer.Probabilities.Count - 1} (confidence {answer.Confidence:P0})");
        foreach (DecisionsScoreProbability level in answer.Probabilities)
        {
            Out.Note($"  {level.Value} {level.Label,-9} {level.Probability:P1}");
        }
        decision.PrintUsage();
    }
}
