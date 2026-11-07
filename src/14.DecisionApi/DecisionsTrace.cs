using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using OpenAI;
using OpenAI.Decisions;
using Shared;

namespace DecisionApi;

/// <summary>
/// The typed DecisionsClient hides the wire. To see the JSON again, add a pipeline policy:
/// it sits in the SDK's own pipeline, so it sees exactly what goes out and comes back.
/// Built from an OpenAIClient with the policy attached, so endpoint and auth match every other sample.
/// </summary>
public static class DecisionsTrace
{
    public static DecisionsClient CreateClient(bool trace)
    {
        if (!trace)
        {
            return Clients.OpenAi().GetDecisionsClient();
        }

        OpenAIClientOptions options = new();
        options.AddPolicy(new TracePolicy(), PipelinePosition.PerCall);
        return new OpenAIClient(new ApiKeyCredential(SecretsManager.Get().OpenAiApiKey), options).GetDecisionsClient();
    }

    private sealed class TracePolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            WriteRequest(message);
            ProcessNext(message, pipeline, currentIndex);
            WriteResponse(message);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            WriteRequest(message);
            await ProcessNextAsync(message, pipeline, currentIndex);
            WriteResponse(message);
        }

        private static void WriteRequest(PipelineMessage message)
        {
            string body = string.Empty;
            if (message.Request.Content is not null)
            {
                using MemoryStream stream = new();
                message.Request.Content.WriteTo(stream);
                body = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }

            Out.Note($"--- {message.Request.Method} {message.Request.Uri} ---");
            Out.Note(Pretty(body));
        }

        private static void WriteResponse(PipelineMessage message)
        {
            if (message.Response is null)
            {
                return;
            }

            Out.Note($"--- response {message.Response.Status} ---");
            Out.Note(Pretty(message.Response.Content.ToString()));
        }

        private static string Pretty(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException)
            {
                return json;
            }
        }
    }
}
