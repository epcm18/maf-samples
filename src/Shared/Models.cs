namespace Shared;

/// <summary>Model names used across the samples - change here, applies everywhere.</summary>
public static class Models
{
    public const string Cheap = "gpt-4.1-mini";
    public const string Standard = "gpt-4.1";
    public const string Reasoning = "gpt-5-mini";

    /// <summary>Decisions API has its own model family. Chat models are rejected by /decisions.</summary>
    public const string Decision = "gpt-6-luna";
}
