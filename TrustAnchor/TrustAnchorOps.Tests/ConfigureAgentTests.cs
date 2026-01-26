using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class ConfigureAgentTests
{
    [Fact]
    public void ConfigureAgent_Uses_Update_Methods()
    {
        var type = Type.GetType("ConfigureAgent.Program, ConfigureAgent");
        Assert.NotNull(type);

        var targetField = type!.GetField("UpdateTargetMethod", BindingFlags.NonPublic | BindingFlags.Static);
        var nameField = type!.GetField("UpdateNameMethod", BindingFlags.NonPublic | BindingFlags.Static);
        var votingField = type!.GetField("SetVotingMethod", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(targetField);
        Assert.NotNull(nameField);
        Assert.NotNull(votingField);

        Assert.Equal("updateAgentTargetById", targetField!.GetValue(null));
        Assert.Equal("updateAgentNameById", nameField!.GetValue(null));
        Assert.Equal("setAgentVotingById", votingField!.GetValue(null));
    }
}
