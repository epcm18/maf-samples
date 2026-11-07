// C# port of the guide's Layer 1 TrustProxy (Python: mcp-trust-proxy). .NET AGT 5.0.0 has no TrustProxy,
// so this one is built on the AGT pieces that do exist: AgentIdentity for the DID and capabilities,
// RateLimiter for the per-minute limit. Six checks, first failure wins.

using AgentGovernance.RateLimiting;
using AgentGovernance.Trust;

namespace McpTrustProxy;

/// <summary>
/// Per-tool rules. MinTrust null = fall back to the proxy's defaultMinTrust - the guide leaves that
/// case ambiguous, this port picks the fallback. MaxCallsPerMinute 0 = unlimited.
/// </summary>
public sealed record ToolPolicy(
    int? MinTrust = null,
    string[]? RequiredCapabilities = null,
    bool Blocked = false,
    int MaxCallsPerMinute = 0,
    string Description = "");

public sealed record AuthResult(bool Allowed, string ToolName, string? AgentDid, string Reason, int TrustScore, DateTimeOffset Timestamp);

public sealed class TrustProxy(
    int defaultMinTrust,
    Dictionary<string, ToolPolicy> toolPolicies,
    IEnumerable<string>? blockedDids = null,
    bool requireDid = true)
{
    private readonly HashSet<string> _blockedDids = [.. blockedDids ?? []];
    private readonly RateLimiter _rateLimiter = new();
    private readonly List<AuthResult> _audit = [];

    /// <summary>Decides one tool call. trustScore is 0-1000, as in the guide - where it comes from is Layer 2's job.</summary>
    public AuthResult Authorize(AgentIdentity? caller, int trustScore, string toolName)
    {
        string? denial = Check(caller, trustScore, toolName, useRateLimit: true);

        AuthResult result = new(denial is null, toolName, caller?.Did, denial ?? "all checks passed", trustScore, DateTimeOffset.UtcNow);
        _audit.Add(result);
        return result;
    }

    /// <summary>
    /// Checks 1-5 only: nothing logged, no rate-limit allowance used. Not in the guide - it is for deciding
    /// which tools to hand an agent in the first place (13.2.3) rather than judging a call.
    /// </summary>
    public bool Permits(AgentIdentity? caller, int trustScore, string toolName) =>
        Check(caller, trustScore, toolName, useRateLimit: false) is null;

    private string? Check(AgentIdentity? caller, int trustScore, string toolName, bool useRateLimit)
    {
        ToolPolicy policy = toolPolicies.GetValueOrDefault(toolName) ?? new ToolPolicy();
        int minTrust = policy.MinTrust ?? defaultMinTrust;
        string? did = caller?.Did;

        return
            requireDid && string.IsNullOrEmpty(did) ? "no agent DID supplied" :
            did is not null && _blockedDids.Contains(did) ? $"agent {did} is blocked" :
            policy.Blocked ? $"tool {toolName} is blocked" :
            trustScore < minTrust ? $"trust {trustScore} is below {minTrust}" :
            MissingCapability(caller, policy) is { } missing ? $"missing capability '{missing}'" :
            // Checked last, so only calls that passed everything else use up the allowance.
            // Keyed per agent and tool - the guide doesn't say which; this is AGT RateLimiter's own convention.
            useRateLimit && policy.MaxCallsPerMinute > 0 && !_rateLimiter.TryAcquire($"{did}:{toolName}", policy.MaxCallsPerMinute, TimeSpan.FromMinutes(1))
                ? $"rate limit {policy.MaxCallsPerMinute}/minute reached"
                : null;
    }

    public void BlockAgent(string did) => _blockedDids.Add(did);
    public void UnblockAgent(string did) => _blockedDids.Remove(did);
    public void SetToolPolicy(string toolName, ToolPolicy policy) => toolPolicies[toolName] = policy;

    public IReadOnlyList<AuthResult> AuditLog => _audit;
    public (int Total, int Allowed, int Denied) Stats => (_audit.Count, _audit.Count(a => a.Allowed), _audit.Count(a => !a.Allowed));

    // HasCapability also accepts prefix wildcards: an identity holding "read:*" passes a "read:reports" requirement.
    private static string? MissingCapability(AgentIdentity? caller, ToolPolicy policy) =>
        policy.RequiredCapabilities?.FirstOrDefault(c => caller is null || !caller.HasCapability(c));
}
