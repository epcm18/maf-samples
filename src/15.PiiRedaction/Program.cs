// PII redaction with Azure Language in Foundry Tools, used from a MAF agent on OpenAI.
//
// Azure Language exposes an MCP server: one URL, one key header, 11 tools
// (redact_pii_from_text, extract_named_entities_from_text, detect_sentiment_from_text, ...).
// The Foundry lab wires it to a Foundry agent; here it's wired two ways:
//
//   1. RedactUserInput - hosted MCP tool on the Responses API. OpenAI calls Azure Language.
//      The model sees the raw text and returns it redacted. Good for cleaning text before you
//      show, store or forward it.
//   2. MaskToolOutput  - tool middleware + our own MCP client. A tool returns personal details,
//      we mask them before the model sees anything. Good for keeping PII away from the LLM.
//
// Setup: fill AzureLanguage in appsettings.json.

using PiiRedaction;
using PiiRedaction.Cases;
using Shared;

Out.Init("15 - PII Redaction (Azure Language)");

const string supportEmail = """
                            Hi, this is Kamal Perera. I was charged twice for order #48213.
                            You can reach me at kamal.perera@example.com or 077 123 4567.
                            My card number is 4111 1111 1111 1111 and I live at 42 Galle Road, Colombo 03.
                            """;

const string supportQuestion = "Customer C-1001 says their headphones haven't arrived. " +
                               "Summarise their account and draft a short reply to them.";

try
{
    LanguageSettings settings = LanguageSettings.Load();

    Out.Header("Case 1 - agent redacts the user's text (hosted MCP tool)");
    await RedactUserInput.RunAsync(settings, supportEmail);

    Out.Separator();

    Out.Header("Case 2 - tool output is masked before the model sees it (middleware)");
    await MaskToolOutput.RunAsync(settings, supportQuestion);
}
catch (Exception ex)
{
    Out.Warn($"{ex.GetType().Name}: {ex.Message}");
}

Out.Separator();
