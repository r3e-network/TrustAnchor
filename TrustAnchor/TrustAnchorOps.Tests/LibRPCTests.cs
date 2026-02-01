using System;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class LibRPCTests
{
    [Fact]
    public void LibRPC_UsesNetworkMagicFromEnv()
    {
        using var _rpc = new TestEnvScope("RPC", "http://localhost:10332");
        using var _magic = new TestEnvScope("NETWORK_MAGIC", "123456");

        var type = Type.GetType("LibRPC.Program, LibRPC");
        Assert.NotNull(type);

        var method = type!.GetMethod("BuildProtocolSettings", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var settings = method!.Invoke(null, Array.Empty<object>())!;
        var networkProperty = settings.GetType().GetProperty("Network");
        Assert.NotNull(networkProperty);
        var networkValue = networkProperty!.GetValue(settings);
        Assert.Equal((uint)123456, (uint)networkValue!);
    }
}
