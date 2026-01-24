using System.Collections.Generic;
using System.Numerics;
using Neo.SmartContract;
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

    [Fact]
    public void FinalizeConfig_allows_target_only_update_after_prefill()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);
        fx.SetRemainingAgentConfigs(1, weight: 0);
        fx.FinalizeConfig();

        var newTarget = fx.AgentCandidate(22);
        fx.BeginConfig();
        fx.CallFrom(fx.OwnerHash, "setAgentTarget", new BigInteger(0), newTarget);
        fx.FinalizeConfig();

        Assert.Equal(newTarget, fx.Call<Neo.Cryptography.ECC.ECPoint>("agentTarget", new BigInteger(0)));
        Assert.Equal(new BigInteger(21), fx.Call<BigInteger>("agentWeight", new BigInteger(0)));
    }

    [Fact]
    public void FinalizeConfig_allows_weight_only_update_after_prefill()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 10);
        fx.SetAgentConfig(1, fx.AgentCandidate(1), 11);
        fx.SetRemainingAgentConfigs(2, weight: 0);
        fx.FinalizeConfig();

        fx.BeginConfig();
        fx.CallFrom(fx.OwnerHash, "setAgentWeight", new BigInteger(0), new BigInteger(9));
        fx.CallFrom(fx.OwnerHash, "setAgentWeight", new BigInteger(1), new BigInteger(12));
        fx.FinalizeConfig();

        Assert.Equal(new BigInteger(9), fx.Call<BigInteger>("agentWeight", new BigInteger(0)));
        Assert.Equal(new BigInteger(12), fx.Call<BigInteger>("agentWeight", new BigInteger(1)));
        Assert.Equal(fx.AgentCandidate(0), fx.Call<Neo.Cryptography.ECC.ECPoint>("agentTarget", new BigInteger(0)));
    }

    [Fact]
    public void FinalizeConfig_allows_weight_array_update_after_prefill()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 10);
        fx.SetAgentConfig(1, fx.AgentCandidate(1), 11);
        fx.SetRemainingAgentConfigs(2, weight: 0);
        fx.FinalizeConfig();

        var weightParameters = new List<ContractParameter>(21);
        for (int i = 0; i < 21; i++)
        {
            weightParameters.Add(new ContractParameter(ContractParameterType.Integer)
            {
                Value = BigInteger.Zero
            });
        }
        weightParameters[0].Value = new BigInteger(21);

        var weights = new ContractParameter(ContractParameterType.Array)
        {
            Value = weightParameters
        };

        fx.BeginConfig();
        fx.CallFrom(fx.OwnerHash, "setAgentWeights", weights);
        fx.FinalizeConfig();

        Assert.Equal(new BigInteger(21), fx.Call<BigInteger>("agentWeight", new BigInteger(0)));
        Assert.Equal(BigInteger.Zero, fx.Call<BigInteger>("agentWeight", new BigInteger(1)));
    }
}
