// Give an agent a voice with an SSML file.
//
// Two halves make the character:
//   - the agent's instructions decide WHAT it says (an old gentleman out of a Victorian novel);
//   - an SSML file decides how it SOUNDS (voice, speed, pitch, pauses) - Voices/victorian-gentleman.xml.
//
// You type, the agent replies in character, the reply goes into the SSML file's {{text}}
// placeholder, and the Speech SDK writes a .wav to ./output. Not real time: one file per reply.
//
// Try another voice: copy the .xml, change it, and pass its path:
//   dotnet run --project src/16.AzureSpeechSsml -- path/to/my-voice.xml
//
// Setup: fill AzureSpeech in appsettings.json (or user-secrets, see SpeechSettings).

using AzureSpeechSsml;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using Shared;

Out.Init("16 - Azure Speech SSML (an agent with a voice)");

try
{
    SpeechSettings settings = SpeechSettings.Load();
    string voicePath = args.Length > 0
        ? Path.GetFullPath(args[0])
        : Path.Combine(AppContext.BaseDirectory, "Voices", "victorian-gentleman.xml");
    SsmlVoice voice = SsmlVoice.Load(voicePath);
    SsmlSynthesizer synthesizer = new(settings);

    AIAgent agent = Clients.Chat()
        .AsAIAgent(
            name: "Bartholomew",
            instructions: "You are Bartholomew, an elderly English gentleman straight out of a Victorian novel. " +
                          "You speak in a thick, old-fashioned British manner: 'I say', 'my dear fellow', " +
                          "'upon my word', 'most peculiar', 'by Jove'. You are courteous, a touch long-winded and " +
                          "fond of a good anecdote. Reply in two to four sentences of plain spoken text - no " +
                          "asterisks, stage directions, emojis or lists, because every word is read aloud.");

    AgentSession session = await agent.CreateSessionAsync();

    Out.Note($"Voice file: {voicePath}");
    Out.Note("Type a message and press Enter. Empty line or '/exit' quits.");

    while (true)
    {
        Out.Separator();
        Console.Write("> ");
        string input = Console.ReadLine() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input) || input.Equals("/exit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        AgentResponse response = await agent.RunAsync(input, session);
        Out.Step($"{agent.Name}:");
        Console.WriteLine(response.Text);

        string ssml = voice.Fill(response.Text);
        Out.Note("SSML sent to Azure Speech:");
        Out.Note(ssml);

        string file = await synthesizer.SaveWavAsync(ssml, voice.Name);
        Out.Step($"Saved {file}");
    }
}
catch (Exception ex)
{
    Out.Warn($"{ex.GetType().Name}: {ex.Message}");
}
