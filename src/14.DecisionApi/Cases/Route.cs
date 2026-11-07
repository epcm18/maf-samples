using Microsoft.Agents.AI;
using OpenAI.Chat;
using OpenAI.Decisions;
using Shared;

namespace DecisionApi.Cases;

/// <summary>
/// Choice question as an intent router in front of ordinary MAF agents.
/// The decision call returns a probability per option, so you can see how sure it was -
/// something a "classify this" chat prompt never tells you.
/// Try: "Who played the drums on Abbey Road?" / "Is Interstellar worth watching?" / "What's 2+2?"
/// </summary>
public static class Route
{
    public static async Task RunAsync(DecisionsClient client, string input)
    {
        Out.Step("Choice - route to a specialist agent");

        DecisionsChoiceQuestion question = new(
            [
                new DecisionsChoice(new DecisionsChoiceValue("music")) { Description = "Questions about music, musicians, albums, or songs" },
                new DecisionsChoice(new DecisionsChoiceValue("movies")) { Description = "Questions about films, actors, directors, or cinema" },
                new DecisionsChoice(new DecisionsChoiceValue("other")) { Description = "Anything that is not about music or movies" }
            ],
            [new DecisionsInputTextInstructionsPart("Is this a question about music, movies, or something else?")])
        {
            Name = "intent"
        };

        DecisionResource decision = await client.CreateDecisionAsync(
            new DecisionCreationOptions([new DecisionsUserMessageItem([new DecisionsInputTextContentPart(input)])], Models.Decision, [question]));

        if (decision.Answers[0] is not DecisionsChoiceAnswer answer)
        {
            Out.Warn($"Not a choice answer: {decision.Answers[0].Kind}");
            return;
        }

        string intent = answer.Choice.StringValue;
        Console.WriteLine($"Routed to: {intent} (confidence {answer.Confidence:P0})");
        foreach (DecisionsChoiceProbability p in answer.Probabilities)
        {
            Out.Note($"  {p.Value.StringValue,-8} {p.Probability:P1}");
        }
        decision.PrintUsage();

        AIAgent agent = intent switch
        {
            "music" => Clients.Chat().AsAIAgent(instructions: "You are a music nerd. Answer in at most 200 characters."),
            "movies" => Clients.Chat().AsAIAgent(instructions: "You are a movie nerd. Answer in at most 200 characters."),
            _ => Clients.Chat().AsAIAgent(instructions: "You are a general nerd. Answer in at most 200 characters.")
        };

        AgentResponse response = await agent.RunAsync(input);
        Console.WriteLine(response);
        response.Usage.Print();
    }
}
