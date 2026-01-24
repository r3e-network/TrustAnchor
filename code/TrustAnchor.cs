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

    [ManifestExtra("Author", "R3E Network")]
    [ManifestExtra("Email", "developer@r3e.network")]
    [ManifestExtra("Description", "TrustAnchor: Decentralized voting delegation for active contributors and reputable community members")]
    [ContractPermission("*", "transfer", "vote", "sync", "claim")]
    [ContractPermission("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5", "transfer", "vote")]
    [ContractPermission("0xd2a4cff31913016155e38e474a2c06d08be276cf", "transfer")]
    [ContractPermission("0xfffdc93764dbaddd97c48f252a53ea4643faa3fd", "update")]
    public class TrustAnchor : SmartContract
    {
        private const byte PREFIXOWNER = 0x01;
        private const byte PREFIXAGENT = 0x02;
        private const byte PREFIXREWARDPERTOKENSTORED = 0x04;
        private const byte PREFIXREWARD = 0x05;
        private const byte PREFIXPAID = 0x06;
        private const byte PREFIXSTAKE = 0x08;
        private const byte PREFIXTOTALSTAKE = 0x09;

        private const byte PREFIXCONFIGREADY = 0x10;
        private const byte PREFIXAGENTCONFIG = 0x11;    // Combined agent target+weight storage (serialized as [33-byte ECPoint + weight bytes])
        private const byte PREFIXCONFIGVERSION = 0x12;  // Config version for tracking changes
        private const byte PREFIXPENDINGACTIVE = 0x20;  // Flag indicating config session is active
        private const byte PREFIXPENDINGCONFIG = 0x21;  // Pending agent configs for batch updates
        private const byte PREFIXPENDINGOWNER = 0x30;
        private const byte PREFIXOWNERDELAY = 0x31;
        private const byte PREFIXPAUSED = 0x40;

        private const int MAXAGENTS = 21;
        private static readonly BigInteger MAXAGENTS_BIG = MAXAGENTS;
        private static readonly BigInteger TOTALWEIGHT = 21;
        private static readonly BigInteger RPS_SCALE = 100000000;
        private static readonly BigInteger DEFAULTCLAIMREMAIN = 99000000;
        private static readonly BigInteger OWNER_CHANGE_DELAY = 3 * 24 * 3600; // 3 days in seconds

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_OWNER = default;

        public static UInt160 Owner() => (UInt160)(byte[])Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });
        public static UInt160 Agent(BigInteger i) => (UInt160)(byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENT).Get((ByteString)i);
        public static BigInteger ConfigVersion() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXCONFIGVERSION });

        /// <summary>Get the voting target (candidate ECPoint) for an agent by index.</summary>
        public static ECPoint AgentConfig_GetTarget(BigInteger i)
        {
            var data = (byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG).Get((ByteString)i);
            if (data is null || data.Length < 33)
                return default;
            var targetBytes = new byte[33];
            for (int k = 0; k < 33; k++) targetBytes[k] = data[k];
            return (ECPoint)(object)targetBytes;
        }

        /// <summary>Get the voting weight for an agent by index.</summary>
        public static BigInteger AgentConfig_GetWeight(BigInteger i)
        {
            var data = (byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG).Get((ByteString)i);
            if (data is null || data.Length < 34)
                return BigInteger.Zero;
            var weightBytes = new byte[data.Length - 33];
            for (int k = 0; k < weightBytes.Length; k++) weightBytes[k] = data[33 + k];
            return new BigInteger(weightBytes);
        }

        private static (ECPoint, BigInteger) AgentConfig(BigInteger i)
        {
            var t = AgentConfig_GetTarget(i);
            var w = AgentConfig_GetWeight(i);
            var result = (t, w);
            return result;
        }

        public static ECPoint AgentTarget(BigInteger i) => AgentConfig_GetTarget(i);
        public static BigInteger AgentWeight(BigInteger i) => AgentConfig_GetWeight(i);
        public static BigInteger RPS() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });
        public static BigInteger TotalStake() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE });
        public static BigInteger StakeOf(UInt160 account) => (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Get(account);
        public static BigInteger Reward(UInt160 account) => SyncAccount(account) ? (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account) : 0;

        public static void _deploy(object data, bool update)
        {
            if (update) return;
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, DEFAULT_OWNER);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            if (Runtime.CallingScriptHash == GAS.Hash && amount > BigInteger.Zero)
            {
                BigInteger ts = TotalStake();
                if (ts > BigInteger.Zero)
                {
                    BigInteger rps = RPS();
                    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, amount * DEFAULTCLAIMREMAIN / ts + rps);
                }
            }

            if (from is null || from == UInt160.Zero) return;
            SyncAccount(from);

            if (Runtime.CallingScriptHash == NEO.Hash && amount > BigInteger.Zero)
            {
                AssertConfigReady();
                BigInteger stakeAmount = amount;
                StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
                BigInteger stake = (BigInteger)stakeMap.Get(from);
                stakeMap.Put(from, stake + stakeAmount);
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() + stakeAmount);
                int targetIndex = SelectHighestWeightAgentIndex();
                UInt160 targetAgent = Agent(targetIndex);
                ExecutionEngine.Assert(targetAgent != UInt160.Zero);
                ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, targetAgent, NEO.BalanceOf(Runtime.ExecutingScriptHash)));
            }
        }

        public static bool SyncAccount(UInt160 account)
        {
            BigInteger rps = RPS();
            BigInteger stake = StakeOf(account);
            if (stake > BigInteger.Zero)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
                BigInteger paid = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXPAID).Get(account);
                BigInteger earned = stake * (rps - paid) / RPS_SCALE + reward;
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, earned);
            }
            new StorageMap(Storage.CurrentContext, PREFIXPAID).Put(account, rps);
            return true;
        }

        public static void ClaimReward(UInt160 account)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            SyncAccount(account);
            BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
            if (reward > BigInteger.Zero)
            {
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, 0);
                ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, account, reward));
            }
        }

        public static void Withdraw(UInt160 account, BigInteger neoAmount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            ExecutionEngine.Assert(neoAmount > BigInteger.Zero);
            SyncAccount(account);
            AssertConfigReady();

            BigInteger stakeAmount = neoAmount;
            StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
            BigInteger stake = (BigInteger)stakeMap.Get(account);
            ExecutionEngine.Assert(stake >= stakeAmount);
            stakeMap.Put(account, stake - stakeAmount);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() - stakeAmount);

            BigInteger remaining = neoAmount;
            bool[] used = new bool[MAXAGENTS];
            for (int step = 0; step < MAXAGENTS && remaining > BigInteger.Zero; step++)
            {
                int selected = SelectLowestWeightAgentIndex(used);
                if (selected < 0) break;
                used[selected] = true;
                UInt160 agent = Agent(selected);
                ExecutionEngine.Assert(agent != UInt160.Zero);
                BigInteger balance = NEO.BalanceOf(agent);
                if (balance <= BigInteger.Zero) continue;
                BigInteger transferAmount = remaining > balance ? balance : remaining;
                if (transferAmount > BigInteger.Zero)
                {
                    Contract.Call(agent, "transfer", CallFlags.All, new object[] { account, transferAmount });
                    remaining -= transferAmount;
                }
            }
            ExecutionEngine.Assert(remaining == BigInteger.Zero);
        }

        /// <summary>Begin a configuration session. Copies existing config to pending for incremental updates.</summary>
        public static void BeginConfig()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE }, 1);

            // Copy current config to pending for incremental updates
            var activeMap = new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);
            var pendingMap = new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG);

            bool hasConfig = Storage.Get(Storage.CurrentContext, new byte[] { PREFIXCONFIGREADY }) is not null;
            if (hasConfig)
            {
                // Copy all existing configs to pending
                for (int i = 0; i < MAXAGENTS; i++)
                {
                    var key = AgentKey(i);
                    var data = (byte[])activeMap.Get(key);
                    if (data is not null && data.Length > 0)
                    {
                        pendingMap.Put(key, data);
                    }
                }
            }
        }

        /// <summary>Set target and weight for a single agent during config session.</summary>
        public static void SetAgentConfig(BigInteger index, ECPoint target, BigInteger weight)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(index >= 0 && index < MAXAGENTS_BIG);
            ExecutionEngine.Assert(weight >= BigInteger.Zero);
            var data = SerializeAgentConfig(target, weight);
            new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG).Put((ByteString)index, data);
        }

        /// <summary>Batch set all 21 agent targets and weights at once.</summary>
        public static void SetAgentConfigs(ECPoint[] targets, BigInteger[] weights)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(targets is not null && weights is not null);
            ExecutionEngine.Assert(targets.Length == MAXAGENTS && weights.Length == MAXAGENTS);
            var pendingMap = new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG);
            for (int i = 0; i < MAXAGENTS; i++)
            {
                ExecutionEngine.Assert(weights[i] >= BigInteger.Zero);
                var data = SerializeAgentConfig(targets[i], weights[i]);
                pendingMap.Put(AgentKey(i), data);
            }
        }

        /// <summary>Update only the target for a specific agent (preserves current weight).</summary>
        public static void SetAgentTarget(BigInteger index, ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(index >= 0 && index < MAXAGENTS_BIG);
            // Preserve current weight, update target only
            var currentWeight = AgentConfig_GetWeight(index);
            var data = SerializeAgentConfig(target, currentWeight);
            new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG).Put((ByteString)index, data);
        }

        /// <summary>Update only the weight for a specific agent (preserves current target).</summary>
        public static void SetAgentWeight(BigInteger index, BigInteger weight)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(index >= 0 && index < MAXAGENTS_BIG);
            ExecutionEngine.Assert(weight >= BigInteger.Zero);
            // Preserve current target, update weight only
            var currentTarget = AgentConfig_GetTarget(index);
            var targetBytes = (byte[])(object)currentTarget;
            if (targetBytes is null || targetBytes.Length == 0)
            {
                targetBytes = new byte[33]; // Use placeholder if no target exists
            }
            var target = (ECPoint)(object)targetBytes;
            var data = SerializeAgentConfig(target, weight);
            new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG).Put((ByteString)index, data);
        }

        public static void SetAgentWeights(BigInteger[] weights)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(weights is not null);
            ExecutionEngine.Assert(weights.Length == MAXAGENTS);
            var pendingMap = new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG);
            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger weight = weights[i];
                ExecutionEngine.Assert(weight >= BigInteger.Zero);
                // Preserve current target, update weight only
                var currentTarget = AgentConfig_GetTarget(i);
                var targetBytes = (byte[])(object)currentTarget;
                if (targetBytes is null || targetBytes.Length == 0)
                {
                    targetBytes = new byte[33]; // Use placeholder if no target exists
                }
                var target = (ECPoint)(object)targetBytes;
                var data = SerializeAgentConfig(target, weight);
                pendingMap.Put(AgentKey(i), data);
            }
        }

        /// <summary>Finalize config session. Validates and atomically applies all pending configs.</summary>
        public static void FinalizeConfig()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            
            var pendingMap = new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG);
            var activeMap = new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);
            
            // Collect all configs for validation
            var targetBytesList = new byte[MAXAGENTS][];
            var weightList = new BigInteger[MAXAGENTS];
            BigInteger totalWeight = 0;

            for (int i = 0; i < MAXAGENTS; i++)
            {
                var key = AgentKey(i);
                var data = (byte[])pendingMap.Get(key);

                // Check pending first, then fall back to active (for incremental updates)
                if (data is null || data.Length == 0)
                {
                    data = (byte[])activeMap.Get(key);
                }

                // At least one of pending or active must have config
                ExecutionEngine.Assert(data is not null && data.Length > 0);

                var target = DeserializeAgentConfig_GetTarget(data);
                var weight = DeserializeAgentConfig_GetWeight(data);
                ExecutionEngine.Assert(weight >= BigInteger.Zero);
                totalWeight += weight;

                // Store target bytes and weight for later use
                var tb = (byte[])(object)target;
                targetBytesList[i] = tb;
                weightList[i] = weight;

                // Check for duplicate targets (optimized: only check against previously seen)
                for (int j = 0; j < i; j++)
                {
                    var prevTargetBytes = targetBytesList[j];
                    if (prevTargetBytes is null || tb is null) continue;
                    bool match = true;
                    for (int k = 0; k < 33; k++)
                    {
                        if (tb[k] != prevTargetBytes[k])
                        {
                            match = false;
                            break;
                        }
                    }
                    ExecutionEngine.Assert(!match);
                }
            }
            
            ExecutionEngine.Assert(totalWeight == TOTALWEIGHT);
            
            // Apply all configs at once (atomic)
            for (int i = 0; i < MAXAGENTS; i++)
            {
                var tb = targetBytesList[i];
                var target = tb is null ? default : (ECPoint)(object)tb;
                var data = SerializeAgentConfig(target, weightList[i]);
                activeMap.Put(AgentKey(i), data);
                pendingMap.Delete(AgentKey(i));
            }

            // Increment config version
            var currentVersion = ConfigVersion();
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXCONFIGVERSION }, currentVersion + 1);
            
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE });
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXCONFIGREADY }, 1);
        }

        /// <summary>
        /// Rebalance NEO and votes across agents according to their weights.
        /// Transfers NEO between agents and casts votes proportionally.
        /// </summary>
        public static void RebalanceVotes()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            AssertConfigReady();

            // Single pass: collect all agent data
            var agents = new UInt160[MAXAGENTS];
            var balances = new BigInteger[MAXAGENTS];
            var weights = new BigInteger[MAXAGENTS];
            var targets = new ECPoint[MAXAGENTS];
            BigInteger total = 0;

            for (int i = 0; i < MAXAGENTS; i++)
            {
                agents[i] = Agent(i);
                ExecutionEngine.Assert(agents[i] != UInt160.Zero);
                balances[i] = NEO.BalanceOf(agents[i]);
                weights[i] = AgentConfig_GetWeight(i);
                targets[i] = AgentConfig_GetTarget(i);
                total += balances[i];
            }

            // Calculate target balances in one pass
            var targetBalances = new BigInteger[MAXAGENTS];
            BigInteger allocated = 0;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                targetBalances[i] = total * weights[i] / TOTALWEIGHT;
                allocated += targetBalances[i];
            }

            // Distribute remainder to highest weight
            BigInteger remainder = total - allocated;
            if (remainder > BigInteger.Zero)
            {
                int highest = SelectHighestWeightAgentIndex();
                targetBalances[highest] += remainder;
            }

            // Count deficits and excesses
            int deficitCount = 0, excessCount = 0;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger diff = targetBalances[i] - balances[i];
                if (diff > 0) deficitCount++;
                else if (diff < 0) excessCount++;
            }

            // Collect indices
            var deficitIndices = new int[deficitCount];
            var excessIndices = new int[excessCount];
            int defIdx = 0, excIdx = 0;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger diff = targetBalances[i] - balances[i];
                if (diff > 0) deficitIndices[defIdx++] = i;
                else if (diff < 0) excessIndices[excIdx++] = i;
            }

            // Match deficits with excesses (more efficient than nested loops)
            for (int d = 0; d < deficitCount; d++)
            {
                int deficitIdx = deficitIndices[d];
                BigInteger need = targetBalances[deficitIdx] - balances[deficitIdx];
                for (int e = 0; e < excessCount && need > 0; e++)
                {
                    int excessIdx = excessIndices[e];
                    BigInteger have = balances[excessIdx] - targetBalances[excessIdx];
                    if (have <= 0) continue;

                    BigInteger amount = need > have ? have : need;
                    if (amount > 0)
                    {
                        Contract.Call(agents[excessIdx], "transfer", CallFlags.All,
                            new object[] { agents[deficitIdx], amount });
                        balances[excessIdx] -= amount;
                        need -= amount;
                    }
                }
                ExecutionEngine.Assert(need == 0);
            }

            // Vote all agents in single loop
            for (int i = 0; i < MAXAGENTS; i++)
            {
                Contract.Call(agents[i], "vote", CallFlags.All,
                    new object[] { targets[i] });
            }
        }

        public static void InitiateOwnerTransfer(UInt160 newOwner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(newOwner != UInt160.Zero);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER }, newOwner);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNERDELAY }, Runtime.Time + OWNER_CHANGE_DELAY);
        }

        public static void AcceptOwnerTransfer()
        {
            var pendingOwner = (UInt160)(byte[])Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER });
            var effectiveTime = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNERDELAY });
            ExecutionEngine.Assert(pendingOwner != UInt160.Zero);
            ExecutionEngine.Assert(Runtime.CheckWitness(pendingOwner));
            ExecutionEngine.Assert(Runtime.Time >= effectiveTime);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, pendingOwner);
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER });
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXOWNERDELAY });
        }

        public static void SetOwner(UInt160 owner)
        {
            // DEPRECATED: Use InitiateOwnerTransfer/AcceptOwnerTransfer for security
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(owner != UInt160.Zero);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, owner);
        }
        public static void SetAgent(BigInteger i, UInt160 agent)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            new StorageMap(Storage.CurrentContext, PREFIXAGENT).Put((ByteString)i, agent);
        }
        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(!IsPaused());
            ContractManagement.Update(nefFile, manifest, null);
        }

        public static void Pause()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPAUSED }, 1);
        }

        public static void Unpause()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPAUSED });
        }
        public static void WithdrawGAS(BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(amount > BigInteger.Zero);
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, Owner(), amount));
        }

        public static void Pika(BigInteger amount)
        {
            // DEPRECATED: Use WithdrawGAS instead
            WithdrawGAS(amount);
        }

        private static bool IsPendingConfigActive()
        {
            return Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE }) is not null;
        }

        private static void AssertConfigReady()
        {
            ExecutionEngine.Assert(Storage.Get(Storage.CurrentContext, new byte[] { PREFIXCONFIGREADY }) is not null);
        }

        private static bool IsPaused()
        {
            return Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPAUSED }) is not null;
        }

        private static ByteString AgentKey(int index)
        {
            return (ByteString)(BigInteger)index;
        }

        private static int SelectHighestWeightAgentIndex()
        {
            BigInteger bestWeight = BigInteger.MinusOne;
            int bestIndex = -1;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger weight = AgentWeight(i);
                if (weight <= BigInteger.Zero) continue;
                if (weight > bestWeight || (weight == bestWeight && (bestIndex < 0 || i < bestIndex)))
                {
                    bestWeight = weight;
                    bestIndex = i;
                }
            }
            ExecutionEngine.Assert(bestIndex >= 0);
            return bestIndex;
        }

        private static int SelectLowestWeightAgentIndex(bool[] used)
        {
            int selected = -1;
            BigInteger selectedWeight = BigInteger.Zero;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                if (used[i]) continue;
                BigInteger weight = AgentWeight(i);
                if (weight <= BigInteger.Zero) continue;
                if (selected < 0 || weight < selectedWeight || (weight == selectedWeight && i < selected))
                {
                    selected = i;
                    selectedWeight = weight;
                }
            }
            return selected;
        }

        /// <summary>
        /// Serialize agent configuration as [ECPoint(33 bytes) + weight bytes].
        /// ECPoint is stored as 33 bytes (compressed SEC format).
        /// Weight is serialized as BigInteger bytes (minimum 1 byte).
        /// </summary>
        private static byte[] SerializeAgentConfig(ECPoint target, BigInteger weight)
        {
            var targetBytes = (byte[])(object)target;
            if (targetBytes is null || targetBytes.Length != 33)
            {
                targetBytes = new byte[33]; // Use all zeros as placeholder for null/invalid ECPoint
            }
            var weightBytes = weight.ToByteArray();
            // Ensure at least 1 byte for weight (BigInteger.Zero -> {0} not {})
            if (weightBytes.Length == 0)
            {
                weightBytes = new byte[] { 0 };
            }
            var result = new byte[33 + weightBytes.Length];
            for (int i = 0; i < 33; i++) result[i] = targetBytes[i];
            for (int i = 0; i < weightBytes.Length; i++) result[33 + i] = weightBytes[i];
            return result;
        }

        private static (ECPoint target, BigInteger weight) DeserializeAgentConfig(byte[] data)
        {
            var t = DeserializeAgentConfig_GetTarget(data);
            var w = DeserializeAgentConfig_GetWeight(data);
            return (t, w);
        }

        /// <summary>Extract ECPoint from serialized config data (first 33 bytes).</summary>
        private static ECPoint DeserializeAgentConfig_GetTarget(byte[] data)
        {
            if (data is null || data.Length < 33)
                return default;
            var targetBytes = new byte[33];
            for (int i = 0; i < 33; i++) targetBytes[i] = data[i];
            return (ECPoint)(object)targetBytes;
        }

        /// <summary>Extract weight from serialized config data (bytes after first 33 bytes).</summary>
        private static BigInteger DeserializeAgentConfig_GetWeight(byte[] data)
        {
            if (data is null || data.Length < 34)
                return BigInteger.Zero;
            var weightBytesLength = data.Length - 33;
            var weightBytes = new byte[weightBytesLength];
            for (int i = 0; i < weightBytesLength; i++) weightBytes[i] = data[33 + i];
            return new BigInteger(weightBytes);
        }
    }
}
