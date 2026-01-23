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
    }

    [Fact]
    public void Neo_deposit_increases_stake_and_totalstake()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAgent(0, fixture.OwnerHash);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);
        Assert.Equal(new BigInteger(2_0000_0000), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.Equal(new BigInteger(2_0000_0000), fixture.Call<BigInteger>("totalStake"));
    }

    [Fact]
    public void Gas_reward_accrues_and_can_be_claimed()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAgent(0, fixture.OwnerHash);
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
    public void Withdraw_reduces_stake_and_transfers_neo_from_agent()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAgent(0, fixture.AgentHash);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 3);

        var before = fixture.NeoBalance(fixture.UserHash);
        fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 1);
        var after = fixture.NeoBalance(fixture.UserHash);

        Assert.Equal(new BigInteger(2_0000_0000), fixture.Call<BigInteger>("stakeOf", fixture.UserHash));
        Assert.True(after > before);
        Assert.Equal(fixture.UserHash, fixture.AgentLastTransferTo());
        Assert.Equal(new BigInteger(1), fixture.AgentLastTransferAmount());
    }

    [Fact]
    public void TrigVote_requires_strategist_and_whitelisted_candidate()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAgent(0, fixture.AgentHash);
        var candidate = fixture.OwnerPubKey;

        AssertFault(() => fixture.CallFrom(fixture.StrangerHash, "trigVote", 0, candidate));

        fixture.AllowCandidate(candidate);
        fixture.SetStrategist(fixture.StrategistHash);
        fixture.CallFrom(fixture.StrategistHash, "trigVote", 0, candidate);

        Assert.Equal(candidate, fixture.AgentLastVote());
    }

    [Fact]
    public void Withdraw_over_balance_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAgent(0, fixture.AgentHash);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 3));
    }

    [Fact]
    public void Withdraw_zero_amount_faults()
    {
        var fixture = new TrustAnchorFixture();
        fixture.SetAgent(0, fixture.AgentHash);
        fixture.MintNeo(fixture.UserHash, 5);
        fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

        AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 0));
    }
}
