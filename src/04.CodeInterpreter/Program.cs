// HostedCodeInterpreterTool: the model writes Python and runs it in an OpenAI-hosted sandbox.
// Same shape as web search - Responses API only, hosted, extra cost.
//
// The interesting part is getting files back out. Generated files are not in the text response;
// they arrive as AIAnnotation entries whose RawRepresentation is a ContainerFileCitationMessageAnnotation.
// You then download them from the container yourself.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Containers;
using OpenAI.Responses;
using Shared;
using System.ClientModel;

Out.Init("04 - Hosted Code Interpreter");

OpenAIClient openAi = Clients.OpenAi();

AIAgent agent = openAi.GetResponsesClient()
    .AsAIAgent(
        model: Models.Standard,
        instructions: "You are a data analyst. Use the code interpreter for any calculation or chart.",
        tools: [new HostedCodeInterpreterTool()]);

const string question =
    "Take the numbers 12, 47, 8, 93, 55, 31, 76, 22. " +
    "Calculate mean, median and standard deviation, then save a bar chart of them as a PNG.";

Out.Step($"Q: {question}");

AgentResponse response = await agent.RunAsync(question);
Console.WriteLine(response);
Out.Separator();
response.Usage.Print();

string downloadFolder = Path.Combine(AppContext.BaseDirectory, "output");
Directory.CreateDirectory(downloadFolder);

foreach (AIAnnotation annotation in response.Messages.SelectMany(m => m.Contents).SelectMany(c => c.Annotations ?? []))
{
    if (annotation.RawRepresentation is not ContainerFileCitationMessageAnnotation citation)
    {
        continue;
    }

    ContainerClient containers = openAi.GetContainerClient();
    ClientResult<BinaryData> file = await containers.DownloadContainerFileAsync(citation.ContainerId, citation.FileId);

    string path = Path.Combine(downloadFolder, citation.Filename);
    await File.WriteAllBytesAsync(path, file.Value.ToArray());
    Out.Step($"Downloaded {citation.Filename} -> {path}");
}
