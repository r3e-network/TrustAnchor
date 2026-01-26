using System;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class StakeNeoTests
{
    [Fact]
    public void Main_Throws_When_Wif_Missing()
    {
        using var _ = new TestEnvScope("WIF", null);
        using var __ = new TestEnvScope("TRUSTANCHOR", null);

        var type = Type.GetType("StakeNEO.Program, StakeNEO");
        Assert.NotNull(type);

        var main = type!.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(main);

        var ex = Assert.Throws<TargetInvocationException>(() => main!.Invoke(null, new object[] { Array.Empty<string>() }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("WIF", ex.InnerException!.Message);
    }

    [Fact]
    public void StakeOfMethodConstant_IsCorrect()
    {
        var type = Type.GetType("StakeNEO.Program, StakeNEO");
        Assert.NotNull(type);

        var field = type!.GetField("StakeOfMethodName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.Equal("stakeOf", field!.GetValue(null));
    }

    [Fact]
    public void TransferMethodConstant_IsCorrect()
    {
        var type = Type.GetType("StakeNEO.Program, StakeNEO");
        Assert.NotNull(type);

        var field = type!.GetField("TransferMethodName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.Equal("transfer", field!.GetValue(null));
    }
}
