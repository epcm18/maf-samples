// OpenAI Decisions API through the SDK's typed DecisionsClient.
//
// What it is: a classification endpoint, not a chat endpoint. You give it evidence (text and/or
// images) plus a fixed list of questions, each of one of three types - predicate, choice, score -
// and get back probabilities, not prose. No tools, no streaming, no generated text.
//
// Why bother when RunAsync<T> (structured output) already exists:
//   - calibrated probabilities per option, not just a label
//   - up to 400 independent questions answered from one read of the evidence
//   - it cannot ramble: the output schema is the API's, not something the model has to honour
//   - output tokens are free; you pay input only
//
// Wiring: OpenAI 2.15.0+ ships OpenAI.Decisions (experimental, OPENAI001). The client hangs off
// the same OpenAIClient every other sample uses: Clients.OpenAi().GetDecisionsClient().
// MAF has no abstraction over this yet - the agent framework is not involved in the decision call
// at all. The Route case shows the natural pairing: decide first, then hand off to an AIAgent.
//
// Decisions are only supported on the GPT-6 Luna model family.

using DecisionApi;
using DecisionApi.Cases;
using OpenAI.Decisions;
using Shared;

Out.Init("14 - OpenAI Decisions API");

const bool trace = true; // true -> prints request and response JSON for every call
DecisionsClient client = DecisionsTrace.CreateClient(trace);

string input = "Did you like the new Dune film?"; // default input for testing
string[] inputArr = [
    "Did you like the new Dune film?",
    "What is the capital of France?",
    "Who played the drums on Abbey Road?",
    "Is Interstellar worth watching?",
    "What's 2+2?"
];

// One input, two evidence parts. Worked out by hand: Joe ends with 4, Jane with 6,
// so the expected answers are false for Joe and true for Jane.
string story = "Story: Jane and Joe had 5 apples each. Jane handed 4 apples to Joe. " +
               "Then Joe handed 3 apples to Jane, and later 2 more apples to Jane.";
string claim = "Claim: Now Joe has zero apples and Jane has 6 apples.";
string[] claimQuestions = [
    "Following the story's transfers step by step, does the claim that Joe ends with zero apples hold?",
    "Following the story's transfers step by step, does the claim that Jane ends with 6 apples hold?"
];

try
{
    //await IsTrue.RunAsync(client, input);
    //await IsTrue.RunAsync(client, inputArr, threshold: 0.7);
    //await IsTrue.RunAsync(client, inputArr);
    //await IsTrue.RunAsync(client, [story, claim], claimQuestions); // one input, two evidence parts, two decisions, ONE request
    //await Route.RunAsync(client, input);
    //await Score.RunAsync(client, "My laptop screen is getting blue out of nowhere");
    //await MultiQuestion.RunAsync(client, "My card was charged twice and I can't open the app, refund me please");
    await Image.RunAsync(client, "Does this asset has a nameplate");
}
catch (Exception ex)
{
    Out.Warn($"{ex.GetType().Name}: {ex.Message}");
}

Out.Separator();
