using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using ContractParameterType = Neo.SmartContract.Framework.ContractParameterType;

namespace TrustAnchor
{
    [ManifestExtra("Author", "developer@r3e.network")]
    [ManifestExtra("Email", "developer@r3e.network")]
    [ManifestExtra("Description", "TrustAnchor Core Contract")]
    [ContractPermission("*", "*")]
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
        private const byte PREFIXAGENTTARGET = 0x11;
        private const byte PREFIXAGENTWEIGHT = 0x12;
        private const byte PREFIXPENDINGACTIVE = 0x20;
        private const byte PREFIXPENDINGAGENTTARGET = 0x21;
        private const byte PREFIXPENDINGAGENTWEIGHT = 0x22;

        private const int MAXAGENTS = 21;
        private const int TOTALWEIGHT = 21;

        private static readonly BigInteger DEFAULTCLAIMREMAIN = 99000000;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_OWNER = default;

        public static UInt160 Owner() => (UInt160)(byte[])Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });
        public static UInt160 Agent(BigInteger i) => (UInt160)(byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENT).Get((ByteString)i);
        public static ECPoint AgentTarget(BigInteger i) => (ECPoint)(byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENTTARGET).Get((ByteString)i);
        public static BigInteger AgentWeight(BigInteger i) => GetWeight(new StorageMap(Storage.CurrentContext, PREFIXAGENTWEIGHT), i);
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
            if (Runtime.CallingScriptHash == GAS.Hash && amount > 0)
            {
                BigInteger ts = TotalStake();
                if (ts > 0)
                {
                    BigInteger rps = RPS();
                    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, amount * DEFAULTCLAIMREMAIN / ts + rps);
                }
            }

            if (from is null) return;
            SyncAccount(from);

            if (Runtime.CallingScriptHash == NEO.Hash && amount > 0)
            {
                AssertConfigReady();
                BigInteger stakeAmount = amount * 100000000;
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
            if (stake > 0)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
                BigInteger paid = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXPAID).Get(account);
                BigInteger earned = stake * (rps - paid) / 100000000 + reward;
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
            if (reward > 0)
            {
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, 0);
                ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, account, reward));
            }
        }

        public static void Withdraw(UInt160 account, BigInteger neoAmount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            ExecutionEngine.Assert(neoAmount > 0);
            SyncAccount(account);
            AssertConfigReady();

            BigInteger stakeAmount = neoAmount * 100000000;
            StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
            BigInteger stake = (BigInteger)stakeMap.Get(account);
            ExecutionEngine.Assert(stake >= stakeAmount);
            stakeMap.Put(account, stake - stakeAmount);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() - stakeAmount);

            BigInteger remaining = neoAmount;
            bool[] used = new bool[MAXAGENTS];
            for (int step = 0; step < MAXAGENTS && remaining > 0; step++)
            {
                int selected = SelectLowestWeightAgentIndex(used);
                if (selected < 0) break;
                used[selected] = true;
                UInt160 agent = Agent(selected);
                ExecutionEngine.Assert(agent != UInt160.Zero);
                BigInteger balance = NEO.BalanceOf(agent);
                if (balance <= 0) continue;
                BigInteger transferAmount = remaining > balance ? balance : remaining;
                if (transferAmount > 0)
                {
                    Contract.Call(agent, "transfer", CallFlags.All, new object[] { account, transferAmount });
                    remaining -= transferAmount;
                }
            }
            ExecutionEngine.Assert(remaining == 0);
        }

        public static void BeginConfig()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE }, 1);
            StorageMap pendingTargets = new(Storage.CurrentContext, PREFIXPENDINGAGENTTARGET);
            StorageMap pendingWeights = new(Storage.CurrentContext, PREFIXPENDINGAGENTWEIGHT);
            for (int i = 0; i < MAXAGENTS; i++)
            {
                pendingTargets.Delete(AgentKey(i));
                pendingWeights.Delete(AgentKey(i));
            }
        }

        public static void SetAgentConfig(BigInteger index, ECPoint target, BigInteger weight)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(index >= 0 && index < MAXAGENTS);
            ExecutionEngine.Assert(weight >= 0);
            StorageMap pendingTargets = new(Storage.CurrentContext, PREFIXPENDINGAGENTTARGET);
            StorageMap pendingWeights = new(Storage.CurrentContext, PREFIXPENDINGAGENTWEIGHT);
            pendingTargets.Put((ByteString)index, target);
            pendingWeights.Put((ByteString)index, weight);
        }

        public static void FinalizeConfig()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            StorageMap pendingTargets = new(Storage.CurrentContext, PREFIXPENDINGAGENTTARGET);
            StorageMap pendingWeights = new(Storage.CurrentContext, PREFIXPENDINGAGENTWEIGHT);
            BigInteger totalWeight = 0;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                ByteString target = pendingTargets.Get(AgentKey(i));
                ByteString weightBytes = pendingWeights.Get(AgentKey(i));
                ExecutionEngine.Assert(target is not null);
                ExecutionEngine.Assert(weightBytes is not null);
                BigInteger weight = (BigInteger)weightBytes;
                ExecutionEngine.Assert(weight >= 0);
                totalWeight += weight;
            }
            ExecutionEngine.Assert(totalWeight == TOTALWEIGHT);
            for (int i = 0; i < MAXAGENTS; i++)
            {
                ByteString target = pendingTargets.Get(AgentKey(i));
                for (int j = i + 1; j < MAXAGENTS; j++)
                {
                    ExecutionEngine.Assert(target != pendingTargets.Get(AgentKey(j)));
                }
            }

            StorageMap targets = new(Storage.CurrentContext, PREFIXAGENTTARGET);
            StorageMap weights = new(Storage.CurrentContext, PREFIXAGENTWEIGHT);
            for (int i = 0; i < MAXAGENTS; i++)
            {
                ByteString target = pendingTargets.Get(AgentKey(i));
                ByteString weightBytes = pendingWeights.Get(AgentKey(i));
                targets.Put(AgentKey(i), target);
                weights.Put(AgentKey(i), weightBytes);
                pendingTargets.Delete(AgentKey(i));
                pendingWeights.Delete(AgentKey(i));
            }

            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE });
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXCONFIGREADY }, 1);
        }

        public static void RebalanceVotes()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            AssertConfigReady();

            BigInteger total = 0;
            BigInteger[] balances = new BigInteger[MAXAGENTS];
            BigInteger[] weights = new BigInteger[MAXAGENTS];
            UInt160[] agents = new UInt160[MAXAGENTS];
            for (int i = 0; i < MAXAGENTS; i++)
            {
                UInt160 agent = Agent(i);
                ExecutionEngine.Assert(agent != UInt160.Zero);
                agents[i] = agent;
                BigInteger balance = NEO.BalanceOf(agent);
                balances[i] = balance;
                BigInteger weight = AgentWeight(i);
                weights[i] = weight;
                total += balance;
            }

            BigInteger[] targets = new BigInteger[MAXAGENTS];
            BigInteger allocated = 0;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                targets[i] = total * weights[i] / TOTALWEIGHT;
                allocated += targets[i];
            }

            BigInteger remainder = total - allocated;
            if (remainder > 0)
            {
                int highest = SelectHighestWeightAgentIndex();
                targets[highest] += remainder;
            }

            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger need = targets[i] - balances[i];
                if (need <= 0) continue;
                for (int j = 0; j < MAXAGENTS && need > 0; j++)
                {
                    BigInteger excess = balances[j] - targets[j];
                    if (excess <= 0) continue;
                    BigInteger amount = need > excess ? excess : need;
                    if (amount > 0)
                    {
                        Contract.Call(agents[j], "transfer", CallFlags.All, new object[] { agents[i], amount });
                        balances[j] -= amount;
                        balances[i] += amount;
                        need -= amount;
                    }
                }
                ExecutionEngine.Assert(need == 0);
            }

            for (int i = 0; i < MAXAGENTS; i++)
            {
                Contract.Call(agents[i], "vote", CallFlags.All, new object[] { AgentTarget(i) });
            }
        }

        public static void SetOwner(UInt160 owner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
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
            ContractManagement.Update(nefFile, manifest, null);
        }
        public static void Pika(BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, Owner(), amount));
        }

        private static bool IsPendingConfigActive()
        {
            return Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE }) is not null;
        }

        private static void AssertConfigReady()
        {
            ExecutionEngine.Assert(Storage.Get(Storage.CurrentContext, new byte[] { PREFIXCONFIGREADY }) is not null);
        }

        private static ByteString AgentKey(int index)
        {
            return (ByteString)(BigInteger)index;
        }

        private static BigInteger GetWeight(StorageMap map, BigInteger index)
        {
            ByteString value = map.Get((ByteString)index);
            return value is null ? 0 : (BigInteger)value;
        }

        private static int SelectHighestWeightAgentIndex()
        {
            BigInteger bestWeight = -1;
            int bestIndex = -1;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger weight = AgentWeight(i);
                if (weight <= 0) continue;
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
            BigInteger selectedWeight = 0;
            for (int i = 0; i < MAXAGENTS; i++)
            {
                if (used[i]) continue;
                BigInteger weight = AgentWeight(i);
                if (weight <= 0) continue;
                if (selected < 0 || weight < selectedWeight || (weight == selectedWeight && i < selected))
                {
                    selected = i;
                    selectedWeight = weight;
                }
            }
            return selected;
        }
    }
}
