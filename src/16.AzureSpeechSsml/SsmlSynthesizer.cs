using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AzureSpeechSsml;

/// <summary>SSML in, .wav file out, through the Speech SDK. Not real time: the whole file is written, then we return.</summary>
public sealed class SsmlSynthesizer(SpeechSettings settings)
{
    public async Task<string> SaveWavAsync(string ssml, string name)
    {
        string folder = Path.GetFullPath(settings.OutputFolder);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.wav");

        SpeechConfig config = SpeechConfig.FromEndpoint(new Uri(settings.Endpoint), settings.ApiKey);

        // The synthesizer holds the file open until it is disposed, so dispose before returning the path.
        using (AudioConfig audio = AudioConfig.FromWavFileOutput(file))
        using (SpeechSynthesizer synthesizer = new(config, audio))
        {
            using SpeechSynthesisResult result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.Canceled)
            {
                SpeechSynthesisCancellationDetails details = SpeechSynthesisCancellationDetails.FromResult(result);
                throw new InvalidOperationException($"Synthesis failed ({details.Reason}): {details.ErrorDetails}");
            }

            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
            {
                throw new InvalidOperationException($"Synthesis did not complete: {result.Reason}");
            }
        }

        return file;
    }
}
