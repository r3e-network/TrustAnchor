using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using ContractParameterType = Neo.SmartContract.Framework.ContractParameterType;

namespace TrustAnchor
{
#pragma warning disable CS8604 // NEO Storage.Get returns nullable, handled by contract logic
#pragma warning disable CS8602 // NEO framework nullable dereferences are safe
#pragma warning disable CS8625 // Null literals for NEO framework compatibility
#pragma warning disable CS8603 // NEO framework returns are handled

    public partial class TrustAnchor : SmartContract
    {
        // ========================================
        // Storage Prefixes
        // ========================================

        /// <summary>Owner address</summary>
        private const byte PREFIXOWNER = 0x01;

        /// <summary>Agent contract addresses (0-20)</summary>
        private const byte PREFIXAGENT = 0x02;

        /// <summary>RPS (Reward Per Stake) accumulator - tracks cumulative reward per token over time</summary>
        /// <remarks>
        /// Formula: RPS = Σ(GAS_received × 100% / totalStake)
        /// Units: reward units per NEO (scaled by RPS_SCALE = 100,000,000)
        ///
        /// This global counter increases whenever GAS is received, proportional to:
        /// - Current total stake (more stake = slower RPS growth)
        /// - GAS amount received
        ///
        /// Example: 100 GAS received with 1000 NEO total stake
        ///   RPS += 100 × 100,000,000 / 1000 = 10,000,000
        /// </remarks>
        private const byte PREFIXREWARDPERTOKENSTORED = 0x04;

        /// <summary>User's accumulated reward balance (claimable GAS)</summary>
        private const byte PREFIXREWARD = 0x05;

        /// <summary>User's last synced RPS value (prevents double-counting rewards)</summary>
        /// <remarks>
        /// Each user has their own 'paid' value that tracks the RPS at their last sync.
        /// When calculating rewards: earned = stake × (currentRPS - paidRPS)
        /// This ensures we only count RPS increases from the last sync, not from the beginning.
        /// </remarks>
        private const byte PREFIXPAID = 0x06;

        /// <summary>GAS received before any stake exists</summary>
        private const byte PREFIXPENDINGREWARD = 0x07;

        /// <summary>User's staked NEO amount</summary>
        private const byte PREFIXSTAKE = 0x08;

        /// <summary>Total staked NEO across all users</summary>
        private const byte PREFIXTOTALSTAKE = 0x09;

        /// <summary>Number of registered agents</summary>
        private const byte PREFIXAGENT_COUNT = 0x13;

        /// <summary>Agent voting target ECPoint (index -> target)</summary>
        private const byte PREFIXAGENT_TARGET = 0x14;

        /// <summary>Agent display name (index -> name)</summary>
        private const byte PREFIXAGENT_NAME = 0x15;

        /// <summary>Agent voting amount (priority only)</summary>
        private const byte PREFIXAGENT_VOTING = 0x16;

        /// <summary>Name to agent index mapping</summary>
        private const byte PREFIX_NAME_TO_ID = 0x18;

        /// <summary>Target to agent index mapping</summary>
        private const byte PREFIX_TARGET_TO_ID = 0x19;

        private const byte PREFIXPAUSED = 0x40;

        // ========================================
        // Constants
        // ========================================

        /// <summary>Number of agent contracts (fixed at 21)</summary>
        private const int MAXAGENTS = 21;
        private static readonly BigInteger MAXAGENTS_BIG = MAXAGENTS;

        /// <summary>Scale factor for reward calculations (8 decimal places)</summary>
        private static readonly BigInteger RPS_SCALE = 100000000;

        /// <summary>100% of GAS goes to stakers, no fees</summary>
        private static readonly BigInteger DEFAULTCLAIMREMAIN = 100000000;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_OWNER = default;
    }
}
