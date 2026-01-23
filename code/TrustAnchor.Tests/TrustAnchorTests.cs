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
}
