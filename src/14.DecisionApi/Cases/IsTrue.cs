using OpenAI.Decisions;
using Shared;

namespace DecisionApi.Cases;

/// <summary>
/// Predicate questions: one probability back per question.
///
/// Threshold: the API has no threshold field. DecisionsPredicateQuestion carries only Name and
/// Instructions, and the answer is a bare probability. rwj's toolkit "Threshold" (default 0.5) is
/// the same `probability >= threshold` check done client-side, after the call - which is what
/// the threshold parameter here does. Changing it never changes the request or the cost.
///
/// Three shapes, all ending in the same request builder:
///   - one input, the movie question                     -> 1 call
///   - many inputs, the movie question                   -> N calls, run concurrently
///     (one request judges ONE piece of evidence, so many inputs means many requests)
///   - one input made of several evidence parts, several questions -> 1 call
/// </summary>
public static class IsTrue
{
    private const string MovieQuestion = "Is this a question about a movie?";

    public static async Task RunAsync(DecisionsClient client, string input, double threshold = 0.5)
    {
        Out.Step($"Predicate - is this a question about a movie? (threshold {threshold:0.##})");

        DecisionResource decision = await DecideAsync(client, [input], [MovieQuestion]);
        if (decision.Answers[0] is not DecisionsPredicateAnswer answer)
        {
            Out.Warn($"Not a predicate answer: {decision.Answers[0].Kind}");
            return;
        }

        Console.WriteLine($"Probability: {answer.Probability:P1}");
        Console.WriteLine($"IsTrue:      {answer.Probability >= threshold}");
        decision.PrintUsage();
    }

    public static async Task RunAsync(DecisionsClient client, string[] inputs, double threshold = 0.5)
    {
        Out.Step($"Predicate x{inputs.Length} - is this a question about a movie? (threshold {threshold:0.##})");

        DecisionResource[] decisions = await Task.WhenAll(inputs.Select(input => DecideAsync(client, [input], [MovieQuestion])));

        for (int i = 0; i < inputs.Length; i++)
        {
            Console.WriteLine($"{Verdict(decisions[i].Answers[0], threshold)}  {inputs[i]}");
        }

        Out.Note($"{inputs.Length} calls | input tokens total: {decisions.Sum(d => d.Usage.InputTokenCount)}");
    }

    /// <summary>
    /// One input built from several evidence parts (e.g. a story and a claim about it),
    /// with one predicate per question - all in a single request.
    /// </summary>
    public static async Task RunAsync(DecisionsClient client, string[] evidence, string[] questions, double threshold = 0.5)
    {
        Out.Step($"Predicate - {questions.Length} questions over {evidence.Length} evidence parts, one request (threshold {threshold:0.##})");
        foreach (string part in evidence)
        {
            Out.Note($"  evidence: {part}");
        }

        DecisionResource decision = await DecideAsync(client, evidence, questions);

        for (int i = 0; i < questions.Length; i++)
        {
            Console.WriteLine($"{Verdict(decision.Answers[i], threshold)}  {questions[i]}");
        }

        decision.PrintUsage();
    }

    // Every evidence part becomes a content part of ONE user message; every question becomes a
    // predicate named q1..qN. Answers come back in question order.
    private static async Task<DecisionResource> DecideAsync(DecisionsClient client, string[] evidence, string[] questions)
    {
        DecisionsUserMessageItem message = new(evidence.Select(part => new DecisionsInputTextContentPart(part)));

        DecisionsPredicateQuestion[] predicates = questions
            .Select((q, i) => new DecisionsPredicateQuestion([new DecisionsInputTextInstructionsPart(q)]) { Name = $"q{i + 1}" })
            .ToArray();

        return await client.CreateDecisionAsync(new DecisionCreationOptions([message], Models.Decision, predicates));
    }

    private static string Verdict(DecisionsAnswer answer, double threshold) => answer is DecisionsPredicateAnswer p
        ? $"{p.Probability,7:P1}  {(p.Probability >= threshold ? "true " : "false")}"
        : $"{answer.Kind,-14}";
}
