using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace Shared;

public static class Clients
{
    public static OpenAIClient OpenAi() => new(SecretsManager.Get().OpenAiApiKey);

    /// <summary>Chat Completions API - the default for most samples.</summary>
    public static ChatClient Chat(string model = Models.Cheap) => OpenAi().GetChatClient(model);

    /// <summary>
    /// Responses API - required for hosted tools (web search, code interpreter)
    /// and for reasoning summaries. Not available on Azure OpenAI for all of these.
    /// </summary>
    public static ResponsesClient Responses() => OpenAi().GetResponsesClient();
}
