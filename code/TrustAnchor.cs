using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using ContractParameterType = Neo.SmartContract.Framework.ContractParameterType;

namespace NeoBurger
{
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "TrustAnchor Core Contract")]
    [ContractPermission("*", "*")]
    public class TrustAnchor : SmartContract
    {
        private const byte PREFIXOWNER = 0x01;
        private const byte PREFIXAGENT = 0x02;
        private const byte PREFIXSTRATEGIST = 0x03;
        private const byte PREFIXREWARDPERTOKENSTORED = 0x04;
        private const byte PREFIXREWARD = 0x05;
        private const byte PREFIXPAID = 0x06;
        private const byte PREFIXCANDIDATEWHITELIST = 0x07;
        private const byte PREFIXSTAKE = 0x08;
        private const byte PREFIXTOTALSTAKE = 0x09;

        private static readonly BigInteger DEFAULTCLAIMREMAIN = 99000000;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_OWNER = default;

        public static UInt160 Owner() => (UInt160)(byte[])Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });
        public static UInt160 Agent(BigInteger i) => (UInt160)(byte[])new StorageMap(Storage.CurrentContext, PREFIXAGENT).Get((ByteString)i);
        public static UInt160 Strategist() => (UInt160)(byte[])Storage.Get(Storage.CurrentContext, new byte[] { PREFIXSTRATEGIST });
        public static ByteString Candidate(ECPoint target) => new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST).Get(target);
        public static BigInteger RPS() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED });
        public static BigInteger TotalStake() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE });
        public static BigInteger StakeOf(UInt160 account) => (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Get(account);
        public static BigInteger Reward(UInt160 account) => SyncAccount(account) ? (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account) : 0;

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, DEFAULT_OWNER);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXSTRATEGIST }, DEFAULT_OWNER);
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
                BigInteger stakeAmount = amount * 100000000;
                StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
                BigInteger stake = (BigInteger)stakeMap.Get(from);
                stakeMap.Put(from, stake + stakeAmount);
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() + stakeAmount);
                ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, Agent(0), NEO.BalanceOf(Runtime.ExecutingScriptHash)));
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

            BigInteger stakeAmount = neoAmount * 100000000;
            StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
            BigInteger stake = (BigInteger)stakeMap.Get(account);
            ExecutionEngine.Assert(stake >= stakeAmount);
            stakeMap.Put(account, stake - stakeAmount);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() - stakeAmount);

            BigInteger remaining = neoAmount;
            for (BigInteger i = 0; remaining > 0; i++)
            {
                UInt160 agent = Agent(i);
                ExecutionEngine.Assert(agent != UInt160.Zero);
                BigInteger balance = NEO.BalanceOf(agent) - 1;
                if (balance < 0) balance = 0;
                if (remaining > balance)
                {
                    remaining -= balance;
                    if (balance > 0)
                        Contract.Call(agent, "transfer", CallFlags.All, new object[] { account, balance });
                }
                else
                {
                    Contract.Call(agent, "transfer", CallFlags.All, new object[] { account, remaining });
                    break;
                }
            }
        }

        public static void TrigVote(BigInteger i, ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Strategist()));
            ExecutionEngine.Assert(new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST).Get(target) is not null);
            Contract.Call(Agent(i), "vote", CallFlags.All, new object[] { target });
        }
        public static void TrigTransfer(BigInteger i, BigInteger j, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Strategist()));
            Contract.Call(Agent(i), "transfer", CallFlags.All, new object[] { Agent(j), amount });
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
        public static void SetStrategist(UInt160 strategist)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXSTRATEGIST }, strategist);
        }
        public static void AllowCandidate(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            StorageMap candidates = new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST);
            candidates.Put(target, 1);
        }
        public static void DisallowCandidate(ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            StorageMap candidates = new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST);
            candidates.Delete(target);
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
    }
}
