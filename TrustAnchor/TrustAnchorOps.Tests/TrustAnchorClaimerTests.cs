using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class TrustAnchorClaimerTests
{
    [Fact]
    public void ParseMod_rejects_zero()
    {
        using var _ = new TestEnvScope("MOD", "0");
        var type = Type.GetType("TrustAnchorClaimer.Program, TrustAnchorClaimer");
        Assert.NotNull(type);

        var method = type!.GetMethod("ParseMod", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var ex = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, Array.Empty<object>()));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("MOD", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAgentIndices_limits_to_max_agents()
    {
        var type = Type.GetType("TrustAnchorClaimer.Program, TrustAnchorClaimer");
        Assert.NotNull(type);

        var method = type!.GetMethod("GetAgentIndices", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (IEnumerable)method!.Invoke(null, new object[] { (uint)1, (uint)0 })!;
        var indices = result.Cast<int>().ToArray();

        Assert.Equal(21, indices.Length);
        Assert.Equal(0, indices.First());
        Assert.Equal(20, indices.Last());
    }
}
