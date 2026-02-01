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
        // Agent Registry
        // ========================================

        /// <summary>Register a new agent contract with target and name</summary>
        public static void RegisterAgent(UInt160 agent, ECPoint target, string name)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(agent != UInt160.Zero);

            ExecutionEngine.Assert(!string.IsNullOrEmpty(name));
            ExecutionEngine.Assert(name.Length <= 32);

            var targetBytes = (byte[])(object)target;
            ExecutionEngine.Assert(targetBytes is not null && targetBytes.Length == 33);

            var nameToId = new StorageMap(Storage.CurrentContext, PREFIX_NAME_TO_ID);
            ExecutionEngine.Assert(nameToId.Get(name) is null, "Name already registered");

            var targetKey = (ByteString)targetBytes;
            var targetToId = new StorageMap(Storage.CurrentContext, PREFIX_TARGET_TO_ID);
            ExecutionEngine.Assert(targetToId.Get(targetKey) is null, "Target already registered");

            var count = AgentCount();
            ExecutionEngine.Assert(count < MAXAGENTS_BIG, "Maximum 21 agents");

            var key = (ByteString)count;
            new StorageMap(Storage.CurrentContext, PREFIXAGENT).Put(key, agent);
            new StorageMap(Storage.CurrentContext, PREFIXAGENT_TARGET).Put(key, targetKey);
            new StorageMap(Storage.CurrentContext, PREFIXAGENT_NAME).Put(key, name);
            new StorageMap(Storage.CurrentContext, PREFIXAGENT_VOTING).Put(key, BigInteger.Zero);

            nameToId.Put(name, count);
            targetToId.Put(targetKey, count);

            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXAGENT_COUNT }, count + 1);
        }

        /// <summary>Update agent target by index</summary>
        public static void UpdateAgentTargetById(BigInteger index, ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(index >= 0 && index < AgentCount());

            var targetBytes = (byte[])(object)target;
            ExecutionEngine.Assert(targetBytes is not null && targetBytes.Length == 33);

            var targetKey = (ByteString)targetBytes;
            var targetToId = new StorageMap(Storage.CurrentContext, PREFIX_TARGET_TO_ID);
            var existing = targetToId.Get(targetKey);
            if (existing is not null)
            {
                ExecutionEngine.Assert((BigInteger)existing == index, "Target already registered");
            }

            var targetMap = new StorageMap(Storage.CurrentContext, PREFIXAGENT_TARGET);
            var current = targetMap.Get((ByteString)index);
            if (current is not null && current != targetKey)
            {
                targetToId.Delete(current);
            }

            targetMap.Put((ByteString)index, targetKey);
            targetToId.Put(targetKey, index);
        }

        /// <summary>Update agent name by index</summary>
        public static void UpdateAgentNameById(BigInteger index, string name)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(index >= 0 && index < AgentCount());

            ExecutionEngine.Assert(!string.IsNullOrEmpty(name));
            ExecutionEngine.Assert(name.Length <= 32);

            var nameToId = new StorageMap(Storage.CurrentContext, PREFIX_NAME_TO_ID);
            var existing = nameToId.Get(name);
            if (existing is not null)
            {
                ExecutionEngine.Assert((BigInteger)existing == index, "Name already registered");
            }

            var nameMap = new StorageMap(Storage.CurrentContext, PREFIXAGENT_NAME);
            var current = nameMap.Get((ByteString)index);
            if (current is not null && (string)current != name)
            {
                nameToId.Delete(current);
            }

            nameMap.Put((ByteString)index, name);
            nameToId.Put(name, index);
        }

        /// <summary>Set agent voting amount by index (priority only)</summary>
        public static void SetAgentVotingById(BigInteger index, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(index >= 0 && index < AgentCount());
            ExecutionEngine.Assert(amount >= BigInteger.Zero);

            new StorageMap(Storage.CurrentContext, PREFIXAGENT_VOTING).Put((ByteString)index, amount);
        }

        /// <summary>Set agent voting amount by name (priority only)</summary>
        public static void SetAgentVotingByName(string name, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            SetAgentVotingById(GetAgentIdByName(name), amount);
        }

        /// <summary>Set agent voting amount by target public key (priority only)</summary>
        public static void SetAgentVotingByTarget(ECPoint target, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            SetAgentVotingById(GetAgentIdByTarget(target), amount);
        }

        /// <summary>Vote using agent id</summary>
        public static void VoteAgentById(BigInteger index)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(index >= 0 && index < AgentCount());

            var agent = Agent(index);
            ExecutionEngine.Assert(agent != UInt160.Zero);
            var target = AgentTarget(index);
            Contract.Call(agent, "vote", CallFlags.All, new object[] { target });
        }

        /// <summary>Vote using agent name</summary>
        public static void VoteAgentByName(string name)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            VoteAgentById(GetAgentIdByName(name));
        }

        /// <summary>Vote using target public key</summary>
        public static void VoteAgentByTarget(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            VoteAgentById(GetAgentIdByTarget(target));
        }

        private static bool IsRegisteredAgent(UInt160 account)
        {
            int count = (int)AgentCount();
            for (int i = 0; i < count; i++)
            {
                if (Agent(i) == account) return true;
            }
            return false;
        }

        private static BigInteger GetAgentIdByName(string name)
        {
            var data = new StorageMap(Storage.CurrentContext, PREFIX_NAME_TO_ID).Get(name);
            ExecutionEngine.Assert(data is not null, "Unknown agent name");
            return (BigInteger)data;
        }

        private static BigInteger GetAgentIdByTarget(ECPoint target)
        {
            var targetBytes = (byte[])(object)target;
            ExecutionEngine.Assert(targetBytes is not null && targetBytes.Length == 33);
            var data = new StorageMap(Storage.CurrentContext, PREFIX_TARGET_TO_ID).Get((ByteString)targetBytes);
            ExecutionEngine.Assert(data is not null, "Unknown agent target");
            return (BigInteger)data;
        }

        /// <summary>Select agent with highest voting amount (priority only)</summary>
        private static int SelectHighestVotingAgentIndex()
        {
            int count = (int)AgentCount();
            ExecutionEngine.Assert(count > 0);

            BigInteger bestVoting = BigInteger.MinusOne;
            int bestIndex = -1;

            for (int i = 0; i < count; i++)
            {
                BigInteger voting = AgentVoting(i);
                if (voting > bestVoting || (voting == bestVoting && (bestIndex < 0 || i < bestIndex)))
                {
                    bestVoting = voting;
                    bestIndex = i;
                }
            }

            ExecutionEngine.Assert(bestIndex >= 0);
            return bestIndex;
        }

        /// <summary>Select agent with lowest voting amount (excluding already used ones)</summary>
        private static int SelectLowestVotingAgentIndex(bool[] used)
        {
            int selected = -1;
            BigInteger selectedVoting = BigInteger.Zero;
            bool hasSelected = false;

            for (int i = 0; i < used.Length; i++)
            {
                if (used[i]) continue;
                BigInteger voting = AgentVoting(i);

                if (!hasSelected || voting < selectedVoting || (voting == selectedVoting && i < selected))
                {
                    selected = i;
                    selectedVoting = voting;
                    hasSelected = true;
                }
            }
            return selected;
        }
    }
}
