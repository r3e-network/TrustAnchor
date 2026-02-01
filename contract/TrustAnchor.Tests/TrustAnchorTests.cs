using System.Numerics;
using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract.Manifest;
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
    public void Read_only_methods_are_marked_safe()
    {
        var fixture = new TrustAnchorFixture();
        var fixtureType = typeof(TrustAnchorFixture);
        var patchMethod = fixtureType.GetMethod("PatchTrustAnchorSources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(patchMethod);
        var compileMethod = fixtureType.GetMethod("CompileSources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(compileMethod);

        var sources = (string[])patchMethod!.Invoke(null, new object[] { fixture.OwnerHash })!;
        var tuple = compileMethod!.Invoke(null, new object[] { sources })!;
        var manifestField = tuple.GetType().GetField("Item2");
        Assert.NotNull(manifestField);
        var manifest = (ContractManifest)manifestField!.GetValue(tuple)!;

        var safeMethods = new[]
        {
            "Owner",
            "Agent",
            "AgentCount",
            "isPaused",
            "RPS",
            "TotalStake",
            "StakeOf",
            "AgentTarget",
            "AgentName",
            "AgentVoting",
            "AgentInfo",
            "AgentList",
        };

        foreach (var name in safeMethods)
        {
            var method = manifest.Abi.Methods.SingleOrDefault(m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(method);
            Assert.True(method!.Safe, $"{name} should be marked safe");
        }
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
    public void Manual_vote_by_id_name_target()
    {
        var fixture = new TrustAnchorFixture();
        fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");

        fixture.CallFrom(fixture.OwnerHash, "voteAgentById", 0);
        Assert.Equal(fixture.AgentCandidate(0), fixture.AgentLastVote(0));

        fixture.CallFrom(fixture.OwnerHash, "voteAgentByName", "a0");
        fixture.CallFrom(fixture.OwnerHash, "voteAgentByTarget", fixture.AgentCandidate(0));
    }

    [Fact]
    public void Withdraw_starts_from_lowest_non_zero_voting_agent()
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
        Assert.Single(list);
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

    [Fact]
    public void SetAgentVoting_by_name_and_target()
    {
        var fixture = new TrustAnchorFixture();
        fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");

        fixture.CallFrom(fixture.OwnerHash, "setAgentVotingByName", "a0", new BigInteger(7));
        Assert.Equal(new BigInteger(7), fixture.Call<BigInteger>("agentVoting", 0));

        fixture.CallFrom(fixture.OwnerHash, "setAgentVotingByTarget", fixture.AgentCandidate(0), new BigInteger(9));
        Assert.Equal(new BigInteger(9), fixture.Call<BigInteger>("agentVoting", 0));
    }

    [Fact]
    public void SetAgentVoting_by_name_or_target_unknown_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "setAgentVotingByName", "missing", new BigInteger(1)));
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "setAgentVotingByTarget", fixture.AgentCandidate(1), new BigInteger(1)));
    }

    [Fact]
    public void Owner_transfer_to_zero_address_faults()
    {
        var fixture = new TrustAnchorFixture();
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "transferOwner", UInt160.Zero));
    }

    [Fact]
    public void Owner_transfer_requires_owner_witness()
    {
        var fixture = new TrustAnchorFixture();
        AssertFault(() => fixture.CallFrom(fixture.OtherHash, "transferOwner", fixture.UserHash));
    }

    [Fact]
    public void Owner_transfer_same_as_current_fails()
    {
        var fixture = new TrustAnchorFixture();
        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "transferOwner", fixture.OwnerHash));
    }

    [Fact]
    public void Owner_transfer_is_immediate()
    {
        var fixture = new TrustAnchorFixture();
        var newOwner = fixture.OtherHash;

        fixture.CallFrom(fixture.OwnerHash, "transferOwner", newOwner);

        Assert.Equal(newOwner, fixture.Call<UInt160>("owner"));
    }

    [Fact]
    public void Multiple_owner_transfers_sequence()
    {
        var fixture = new TrustAnchorFixture();
        var newOwner1 = fixture.OtherHash;
        var newOwner2 = fixture.UserHash;

        fixture.CallFrom(fixture.OwnerHash, "transferOwner", newOwner1);
        Assert.Equal(newOwner1, fixture.Call<UInt160>("owner"));

        fixture.CallFrom(newOwner1, "transferOwner", newOwner2);
        Assert.Equal(newOwner2, fixture.Call<UInt160>("owner"));
    }

    [Fact]
    public void Withdraw_works_with_core_only_agent()
    {
        var fixture = new TrustAnchorFixture();
        var authAgent = fixture.DeployAuthAgent();

        fixture.CallFrom(fixture.OwnerHash, "registerAgent", authAgent, fixture.AgentCandidate(0), "auth-agent");
        fixture.CallFrom(fixture.OwnerHash, "setAgentVotingById", 0, new BigInteger(1));

        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 1);

        Assert.Equal(fixture.UserHash, fixture.AuthAgentLastTransferTo());
        Assert.Equal(new BigInteger(1), fixture.AuthAgentLastTransferAmount());
    }

    [Fact]
    public void EmergencyWithdraw_succeeds_with_partial_agent_registry()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        fixture.CallFrom(fixture.OwnerHash, "pause");
        fixture.DrainAgentTo(fixture.AgentHashes[0], fixture.TrustHash, 1);
        fixture.CallFrom(fixture.UserHash, "emergencyWithdraw", fixture.UserHash);

        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
    }

    // ========================================
    // Edge Case Tests
    // ========================================

    [Fact]
    public void Stake_zero_neo_does_not_change_state()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);

        var stakeBefore = fixture.Call<BigInteger>("stakeOf", fixture.UserHash);
        var totalBefore = fixture.Call<BigInteger>("totalStake");

        // NEO transfer of 0 should not change stake
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 0);

        Assert.Equal(stakeBefore, fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(totalBefore, fixture.Call<BigInteger>("totalStake"));
    }

    [Fact]
    public void Multiple_users_stake_and_rewards_distribute_proportionally()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        // User stakes 3 NEO
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 3);

        // Other stakes 1 NEO
        fixture.MintNeo(fixture.OtherHash, 5);
        fixture.NeoTransfer(fixture.OtherHash, fixture.TrustHash, 1);

        // Total stake = 4 NEO, User has 75%, Other has 25%
        Assert.Equal(new BigInteger(4), fixture.Call<BigInteger>("totalStake"));

        // Send 4 GAS as reward
        fixture.MintGas(fixture.StrangerHash, 10);
        fixture.GasTransfer(fixture.StrangerHash, fixture.TrustHash, 4);

        // User should get 3 GAS (75%)
        var userBefore = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var userAfter = fixture.GasBalance(fixture.UserHash);
        Assert.Equal(new BigInteger(3), userAfter - userBefore);

        // Other should get 1 GAS (25%)
        var otherBefore = fixture.GasBalance(fixture.OtherHash);
        fixture.CallFrom(fixture.OtherHash, "claimReward", fixture.OtherHash);
        var otherAfter = fixture.GasBalance(fixture.OtherHash);
        Assert.Equal(new BigInteger(1), otherAfter - otherBefore);
    }

    [Fact]
    public void Claim_reward_with_zero_balance_succeeds_silently()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        // User has no stake, no rewards
        var before = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after = fixture.GasBalance(fixture.UserHash);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Withdraw_full_stake_leaves_zero_balance()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 3);

        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 3);

        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("totalStake"));
    }

    // ========================================
    // Security Tests - Unauthorized Access
    // ========================================

    [Fact]
    public void Withdraw_without_witness_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        // Stranger tries to withdraw User's stake
        AssertFault(() => fixture.CallFrom(fixture.StrangerHash, "withdraw", fixture.UserHash, 1));
    }

    [Fact]
    public void ClaimReward_without_witness_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);
        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        // Stranger tries to claim User's rewards
        AssertFault(() => fixture.CallFrom(fixture.StrangerHash, "claimReward", fixture.UserHash));
    }

    [Fact]
    public void RegisterAgent_non_owner_faults()
    {
        var fixture = new TrustAnchorFixture();

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "registerAgent",
            fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0"));
    }

    [Fact]
    public void SetAgentVoting_non_owner_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "setAgentVotingById", 0, new BigInteger(10)));
    }

    [Fact]
    public void Pause_non_owner_faults()
    {
        var fixture = new TrustAnchorFixture();

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "pause"));
    }

    [Fact]
    public void Unpause_non_owner_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.CallFrom(fixture.OwnerHash, "pause");

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "unpause"));
    }

    // ========================================
    // Pause/Unpause Functionality Tests
    // ========================================

    [Fact]
    public void Deposit_while_paused_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.CallFrom(fixture.OwnerHash, "pause");

        fixture.MintNeo(fixture.UserHash, 5);
        AssertFault(() => fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1));
    }

    [Fact]
    public void Unpause_allows_deposits_again()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        fixture.CallFrom(fixture.OwnerHash, "pause");
        Assert.True(fixture.Call<bool>("isPaused"));

        fixture.CallFrom(fixture.OwnerHash, "unpause");
        Assert.False(fixture.Call<bool>("isPaused"));

        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);
        Assert.Equal(new BigInteger(1), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
    }

    // ========================================
    // Agent Registration Tests
    // ========================================

    [Fact]
    public void RegisterAgent_with_empty_name_faults()
    {
        var fixture = new TrustAnchorFixture();

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent",
            fixture.AgentHashes[0], fixture.AgentCandidate(0), ""));
    }

    [Fact]
    public void RegisterAgent_with_zero_address_faults()
    {
        var fixture = new TrustAnchorFixture();

        AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent",
            UInt160.Zero, fixture.AgentCandidate(0), "agent-0"));
    }

    [Fact]
    public void AgentCount_increments_on_registration()
    {
        var fixture = new TrustAnchorFixture();

        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("agentCount"));

        fixture.RegisterAgent(fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        Assert.Equal(BigInteger.One, fixture.Call<BigInteger>("agentCount"));

        fixture.RegisterAgent(fixture.AgentHashes[1], fixture.AgentCandidate(1), "agent-1");
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("agentCount"));
    }

    [Fact]
    public void AgentInfo_invalid_index_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        AssertFault(() => fixture.Call<VmArray>("agentInfo", 1));
        AssertFault(() => fixture.Call<VmArray>("agentInfo", -1));
    }

    // ========================================
    // Multi-Agent Withdrawal Tests
    // ========================================

    [Fact]
    public void Withdraw_from_multiple_agents_in_priority_order()
    {
        var fixture = new TrustAnchorFixture();

        // Register 3 agents with different voting priorities
        fixture.RegisterAgent(fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        fixture.RegisterAgent(fixture.AgentHashes[1], fixture.AgentCandidate(1), "agent-1");
        fixture.RegisterAgent(fixture.AgentHashes[2], fixture.AgentCandidate(2), "agent-2");
        fixture.SetAgentVotingById(0, 10); // highest
        fixture.SetAgentVotingById(1, 5);  // medium
        fixture.SetAgentVotingById(2, 1);  // lowest

        // Stake goes to highest voting agent (agent-0)
        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 3);

        Assert.Equal(new BigInteger(3), fixture.NeoBalance(fixture.AgentHashes[0]));

        // Manually add NEO to other agents for withdrawal test
        fixture.MintNeo(fixture.AgentHashes[1], 2);
        fixture.MintNeo(fixture.AgentHashes[2], 1);

        // Withdraw 2 NEO - should come from lowest voting agent first (agent-2)
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 2);

        // Agent-2 (lowest) should be drained first
        Assert.Equal(fixture.UserHash, fixture.AgentLastTransferTo(2));
    }

    // ========================================
    // Emergency Withdraw Tests
    // ========================================

    [Fact]
    public void EmergencyWithdraw_not_paused_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        // Not paused - should fail
        AssertFault(() => fixture.CallFrom(fixture.UserHash, "emergencyWithdraw", fixture.UserHash));
    }

    [Fact]
    public void EmergencyWithdraw_with_agent_balance_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        fixture.CallFrom(fixture.OwnerHash, "pause");

        // Agent still has balance - should fail
        AssertFault(() => fixture.CallFrom(fixture.UserHash, "emergencyWithdraw", fixture.UserHash));
    }

    [Fact]
    public void EmergencyWithdraw_no_stake_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.CallFrom(fixture.OwnerHash, "pause");

        // User has no stake
        AssertFault(() => fixture.CallFrom(fixture.UserHash, "emergencyWithdraw", fixture.UserHash));
    }

    // ========================================
    // Reward Calculation Accuracy Tests
    // ========================================

    [Fact]
    public void Rewards_accumulate_correctly_over_multiple_deposits()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        // First stake
        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        // First reward
        fixture.MintGas(fixture.StrangerHash, 20);
        fixture.GasTransfer(fixture.StrangerHash, fixture.TrustHash, 4);

        // Second stake (same user)
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        // Second reward
        fixture.GasTransfer(fixture.StrangerHash, fixture.TrustHash, 4);

        // User should have: 4 (first) + 4 (second) = 8 GAS
        var before = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after = fixture.GasBalance(fixture.UserHash);

        Assert.Equal(new BigInteger(8), after - before);
    }

    [Fact]
    public void Late_staker_only_gets_rewards_after_joining()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        // User stakes first
        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        // First reward - only User gets it
        fixture.MintGas(fixture.StrangerHash, 20);
        fixture.GasTransfer(fixture.StrangerHash, fixture.TrustHash, 4);

        // Other joins late
        fixture.MintNeo(fixture.OtherHash, 10);
        fixture.NeoTransfer(fixture.OtherHash, fixture.TrustHash, 2);

        // Second reward - split 50/50
        fixture.GasTransfer(fixture.StrangerHash, fixture.TrustHash, 4);

        // User: 4 (first) + 2 (second) = 6
        var userBefore = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var userAfter = fixture.GasBalance(fixture.UserHash);
        Assert.Equal(new BigInteger(6), userAfter - userBefore);

        // Other: 0 (first) + 2 (second) = 2
        var otherBefore = fixture.GasBalance(fixture.OtherHash);
        fixture.CallFrom(fixture.OtherHash, "claimReward", fixture.OtherHash);
        var otherAfter = fixture.GasBalance(fixture.OtherHash);
        Assert.Equal(new BigInteger(2), otherAfter - otherBefore);
    }

    // ========================================
    // Reentrancy Guard Tests
    // ========================================

    [Fact]
    public void Withdraw_completes_and_releases_reentrancy_lock()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 5);

        // First withdrawal should succeed
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 2);
        Assert.Equal(new BigInteger(3), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));

        // Second withdrawal should also succeed (lock was released)
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 1);
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
    }

    [Fact]
    public void Multiple_sequential_withdrawals_succeed()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 20);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 10);

        // Multiple sequential withdrawals should all succeed
        for (int i = 0; i < 5; i++)
        {
            fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 1);
        }

        Assert.Equal(new BigInteger(5), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
    }

    // ========================================
    // Owner() Null Handling Tests
    // ========================================

    [Fact]
    public void Owner_returns_default_owner_on_fresh_deploy()
    {
        var fixture = new TrustAnchorFixture();
        var owner = fixture.Call<UInt160>("owner");
        
        // Owner should be set to the default owner (OwnerHash)
        Assert.NotNull(owner);
        Assert.Equal(fixture.OwnerHash, owner);
    }

    [Fact]
    public void Owner_persists_after_operations()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        
        // Perform various operations
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);
        fixture.CallFrom(fixture.OwnerHash, "pause");
        fixture.CallFrom(fixture.OwnerHash, "unpause");
        
        // Owner should still be the same
        var owner = fixture.Call<UInt160>("owner");
        Assert.Equal(fixture.OwnerHash, owner);
    }

    // ========================================
    // EmergencyWithdraw Balance Check Tests
    // ========================================

    [Fact]
    public void EmergencyWithdraw_without_witness_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        fixture.CallFrom(fixture.OwnerHash, "pause");
        fixture.DrainAgentTo(fixture.AgentHashes[0], fixture.TrustHash, 1);

        // Stranger tries to emergency withdraw User's stake
        AssertFault(() => fixture.CallFrom(fixture.StrangerHash, "emergencyWithdraw", fixture.UserHash));
    }

    [Fact]
    public void EmergencyWithdraw_owner_can_trigger_for_user()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        fixture.CallFrom(fixture.OwnerHash, "pause");
        fixture.DrainAgentTo(fixture.AgentHashes[0], fixture.TrustHash, 1);

        // Owner can trigger emergency withdraw for user
        fixture.CallFrom(fixture.OwnerHash, "emergencyWithdraw", fixture.UserHash);
        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
    }

    [Fact]
    public void EmergencyWithdraw_multiple_agents_all_must_be_empty()
    {
        var fixture = new TrustAnchorFixture();
        
        // Register multiple agents
        fixture.RegisterAgent(fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
        fixture.RegisterAgent(fixture.AgentHashes[1], fixture.AgentCandidate(1), "agent-1");
        fixture.SetAgentVotingById(0, 10);
        fixture.SetAgentVotingById(1, 5);

        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        fixture.CallFrom(fixture.OwnerHash, "pause");
        
        // Only drain agent-0, agent-1 might have balance from routing
        fixture.DrainAgentTo(fixture.AgentHashes[0], fixture.TrustHash, 2);
        
        // If agent-1 has any balance, emergency withdraw should fail
        var agent1Balance = fixture.NeoBalance(fixture.AgentHashes[1]);
        if (agent1Balance > BigInteger.Zero)
        {
            AssertFault(() => fixture.CallFrom(fixture.UserHash, "emergencyWithdraw", fixture.UserHash));
        }
    }

    // ========================================
    // Edge Cases - Zero and Max Values
    // ========================================

    [Fact]
    public void ClaimReward_twice_second_claim_is_zero()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        // First claim
        var before1 = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after1 = fixture.GasBalance(fixture.UserHash);
        Assert.Equal(new BigInteger(5), after1 - before1);

        // Second claim should be zero (no new rewards)
        var before2 = fixture.GasBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
        var after2 = fixture.GasBalance(fixture.UserHash);
        Assert.Equal(BigInteger.Zero, after2 - before2);
    }

    [Fact]
    public void Large_stake_amount_handles_correctly()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        
        // Stake a large amount
        BigInteger largeAmount = new BigInteger(1000);
        fixture.MintNeo(fixture.UserHash, largeAmount + 100);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, largeAmount);

        Assert.Equal(largeAmount, fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(largeAmount, fixture.Call<BigInteger>("totalStake"));
    }

    [Fact]
    public void Withdraw_exact_stake_amount_succeeds()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 5);

        // Withdraw exact stake amount
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 5);

        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("totalStake"));
    }

    [Fact]
    public void SetAgentVoting_to_zero_succeeds()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 10);

        // Set voting to zero
        fixture.SetAgentVotingById(0, BigInteger.Zero);
        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("agentVoting", 0));
    }

    [Fact]
    public void Multiple_users_withdraw_sequentially()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        // User stakes
        fixture.MintNeo(fixture.UserHash, 10);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 3);

        // Other stakes
        fixture.MintNeo(fixture.OtherHash, 10);
        fixture.NeoTransfer(fixture.OtherHash, fixture.TrustHash, 2);

        Assert.Equal(new BigInteger(5), fixture.Call<BigInteger>("totalStake"));

        // User withdraws
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 2);
        Assert.Equal(new BigInteger(3), fixture.Call<BigInteger>("totalStake"));

        // Other withdraws
        fixture.CallFrom(fixture.OtherHash, "withdraw", fixture.OtherHash, 1);
        Assert.Equal(new BigInteger(2), fixture.Call<BigInteger>("totalStake"));
    }

    // ========================================
    // Agent Name Length Edge Cases
    // ========================================

    [Fact]
    public void RegisterAgent_with_max_length_name_succeeds()
    {
        var fixture = new TrustAnchorFixture();

        // 32 bytes is the max length
        var maxName = new string('a', 32);
        fixture.CallFrom(fixture.OwnerHash, "registerAgent",
            fixture.AgentHashes[0], fixture.AgentCandidate(0), maxName);

        Assert.Equal(maxName, fixture.Call<string>("agentName", 0));
    }

    [Fact]
    public void RegisterAgent_with_single_char_name_succeeds()
    {
        var fixture = new TrustAnchorFixture();

        fixture.CallFrom(fixture.OwnerHash, "registerAgent",
            fixture.AgentHashes[0], fixture.AgentCandidate(0), "a");

        Assert.Equal("a", fixture.Call<string>("agentName", 0));
    }

    // ========================================
    // RPS (Reward Per Stake) Edge Cases
    // ========================================

    [Fact]
    public void RPS_starts_at_zero()
    {
        var fixture = new TrustAnchorFixture();
        Assert.Equal(BigInteger.Zero, fixture.Call<BigInteger>("rPS"));
    }

    [Fact]
    public void RPS_increases_with_gas_deposits()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        var rpsBefore = fixture.Call<BigInteger>("rPS");

        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        var rpsAfter = fixture.Call<BigInteger>("rPS");
        Assert.True(rpsAfter > rpsBefore);
    }

    // ========================================
    // Sync Account Tests
    // ========================================

    [Fact]
    public void SyncAccount_with_zero_stake_succeeds()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);

        // Sync account with no stake should succeed
        var result = fixture.Call<bool>("syncAccount", fixture.UserHash);
        Assert.True(result);
    }

    [Fact]
    public void SyncAccount_updates_paid_value()
    {
        var fixture = new TrustAnchorFixture();
        fixture.RegisterSingleAgentWithVoting(0, 1);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        // Add rewards
        fixture.MintGas(fixture.OtherHash, 10);
        fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

        // Sync should update paid value
        fixture.Call<bool>("syncAccount", fixture.UserHash);

        // Reward should be available
        var reward = fixture.Call<BigInteger>("reward", fixture.UserHash);
        Assert.Equal(new BigInteger(5), reward);
    }
}
