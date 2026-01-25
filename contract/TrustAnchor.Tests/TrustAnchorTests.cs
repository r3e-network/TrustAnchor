using System.Numerics;
using Neo;
using Neo.Cryptography.ECC;
using Neo.VM.Types;
using VmArray = Neo.VM.Types.Array;
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
    public void IsPaused_is_exposed_for_agents()
    {
        var fixture = new TrustAnchorFixture();
        var paused = fixture.Call<bool>("isPaused");
        Assert.False(paused);
    }

    [Fact]
    public void Neo_deposit_increases_stake_and_totalstake()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("totalStake"));
    }

    [Fact]
    public void Neo_deposit_uses_integer_neo_units()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

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
        fixture.RegisterSingleAgentWithVoting(0, 1);
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
        fixture.RegisterSingleAgentWithVoting(0, 1);
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
    public void Gas_before_first_stake_is_distributed_on_first_stake()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        var before = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after = fixture.GasBalance(fixture.UserHash);

        Assert.Equal(before + new BigInteger(5), after);
    }

    [Fact]
    public void Withdraw_reduces_stake_and_transfers_neo_from_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
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
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 3));
    }

    [Fact]
    public void Withdraw_zero_amount_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 0));
    }

    [Fact]
    public void Deposit_routes_to_highest_voting_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterAgent(fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        fixture.RegisterAgent(fixture.AgentHashes[1], fixture.AgentCandidate(1), "agent-1");
        fixture.SetAgentVotingById(0, 5);
        fixture.SetAgentVotingById(1, 7);

        fixture.MintNeo(fixture.UserHash, 3);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        Assert.Equal(new BigInteger(2), fixture.NeoBalance(fixture.AgentHashes[1]));
    }

    [Fact]
    public void Deposit_without_agents_faults()
    {
        var fixture = new TrustAnchorFixture();
        AssertFault(() => fixture.InvokeNeoPayment(fixture.UserHash, 1));
    }

    [Fact]
    public void Withdraw_starts_from_lowest_non_zero_weight_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterAgent(fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        fixture.RegisterAgent(fixture.AgentHashes[1], fixture.AgentCandidate(1), "agent-1");
        fixture.SetAgentVotingById(0, 20);
        fixture.SetAgentVotingById(1, 1);

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
        fixture.RegisterAgent(fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        fixture.RegisterAgent(fixture.AgentHashes[1], fixture.AgentCandidate(1), "agent-1");
        fixture.SetAgentVotingById(0, 10);
        fixture.SetAgentVotingById(1, 11);

        fixture.MintNeo(fixture.UserHash, 6);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 6);

        fixture.CallFrom(fixture.OwnerHash, "rebalanceVotes");

        Assert.Equal(fixture.AgentCandidate(0), fixture.AgentLastVote(0));
        Assert.Equal(fixture.AgentCandidate(1), fixture.AgentLastVote(1));
        Assert.Equal(new BigInteger(2), fixture.NeoBalance(fixture.AgentHashes[0]));
        Assert.Equal(new BigInteger(4), fixture.NeoBalance(fixture.AgentHashes[1]));
    }

    [Fact]
    public void WithdrawGAS_is_disabled()
    {
        var fixture = new TrustAnchorFixture();
        fixture.MintGas(fixture.OtherHash, 2);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 1);

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1)));

        fixture.CallFrom(fixture.OwnerHash, "pause");
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1)));
    }

    [Fact]
    public void AgentInfo_and_list_return_metadata()
    {
        var fixture = new TrustAnchorFixture();
        fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        fixture.CallFrom(fixture.OwnerHash, "setAgentVotingById", 0, new BigInteger(5));

        var info = fixture.Call<VmArray>("agentInfo", 0);
        Assert.Equal(new BigInteger(0), info[0].GetInteger());
        Assert.Equal(fixture.AgentHashes[0], new UInt160(info[1].GetSpan()));
        Assert.Equal(fixture.AgentCandidate(0).EncodePoint(true), info[2].GetSpan().ToArray());
        Assert.Equal("agent-0", info[3].GetString());
        Assert.Equal(new BigInteger(5), info[4].GetInteger());

        var list = fixture.Call<VmArray>("agentList");
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void RegisterAgent_enforces_name_length_and_uniqueness()
    {
        var fixture = new TrustAnchorFixture();
        var agent0 = fixture.AgentHashes[0];
        var agent1 = fixture.AgentHashes[1];
        var target0 = fixture.AgentCandidate(0);
        var target1 = fixture.AgentCandidate(1);

        fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent0, target0, "agent-0");

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent1, target1, "agent-0"));
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent1, target0, "agent-1"));

        var longName = new string('a', 33);
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent1, target1, longName));
    }

    [Fact]
    public void UpdateAgentName_and_target_enforce_uniqueness()
    {
        var fixture = new TrustAnchorFixture();
        fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");
        fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[1], fixture.AgentCandidate(1), "a1");

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "updateAgentNameById", 1, "a0"));
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "updateAgentTargetById", 1, fixture.AgentCandidate(0)));
    }
}
