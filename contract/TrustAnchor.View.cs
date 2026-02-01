using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services;

namespace TrustAnchor
{
#pragma warning disable CS8604 // NEO Storage.Get returns nullable, handled by contract logic
#pragma warning disable CS8602 // NEO framework nullable dereferences are safe
#pragma warning disable CS8625 // Null literals for NEO framework compatibility
#pragma warning disable CS8603 // NEO framework returns are handled

    public partial class TrustAnchor : SmartContract
    {
        // ========================================
        // Public View Methods
        // ========================================

        /// <summary>Get the contract owner address</summary>
        public static UInt160 Owner() => (UInt160)(byte[])Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });

        /// <summary>Get agent contract address by index</summary>
        public static UInt160 Agent(BigInteger i) => (UInt160)(byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENT).Get((ByteString)i);

        /// <summary>Get number of registered agents</summary>
        public static BigInteger AgentCount()
        {
            var stored = Storage.Get(Storage.CurrentContext, new byte[] { PREFIXAGENT_COUNT });
            return stored is null ? BigInteger.Zero : (BigInteger)stored;
        }

        /// <summary>Get pause state (exposed for agents)</summary>
        public static bool isPaused() => IsPaused();

        /// <summary>Get the current RPS (Reward Per Stake) accumulator value</summary>
        /// <returns>Cumulative reward per token (scaled by RPS_SCALE)</returns>
        public static BigInteger RPS() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });

        /// <summary>Get total staked NEO across all users</summary>
        public static BigInteger TotalStake() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE });

        /// <summary>Get user's staked NEO amount</summary>
        public static BigInteger StakeOf(UInt160 account) => (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Get(account);

        /// <summary>Get user's claimable GAS reward (after syncing)</summary>
        /// <param name="account">User address</param>
        /// <returns>Claimable GAS amount</returns>
        public static BigInteger Reward(UInt160 account)
        {
            SyncAccount(account);  // Always sync before reading reward
            return (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
        }

        /// <summary>Get agent's voting target address by index</summary>
        /// <param name="index">Agent index</param>
        /// <returns>ECPoint of the candidate this agent votes for</returns>
        public static ECPoint AgentTarget(BigInteger index)
        {
            var data = new StorageMap(Storage.CurrentContext, PREFIXAGENT_TARGET).Get((ByteString)index);
            return data is null ? default : (ECPoint)(byte[])data;
        }

        /// <summary>Get agent name by index</summary>
        public static string AgentName(BigInteger index)
        {
            var data = new StorageMap(Storage.CurrentContext, PREFIXAGENT_NAME).Get((ByteString)index);
            return data is null ? string.Empty : (string)data;
        }

        /// <summary>Get agent voting amount (priority only)</summary>
        public static BigInteger AgentVoting(BigInteger index)
        {
            var data = new StorageMap(Storage.CurrentContext, PREFIXAGENT_VOTING).Get((ByteString)index);
            return data is null ? BigInteger.Zero : (BigInteger)data;
        }

        /// <summary>Get full agent metadata by index</summary>
        public static object[] AgentInfo(BigInteger index)
        {
            ExecutionEngine.Assert(index >= 0 && index < AgentCount());
            return new object[] { index, Agent(index), AgentTarget(index), AgentName(index), AgentVoting(index) };
        }

        /// <summary>List all registered agents</summary>
        public static object[] AgentList()
        {
            int count = (int)AgentCount();
            var result = new object[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = AgentInfo(i);
            }
            return result;
        }
    }
}
