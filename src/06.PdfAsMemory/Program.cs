// Sending a PDF straight to the model as DataContent - no text extraction, no chunking, no vector store.
//
// Constraints worth remembering:
//  - OpenAI only. Azure OpenAI does not accept PDF content this way.
//  - PDFs cannot be passed by URL, only as local bytes.
//  - The whole document lands in the context window, so it is priced as input tokens every turn.
//
// Two ways to hand over the bytes: a base64 data URI, or ReadOnlyMemory<byte> directly.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Shared;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

Out.Init("06 - PDF as Memory");

string pdfPath = Path.Combine(AppContext.BaseDirectory, "Data", "expense-policy.pdf");
byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);

AIAgent agent = Clients.Chat(Models.Standard)
    .AsAIAgent(instructions: "Answer strictly from the attached document. If it is not in there, say so.");

// Option A: base64 data URI -> 3 conversions you had to do if you only have a file. better to use when a uri presents
Out.Step("A. PDF as base64 data URI");
string dataUri = $"data:application/pdf;base64,{Convert.ToBase64String(pdfBytes)}";

AgentResponse a = await agent.RunAsync(new ChatMessage(ChatRole.User,
[
    new TextContent("What is the deadline for submitting an expense claim, and what happens if you miss it?"),
    new DataContent(dataUri, "application/pdf")
]));

Console.WriteLine(a);
a.Usage.Print();
Out.Separator();

// Option B: raw bytes - same result, no base64 string in memory -> one single copy of your resource
Out.Step("B. PDF as ReadOnlyMemory<byte>");
ReadOnlyMemory<byte> memory = pdfBytes.AsMemory();

AgentResponse b = await agent.RunAsync(new ChatMessage(ChatRole.User,
[
    new TextContent("I want to book a 200 EUR hotel in Lisbon. Is that allowed? Quote the rule."),
    new DataContent(memory, "application/pdf")
]));

Console.WriteLine(b);
b.Usage.Print();
Out.Separator();

// The document is not remembered between calls unless you attach it again.
Out.Step("C. Follow-up in a session - note the document must be re-sent or kept in the session");
AgentSession session = await agent.CreateSessionAsync();

await agent.RunAsync(new ChatMessage(ChatRole.User,
[
    new TextContent("Here is our expense policy. Keep it in mind."),
    new DataContent(memory, "application/pdf")
]), session);

AgentResponse c = await agent.RunAsync("Is alcohol reimbursable?", session); // follow up
Console.WriteLine(c);
c.Usage.Print();
Out.Note("Input tokens stay high on the follow-up: the PDF is still in the session history.");
