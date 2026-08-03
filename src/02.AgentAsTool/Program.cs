// Agent as a tool: agent.AsAIFunction() turns a whole agent into something another agent can call.
// The niche agents keep their own instructions and their own tool set; the coordinator only sees two tools.

using AgentAsTool;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Shared;

Out.Init("02 - Agent as a Tool");

AIAgent travelAgent = Clients.Chat()
    .AsAIAgent(
        name: "TravelAgent",
        instructions: "You answer travel questions only: weather and public holidays. Be terse.",
        tools: [AIFunctionFactory.Create(TravelTools.GetWeather), AIFunctionFactory.Create(TravelTools.GetHolidays)])
    .AsBuilder()
    .Use(ToolLogging.LogToolCalls)
    .Build();

AIAgent financeAgent = Clients.Chat()
    .AsAIAgent(
        name: "FinanceAgent",
        instructions: "You answer money questions only: currency conversion and allowances. Be terse.",
        tools: [AIFunctionFactory.Create(FinanceTools.Convert), AIFunctionFactory.Create(FinanceTools.GetAllowance)])
    .AsBuilder()
    .Use(ToolLogging.LogToolCalls)
    .Build();

AIAgent coordinator = Clients.Chat(Models.Standard)
    .AsAIAgent(
        name: "Coordinator",
        instructions: "You delegate. Never answer travel or finance questions yourself - always use the tools.",
        tools:
        [
            travelAgent.AsAIFunction(new AIFunctionFactoryOptions
            {
                Name = "ask_travel_agent",
                Description = "Ask the travel specialist about weather or public holidays."
            }),
            financeAgent.AsAIFunction(new AIFunctionFactoryOptions
            {
                Name = "ask_finance_agent",
                Description = "Ask the finance specialist about currency conversion or travel allowances."
            })
        ])
    .AsBuilder()
    .Use(ToolLogging.LogToolCalls)
    .Build();

Out.Step("Question that needs both specialists:");
AgentResponse response = await coordinator.RunAsync(
    "I am travelling to Portugal next week for 3 days. What is the weather like, " +
    "and how much allowance do I get in USD?");

Console.WriteLine(response);
response.Usage.Print();
