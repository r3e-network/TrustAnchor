using System.Numerics;
using Neo;
using Xunit;

namespace TrustAnchor.Tests;

public class TrustAnchorTests
{
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
}
