// The Batch API: ~50% cheaper, up to 24h turnaround, and it sits below the agent abstraction.
// You write JSONL requests, upload, poll, download. No AIAgent involved - and no tool calling.
//
// Good fit: large repetitive jobs (classification, translation, enrichment) where latency is irrelevant.
// Bad fit: anything interactive or anything that needs tools.

using OpenAI;
using OpenAI.Batch;
using OpenAI.Files;
using Shared;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

Out.Init("09 - Batch Jobs");

string[] reviews =
[
    "The pump arrived two weeks late and the housing was cracked.",
    "Works exactly as described, installation took ten minutes.",
    "Decent build quality but the manual is useless.",
    "Third failure in six months. We are switching supplier.",
    "Support answered within an hour and shipped a replacement free of charge."
];

// 1. Build the JSONL - one line per request, each with your own custom_id.
string jsonlPath = Path.Combine(AppContext.BaseDirectory, "batch-input.jsonl");
StringBuilder jsonl = new();

for (int i = 0; i < reviews.Length; i++)
{
    var request = new
    {
        custom_id = $"review-{i + 1}",
        method = "POST",
        url = "/v1/chat/completions",
        body = new
        {
            model = Models.Cheap,
            messages = new object[]
            {
                new { role = "system", content = "Classify the review sentiment as POSITIVE, NEUTRAL or NEGATIVE. Reply with one word." },
                new { role = "user", content = reviews[i] }
            }
        }
    };

    jsonl.AppendLine(JsonSerializer.Serialize(request));
}

await File.WriteAllTextAsync(jsonlPath, jsonl.ToString());
Out.Note($"Wrote {reviews.Length} requests to {jsonlPath}");

// 2. Upload with purpose "batch".
OpenAIClient client = Clients.OpenAi();
OpenAIFileClient files = client.GetOpenAIFileClient();
BatchClient batches = client.GetBatchClient();

// Throws 401 here if the key lacks api.files.write access
OpenAIFile uploaded = await files.UploadFileAsync(jsonlPath, new FileUploadPurpose("batch"));
Out.Step($"Uploaded file: {uploaded.Id}");

// 3. Create the batch. The typed surface is thin here, so this goes through BinaryContent.
BinaryContent payload = BinaryContent.CreateJson(new CreateBatchRequest
{
    InputFileId = uploaded.Id,
    Endpoint = "/v1/chat/completions",
    CompletionWindow = "24h"
});

CreateBatchOperation operation = await batches.CreateBatchAsync(payload, waitUntilCompleted: false);
Out.Step($"Batch created: {operation.BatchId}");

// 4. Poll until a terminal status.
string[] terminal = ["completed", "expired", "cancelled", "failed"];
string status = string.Empty;
string? outputFileId = null;
Stopwatch stopwatch = Stopwatch.StartNew();

while (!terminal.Contains(status))
{
    await Task.Delay(TimeSpan.FromSeconds(10));

    ClientResult result = await batches.GetBatchAsync(operation.BatchId, new RequestOptions());
    BatchStatus batch = JsonSerializer.Deserialize<BatchStatus>(result.GetRawResponse().Content.ToString())!;

    status = batch.Status;
    outputFileId = batch.OutputFileId;

    Out.Note($"[{stopwatch.Elapsed.TotalSeconds:F0}s] {batch.Status} - " +
             $"total {batch.Counts.Total}, completed {batch.Counts.Completed}, failed {batch.Counts.Failed}");
}

// 5. Download the results.
if (string.IsNullOrWhiteSpace(outputFileId))
{
    Out.Warn($"Batch ended with status '{status}' and produced no output file.");
    return;
}

ClientResult<BinaryData> download = await files.DownloadFileAsync(outputFileId);
string outputPath = Path.Combine(AppContext.BaseDirectory, "batch-output.jsonl");
await File.WriteAllBytesAsync(outputPath, download.Value.ToArray());

Out.Step($"Results written to {outputPath}");
Out.Separator();

foreach (string line in await File.ReadAllLinesAsync(outputPath))
{
    using JsonDocument document = JsonDocument.Parse(line);
    string customId = document.RootElement.GetProperty("custom_id").GetString()!;
    string content = document.RootElement
        .GetProperty("response").GetProperty("body")
        .GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;

    Console.WriteLine($"{customId}: {content}");
}

file sealed class CreateBatchRequest
{
    [JsonPropertyName("input_file_id")] public required string InputFileId { get; init; }
    [JsonPropertyName("endpoint")] public required string Endpoint { get; init; }
    [JsonPropertyName("completion_window")] public required string CompletionWindow { get; init; }
}

file sealed class BatchStatus
{
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("request_counts")] public required BatchCounts Counts { get; init; }
    [JsonPropertyName("output_file_id")] public string? OutputFileId { get; init; }
    [JsonPropertyName("error_file_id")] public string? ErrorFileId { get; init; }
}

file sealed class BatchCounts
{
    [JsonPropertyName("total")] public int Total { get; init; }
    [JsonPropertyName("completed")] public int Completed { get; init; }
    [JsonPropertyName("failed")] public int Failed { get; init; }
}
