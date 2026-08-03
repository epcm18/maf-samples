# maf-samples

A small hands-on repo for the Microsoft Agent Framework topics that were new to me in
Rasmus' Udemy course *AI in C# using the Microsoft Agent Framework*.

The course repo covers everything; this one covers only the parts I highlighted in my notes,
one runnable console project each, wired to **OpenAI direct**.

- Microsoft Agent Framework **1.15.0**
- .NET **10**
- OpenAI API (some samples need the Responses API, which Azure OpenAI does not support)

If you are new to the framework, start with the course — this repo assumes you already know
what an agent and a tool call are. It is a set of worked notes, not a tutorial.

## Setup

You need the **.NET 10 SDK** ([global.json](global.json) pins `10.0.100`) and an **OpenAI API
key** from platform.openai.com. Azure OpenAI will not do — samples 03, 04 and 05 need the
Responses API.

One API key, shared by every sample through `src/Shared`:

```bash
dotnet user-secrets set "OpenAiApiKey" "sk-..." --project src/Shared
```

`OPENAI_API_KEY` as an environment variable works too. Then:

```bash
dotnet build maf-samples.slnx
```

Model names live in one place — [src/Shared/Models.cs](src/Shared/Models.cs) — change them there.
Everything else in `src/Shared` is plumbing: the client factory, secret loading, and the console
helpers (`Out.Step`, `Out.Note`, usage printing) the samples use to keep their output readable.

**These cost real money.** Every run is live API traffic. The samples are deliberately small,
but 03 adds ~$10/1k web searches and 09 fires a batch job — read the comments at the top of a
`Program.cs` before you run it.

## The samples

Run any of them with `dotnet run --project src/<folder>`. Each is a single-file console app —
open `Program.cs`, read the comment block at the top, then run it and watch the output.

| # | Sample | What was new | Watch for |
|---|--------|--------------|-----------|
| 01 | [ToolMiddleware](src/01.ToolMiddleware) | `.AsBuilder().Use(...)` around tool calls | Observe, **block** (return without calling `next`), or **rewrite** the result. Redaction only works on values the tool alone knows — it cannot unsee the prompt or the tool arguments |
| 02 | [AgentAsTool](src/02.AgentAsTool) | `agent.AsAIFunction()` | A whole agent becomes one tool; specialists keep their own instructions and tools |
| 03 | [WebSearchTool](src/03.WebSearchTool) | `HostedWebSearchTool()` | Needs `GetResponsesClient()`, not `GetChatClient()`. Extra ~$10/1k calls. Not on Azure OpenAI |
| 04 | [CodeInterpreter](src/04.CodeInterpreter) | `HostedCodeInterpreterTool()` | Generated files come back as annotations — you download them from the container yourself |
| 05 | [ReasoningEffort](src/05.ReasoningEffort) | Setting effort manually | `ChatOptions.RawRepresentationFactory` is the escape hatch. Compare reasoning token counts. Summaries only on the Responses API |
| 06 | [PdfAsMemory](src/06.PdfAsMemory) | PDF straight into the prompt | `DataContent` as base64 or raw bytes. OpenAI only, no URLs, priced as input tokens every turn |
| 07 | [AiAsFilter](src/07.AiAsFilter) | AI as a filter, not a calculator | The model builds a filter, C# runs it. Compare token counts and accuracy against dumping 200 rows in the prompt |
| 08 | [PromptCaching](src/08.PromptCaching) | Caching is on by default | `CachedInputTokenCount`. Prefix matching: a change at the start costs everything, a change at the end costs the tail |
| 09 | [BatchJobs](src/09.BatchJobs) | Batch API | ~50% cheaper, up to 24h, **no tool calling**. Sits below the agent abstraction entirely |
| 11 | [AgentHarness](src/11.AgentHarness) | `AsHarnessAgent()` | Todos, plan/execute modes, background subagents, compaction, file memory — all on by default. This is the "build your own Copilot" piece |
| 12 | [CodeActHyperlight](src/12.CodeActHyperlight) | CodeAct + Hyperlight sandbox | One `execute_code` tool replaces N tools. Your C# tools go *inside* the sandbox options so generated code can call them |

Numbers match my notes, so there are gaps. Nothing is missing.

A reasonable order if you are working through them: 01 and 02 for the mechanics, 07 to 09 for
the cost thinking, then 11 and 12 for the frontier bits. 03 to 06 are self-contained and can be
read in any order.

## Things worth remembering

**Responses API vs Chat Completions.** Hosted tools (03, 04) and reasoning summaries (05) only
exist on the Responses API. The model name moves from `GetChatClient("model")` into
`AsAIAgent(model: "model")`.

**Everything experimental is a build error by default.** `OPENAI001`, `MEAI001` and `MAAI001`
are suppressed in [Directory.Build.props](Directory.Build.props). Harness and Hyperlight are
both evaluation-only — expect them to move between MAF versions.

**Package versions are central.** All of them live in
[Directory.Packages.props](Directory.Packages.props); the `.csproj` files carry no version
numbers. Bump there.

## Not covered here

Deliberately skipped, since the course repo already does them well and they were not new to me:
provider setup for Azure / Foundry / Ollama, basic tool calling, structured output on its own,
RAG, workflows, telemetry, MCP.
