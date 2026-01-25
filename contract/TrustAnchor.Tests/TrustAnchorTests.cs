using System.Numerics;
using Neo;
using Xunit;

namespace TrustAnchor.Tests;

public class TrustAnchorTests
{
    private static void AssertFault(Action action)
    {
        Assert.ThrowsAny<Neo.SmartContract.Testing.Exceptions.TestException>(action);
    }

    [Fact]
    public void Deploys_contract_and_returns_owner()
    {
        var fixture = new TrustAnchorFixture();
        var owner = fixture.Call<UInt160>("owner");
        Assert.Equal(fixture.OwnerHash, owner);
        Assert.NotNull(typeof(TrustAnchor));
        Assert.NotNull(typeof(TrustAnchorAgent));
    }

    [Fact]
    public void Neo_deposit_increases_stake_and_totalstake()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("totalStake"));
    }

    [Fact]
    public void Neo_deposit_uses_integer_neo_units()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();

        BigInteger depositAmount = BigInteger.Parse("30");
        fixture.MintNeo(fixture.UserHash, depositAmount);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, depositAmount);

        BigInteger stake = fixture.Call<BigInteger>("stakeOf", fixture.UserHash);
        Assert.Equal(depositAmount, stake);
        Assert.NotEqual(depositAmount * BigInteger.Parse("100000000"), stake);
    }

    [Fact]
    public void Gas_reward_accrues_and_can_be_claimed()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        var before = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after = fixture.GasBalance(fixture.UserHash);

        Assert.True(after > before);
    }

    [Fact]
    public void Gas_reward_distributes_full_amount_for_single_staker()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        var before = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after = fixture.GasBalance(fixture.UserHash);

        Assert.Equal(before + new BigInteger(5), after);
    }

    [Fact]
    public void Withdraw_reduces_stake_and_transfers_neo_from_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 3);

        var before = fixture.NeoBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 1);
        var after = fixture.NeoBalance(fixture.UserHash);

        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.True(after > before);
        Assert.Equal(fixture.UserHash, fixture.AgentLastTransferTo());
        Assert.Equal(new BigInteger(1), fixture.AgentLastTransferAmount());
    }

    [Fact]
    public void Withdraw_over_balance_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 3));
    }

    [Fact]
    public void Withdraw_zero_amount_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 0));
    }

    [Fact]
    public void Deposit_routes_to_highest_weight_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 5);
        fixture.SetAgentConfig(1, fixture.AgentCandidate(1), 16);
        fixture.SetRemainingAgentConfigs(2, weight: 0);
        fixture.FinalizeConfig();

        fixture.MintNeo(fixture.UserHash, 3);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        Assert.Equal(new BigInteger(2), fixture.NeoBalance(fixture.AgentHashes[1]));
    }

    [Fact]
    public void Withdraw_starts_from_lowest_non_zero_weight_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 20);
        fixture.SetAgentConfig(1, fixture.AgentCandidate(1), 1);
        fixture.SetRemainingAgentConfigs(2, weight: 0);
        fixture.FinalizeConfig();

        fixture.MintNeo(fixture.UserHash, 2);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);
        fixture.MintNeo(fixture.AgentHashes[1], 1);

        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 1);

        Assert.Equal(fixture.UserHash, fixture.AgentLastTransferTo(1));
        Assert.Equal(new BigInteger(1), fixture.AgentLastTransferAmount(1));
    }

    [Fact]
    public void Rebalance_moves_neo_and_votes_per_weights()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 10);
        fixture.SetAgentConfig(1, fixture.AgentCandidate(1), 11);
        fixture.SetRemainingAgentConfigs(2, weight: 0);
        fixture.FinalizeConfig();

        fixture.MintNeo(fixture.UserHash, 6);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 6);

        fixture.RebalanceVotes();

        Assert.Equal(fixture.AgentCandidate(0), fixture.AgentLastVote(0));
        Assert.Equal(fixture.AgentCandidate(1), fixture.AgentLastVote(1));
        Assert.Equal(new BigInteger(2), fixture.NeoBalance(fixture.AgentHashes[0]));
        Assert.Equal(new BigInteger(4), fixture.NeoBalance(fixture.AgentHashes[1]));
    }

    [Fact]
    public void ClaimReward_with_reentrancy_guard()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAllAgents();
        fixture.BeginConfig();
        fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
        fixture.SetRemainingAgentConfigs(1, weight: 0);
        fixture.FinalizeConfig();

        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 5);

        fixture.MintGas(fixture.OtherHash, 100);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 50);

        var balanceBefore = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var balanceAfter = fixture.GasBalance(fixture.UserHash);

        Assert.True(balanceAfter > balanceBefore, "User should receive GAS rewards");

        var rewardAfterFirstClaim = fixture.Call<BigInteger>("reward", fixture.UserHash);
        Assert.Equal(BigInteger.Zero, rewardAfterFirstClaim);

        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var rewardAfterSecondClaim = fixture.Call<BigInteger>("reward", fixture.UserHash);
        Assert.Equal(BigInteger.Zero, rewardAfterSecondClaim);
    }

    [Fact]
    public void WithdrawGAS_requires_pause()
    {
        var fixture = new TrustAnchorFixture();
        fixture.MintGas(fixture.OtherHash, 2);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 1);

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1)));

        fixture.CallFrom(fixture.OwnerHash, "pause");
        fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1));
    }
}
