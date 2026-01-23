using System.Numerics;
using Xunit;

namespace TrustAnchor.Tests;

public class ConfigValidationTests
{
    [Fact]
    public void FinalizeConfig_rejects_weight_sum_not_21()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 10);
        fx.SetAgentConfig(1, fx.AgentCandidate(1), 10);
        fx.SetRemainingAgentConfigs(2, weight: 0);

        Assert.ThrowsAny<System.Exception>(() => fx.FinalizeConfig());
    }

    [Fact]
    public void FinalizeConfig_rejects_duplicate_candidates()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        var candidate = fx.AgentCandidate(0);
        fx.SetAgentConfig(0, candidate, 11);
        fx.SetAgentConfig(1, candidate, 10);
        fx.SetRemainingAgentConfigs(2, weight: 0);

        Assert.ThrowsAny<System.Exception>(() => fx.FinalizeConfig());
    }

    [Fact]
    public void FinalizeConfig_rejects_missing_agent_configs()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);

        Assert.ThrowsAny<System.Exception>(() => fx.FinalizeConfig());
    }

    [Fact]
    public void FinalizeConfig_accepts_valid_config()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);
        fx.SetRemainingAgentConfigs(1, weight: 0);

        fx.FinalizeConfig();
    }
}
