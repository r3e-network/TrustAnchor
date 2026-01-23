using System;
using System.IO;
using Xunit;

namespace BurgerStrategist.Tests;

public class VoteConfigTests
{
    [Fact]
    public void Loads_and_validates_weights_sum_21()
    {
        var json = """
        {"candidates":[{"pubkey":"03ab","weight":10},{"pubkey":"02cd","weight":11}]}
        """;
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        var cfg = VoteConfig.Load(path);
        Assert.Equal(21, cfg.TotalWeight);
        Assert.Equal(2, cfg.Candidates.Count);
    }

    [Fact]
    public void Rejects_invalid_weights()
    {
        var json = """
        {"candidates":[{"pubkey":"03ab","weight":0},{"pubkey":"02cd","weight":21}]}
        """;
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        Assert.Throws<InvalidOperationException>(() => VoteConfig.Load(path));
    }

    [Fact]
    public void Rejects_duplicate_pubkeys()
    {
        var json = """
        {"candidates":[{"pubkey":"03ab","weight":10},{"pubkey":"03ab","weight":11}]}
        """;
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        Assert.Throws<InvalidOperationException>(() => VoteConfig.Load(path));
    }
}
