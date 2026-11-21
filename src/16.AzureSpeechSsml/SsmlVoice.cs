using System.Security;
using System.Xml;
using System.Xml.Linq;

namespace AzureSpeechSsml;

/// <summary>
/// An SSML file with a {{text}} placeholder. The file is the voice - which speaker, how fast,
/// how low, where to pause. Fill(text) drops the agent's reply into it.
/// </summary>
public sealed class SsmlVoice
{
    public const string Placeholder = "{{text}}";

    private readonly string template;

    private SsmlVoice(string name, string template)
    {
        Name = name;
        this.template = template;
    }

    /// <summary>The file name without extension, e.g. "victorian-gentleman". Used for the .wav names.</summary>
    public string Name { get; }

    public static SsmlVoice Load(string path)
    {
        string template = File.ReadAllText(path);

        if (!template.Contains(Placeholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path} has no {Placeholder} placeholder - nowhere to put the text.");
        }

        // Catch a broken file now, with a line number, rather than as a vague synthesis error later.
        try
        {
            XDocument.Parse(template);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"{path} is not valid XML: {ex.Message}", ex);
        }

        return new SsmlVoice(Path.GetFileNameWithoutExtension(path), template);
    }

    /// <summary>
    /// The SSML to speak. The text is escaped, so a reply with "&amp;" or "&lt;" can't break the XML -
    /// and can't inject SSML of its own.
    /// </summary>
    public string Fill(string text) => template.Replace(Placeholder, SecurityElement.Escape(text), StringComparison.Ordinal);
}
