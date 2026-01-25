using System;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class LibWalletTests
{
    [Fact]
    public void Main_Throws_WithFriendlyMessage_When_Wif_Missing()
    {
        using var _ = new TestEnvScope("WIF", null);

        var type = Type.GetType("LibWallet.Program, LibWallet");
        Assert.NotNull(type);

        var main = type!.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(main);

        var ex = Assert.Throws<TargetInvocationException>(() => main!.Invoke(null, new object[] { Array.Empty<string>() }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("WIF", ex.InnerException!.Message);
    }
}
