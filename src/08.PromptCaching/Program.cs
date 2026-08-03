// Prompt caching is on by default at OpenAI - there is no switch to flip.
// You only observe it, through UsageDetails.CachedInputTokenCount.
//
// Rules that matter in practice:
//  - Only kicks in above ~1024 input tokens.
//  - It caches a *prefix*. Matching stops at the first differing character.
//  - So: static content first, variable content last. A changed first character costs you everything.
//
// Run 1 is a cold cache. Run 2 reuses it. Run 3 changes one word at the very start.
// Run 4 changes one word near the end - only the tail is lost.

using Microsoft.Agents.AI;
using OpenAI.Chat;
using Shared;

Out.Init("08 - Prompt Caching");

string bigPreamble = BuildPreamble("Contoso");
string bigPreambleChangedAtStart = BuildPreamble("Fabrikam");
string bigPreambleChangedAtEnd = BuildPreamble("Contoso") + "\nAddendum: rule 999 - always greet the customer by name.";

await AskAsync("1. cold cache", bigPreamble, "Who is responsible for approving refunds?");
await AskAsync("2. same prefix (expect a cache hit)", bigPreamble, "What is the escalation path for a P1 incident?");
await AskAsync("3. first word changed (expect cache miss)", bigPreambleChangedAtStart, "Who is responsible for approving refunds?");
await AskAsync("4. text appended at the end (expect a partial hit)", bigPreambleChangedAtEnd, "Who is responsible for approving refunds?"); // hit almost same cache like in 2

return;

async Task AskAsync(string label, string instructions, string question)
{
    Out.Step(label);

    AIAgent agent = Clients.Chat(Models.Standard).AsAIAgent(instructions: instructions);
    AgentResponse response = await agent.RunAsync(question);

    long input = response.Usage?.InputTokenCount ?? 0;
    long cached = response.Usage?.CachedInputTokenCount ?? 0;
    double percent = input == 0 ? 0 : cached * 100.0 / input;

    Out.Note($"input {input} | cached {cached} ({percent:F0}%)");
    Console.WriteLine(response);
    Out.Separator();
}

// Long, stable system prompt. Caching only starts above ~1024 tokens, so this needs real bulk.
static string BuildPreamble(string company)
{
    IEnumerable<string> rules = Enumerable.Range(1, 220)
        .Select(i => $"Rule {i}: Support agents at {company} must document case {i} within one business day, " +
                     $"tag it with the correct product area, and link any related incident.");

    return $"You are the internal support handbook for {company}.\n" +
           string.Join("\n", rules) +
           "\nRule A: Refunds above 1000 EUR are approved by the Finance Lead, below that by the Support Manager." +
           "\nRule B: A P1 incident escalates to the on-call engineer, then the Head of Engineering after 30 minutes.";
}
