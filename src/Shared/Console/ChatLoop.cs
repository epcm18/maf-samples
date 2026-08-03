using Microsoft.Agents.AI;

namespace Shared;

public static class ChatLoop
{
    /// <summary>Interactive REPL against an agent. '/new' resets the session, '/exit' quits.</summary>
    public static async Task RunAsync(AIAgent agent)
    {
        AgentSession session = await agent.CreateSessionAsync();
        Out.Note("Type a message. '/new' starts a fresh session, '/exit' quits.");

        while (true)
        {
            System.Console.Write("> ");
            string input = System.Console.ReadLine() ?? string.Empty;

            if (input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (input.Equals("/new", StringComparison.OrdinalIgnoreCase))
            {
                session = await agent.CreateSessionAsync();
                Out.Note("New session.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            AgentResponse response = await agent.RunAsync(input, session);
            System.Console.WriteLine(response);
            response.Usage.Print();
            Out.Separator();
        }
    }
}
