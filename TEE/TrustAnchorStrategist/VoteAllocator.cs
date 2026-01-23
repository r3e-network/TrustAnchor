using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TrustAnchorStrategist;

public static class VoteAllocator
{
    public static IReadOnlyList<BigInteger> ComputeTargets(BigInteger totalPower, IReadOnlyList<int> weights)
    {
        if (weights.Count == 0) throw new InvalidOperationException("No weights");
        var totalWeight = weights.Sum();
        if (totalWeight != 21) throw new InvalidOperationException("Weight sum must be 21");

        var targets = weights.Select(w => totalPower * w / totalWeight).ToList();
        var used = targets.Aggregate(BigInteger.Zero, (acc, v) => acc + v);
        var remainder = totalPower - used;
        if (remainder > 0)
        {
            var maxWeight = weights[0];
            var idx = 0;
            for (var i = 1; i < weights.Count; i++)
            {
                if (weights[i] > maxWeight)
                {
                    maxWeight = weights[i];
                    idx = i;
                }
            }
            targets[idx] += remainder;
        }
        return targets;
    }
}
