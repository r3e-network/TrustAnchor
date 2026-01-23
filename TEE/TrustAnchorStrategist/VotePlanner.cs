using System;
using System.Collections.Generic;
using System.Numerics;

namespace TrustAnchorStrategist;

public static class VotePlanner
{
    public static IReadOnlyList<BigInteger> AssignTargets(int agentCount, IReadOnlyList<BigInteger> targets)
    {
        if (agentCount != targets.Count)
            throw new InvalidOperationException("Candidate count must match agent count");

        return targets;
    }
}
