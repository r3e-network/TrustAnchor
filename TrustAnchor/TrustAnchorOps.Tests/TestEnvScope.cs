using System;

namespace TrustAnchorOps.Tests;

public sealed class TestEnvScope : IDisposable
{
    private readonly string _name;
    private readonly string? _prior;

    public TestEnvScope(string name, string? value)
    {
        _name = name;
        _prior = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _prior);
}
