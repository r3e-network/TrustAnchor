using Xunit;
using NeoBurger;

namespace TrustAnchor.Tests;

public class TrustAnchorTests
{
    [Fact]
    public void TrustAnchor_type_is_available()
    {
        Assert.NotNull(typeof(NeoBurger.TrustAnchor));
    }
}
