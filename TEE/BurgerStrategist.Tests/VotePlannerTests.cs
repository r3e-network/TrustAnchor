using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace BurgerStrategist.Tests;

public class VotePlannerTests
{
    [Fact]
    public void Rejects_candidate_count_mismatch()
    {
        var targets = new List<BigInteger> { 1000, 1100 };
        Assert.Throws<InvalidOperationException>(() => VotePlanner.AssignTargets(3, targets));
    }
}
