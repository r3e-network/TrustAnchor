using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace BurgerStrategist.Tests;

public class VoteAllocatorTests
{
    [Fact]
    public void Targets_sum_to_total_power()
    {
        var weights = new List<int> { 10, 11 };
        var targets = VoteAllocator.ComputeTargets(new BigInteger(2100), weights);
        Assert.Equal(new BigInteger(2100), targets[0] + targets[1]);
    }

    [Fact]
    public void Remainder_is_assigned_to_largest_weight()
    {
        var weights = new List<int> { 10, 11 };
        var targets = VoteAllocator.ComputeTargets(new BigInteger(1), weights);
        Assert.Equal(new BigInteger(0), targets[0]);
        Assert.Equal(new BigInteger(1), targets[1]);
    }
}
