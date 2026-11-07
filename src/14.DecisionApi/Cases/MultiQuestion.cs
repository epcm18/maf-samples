using OpenAI.Decisions;
using Shared;

namespace DecisionApi.Cases;

/// <summary>
/// Several questions about the same evidence in ONE request. This is the real shape of the API -
/// one read of the input, up to 400 questions, every answer independent.
/// Answers come back in question order, so the index is the lookup.
/// Try: "My card was charged twice and I can't open the app, refund me please"
/// </summary>
public static class MultiQuestion
{
    public static async Task RunAsync(DecisionsClient client, string input)
    {
        Out.Step("Multi-question - triage a support message in one call");

        DecisionsChoiceQuestion department = new(
            [
                new DecisionsChoice(new DecisionsChoiceValue("billing")) { Description = "Charges, payments, refunds, and subscriptions" },
                new DecisionsChoice(new DecisionsChoiceValue("technical")) { Description = "Software defects, errors, and outages" },
                new DecisionsChoice(new DecisionsChoiceValue("general")) { Description = "Anything else" }
            ],
            [new DecisionsInputTextInstructionsPart("Which team should handle this?")])
        { Name = "department" };

        DecisionsScoreQuestion urgency = new(
            [new DecisionsInputTextInstructionsPart("How urgently should support respond?")],
            [
                new DecisionsScoreLevel("routine") { Description = "Can wait for the normal queue" },
                new DecisionsScoreLevel("soon") { Description = "Should be handled today" },
                new DecisionsScoreLevel("immediately") { Description = "Needs a response right now" }
            ])
        { Name = "urgency" };

        DecisionsPredicateQuestion refund = new([new DecisionsInputTextInstructionsPart("Does the customer explicitly ask for a refund?")])
        { Name = "refund_requested" };

        DecisionsPredicateQuestion angry = new([new DecisionsInputTextInstructionsPart("Is the customer angry or frustrated?")])
        { Name = "angry" };

        DecisionResource decision = await client.CreateDecisionAsync(new DecisionCreationOptions(
            [new DecisionsUserMessageItem([new DecisionsInputTextContentPart(input)])],
            Models.Decision,
            [department, urgency, refund, angry]));

        foreach (DecisionsAnswer answer in decision.Answers)
        {
            switch (answer)
            {
                case DecisionsChoiceAnswer c:
                    Console.WriteLine($"{c.Name,-17} {c.Choice.StringValue} ({c.Confidence:P0})");
                    break;
                case DecisionsScoreAnswer s:
                    Console.WriteLine($"{s.Name,-17} {s.Score:F2} / {s.Probabilities.Count - 1}");
                    break;
                case DecisionsPredicateAnswer p:
                    Console.WriteLine($"{p.Name,-17} {p.Probability:P0}");
                    break;
                case DecisionsRefusalAnswer r:
                    Out.Warn($"{r.Name,-17} refused");
                    break;
            }
        }

        decision.PrintUsage();
    }
}
