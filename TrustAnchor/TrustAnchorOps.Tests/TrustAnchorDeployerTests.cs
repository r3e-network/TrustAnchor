using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class TrustAnchorDeployerTests
{
    [Fact]
    public void ResolveContractsDir_UsesEnvOverride()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempRoot);
        using var _ = new TestEnvScope("CONTRACTS_DIR", tempRoot);

        var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
        Assert.NotNull(type);

        var method = type!.GetMethod("ResolveContractsDir", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, Array.Empty<object>())!;
        Assert.Equal(Path.GetFullPath(tempRoot), result);
    }

    [Fact]
    public void ResolveContractsDir_SearchesUpward()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        var contractDir = Path.Combine(tempRoot, "contract");
        var nested = Path.Combine(tempRoot, "a", "b");
        Directory.CreateDirectory(contractDir);
        Directory.CreateDirectory(nested);

        using var _ = new TestEnvScope("CONTRACTS_DIR", null);
        var original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(nested);
        try
        {
            var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
            Assert.NotNull(type);
            var method = type!.GetMethod("ResolveContractsDir", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            var result = (string)method!.Invoke(null, Array.Empty<object>())!;
            Assert.Equal(contractDir, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void ResolveNccsPath_UsesEnvOverride()
    {
        using var _ = new TestEnvScope("NCCS_PATH", "/custom/nccs");
        var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
        Assert.NotNull(type);
        var method = type!.GetMethod("ResolveNccsPath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (string)method!.Invoke(null, Array.Empty<object>())!;
        Assert.Equal("/custom/nccs", result);
    }

    [Fact]
    public void Deployer_requires_wif()
    {
        using var _ = new TestEnvScope("WIF", null);
        var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
        Assert.NotNull(type);

        var method = type!.GetMethod("GetKeyPair", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var ex = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, Array.Empty<object>()));
        Assert.Contains("WIF", ex.InnerException!.Message);
    }
}
