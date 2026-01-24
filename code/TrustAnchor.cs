using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;
using Neo.Cryptography.ECC;


namespace TrustAnchor
{
    [ManifestExtra("Author", "developer@r3e.network")]
    [ManifestExtra("Email", "developer@r3e.network")]
    [ManifestExtra("Description", "TrustAnchor - Direct NEO Staking & Voting")]
    [ContractPermission("*", "*")]
    public class TrustAnchor : SmartContract
    {
        private const byte PREFIXOWNER = 0x01;
        private const byte PREFIXAGENT = 0x02;
        private const byte PREFIXSTRATEGIST = 0x03;
        private const byte PREFIXSTAKE = 0x04;           // User NEO deposits
        private const byte PREFIXTOTALSTAKE = 0x05;     // Total NEO deposited
        private const byte PREFIXREWARDPERSTORED = 0x06; // Reward Per Share (RPS)
        private const byte PREFIXREWARD = 0x07;          // Pending reward for user
        private const byte PREFIXPAID = 0x08;            // RPS already paid to user
        private static readonly BigInteger DEFAULTCLAIMREMAIN = 99000000;

        public static UInt160 Owner() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXOWNER });
        public static UInt160 Agent(BigInteger i) => (UInt160)new StorageMap(Storage.CurrentContext, PREFIXAGENT).Get((ByteString)i);
        public static UInt160 Strategist() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXSTRATEGIST });
        public static ByteString Candidate(ECPoint target) => new StorageMap(Storage.CurrentContext, PREFIXCANDIDATEWHITELIST).Get(target);

        // Get user's staked NEO amount
        public static BigInteger Stake(UInt160 account) => (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Get(account);
        // Get total staked NEO
        public static BigInteger TotalStake() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE });
        // Reward Per Share
        public static BigInteger RPS() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERSTORED });

        // Get pending reward for user
        public static BigInteger PendingReward(UInt160 account)
        {
            SyncAccount(account);
            return (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // Handle NEO deposit - just track it, NO token minting
            if (Runtime.CallingScriptHash == NEO.Hash && amount > 0)
            {
                // Transfer NEO to Agent(0) for voting
                ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, Agent(0), amount));

                // Track user stake (1 NEO = 1 unit, same scale as NEO)
                new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Put(from, Stake(from) + amount);
                new StorageMap(Storage.CurrentContext, PREFIXTOTALSTAKE).Put(TotalStake() + amount);

                // Sync rewards for the depositor
                SyncAccount(from);
            }
            // Handle GAS reward distribution
            else if (Runtime.CallingScriptHash == GAS.Hash && amount > 0)
            {
                BigInteger totalStake = TotalStake();
                if (totalStake > 0)
                {
                    BigInteger rps = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERSTORED });
                    BigInteger newRps = amount * DEFAULTCLAIMREMAIN / totalStake + rps;
                    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERSTORED }, newRps);
                }
            }
            // Handle reward claiming - user sends GAS to trigger claim
            else if (Runtime.CallingScriptHash == GAS.Hash && amount > 0 && data is null)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(from);
                if (reward > 0)
                {
                    new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(from, 0);
                    ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, from, reward));
                }
            }
        }

        // Sync user's pending rewards
        public static bool SyncAccount(UInt160 account)
        {
            BigInteger rps = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXREWARDPERSTORED });
            BigInteger stake = Stake(account);
            if (stake > 0)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
                BigInteger paid = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXPAID).Get(account);
                // Calculate earned rewards using RPS formula (simplified - no division needed)
                BigInteger earned = stake * (rps - paid) + reward;
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, earned);
            }
            new StorageMap(Storage.CurrentContext, PREFIXPAID).Put(account, rps);
            return true;
        }

        // Withdraw NEO - user can withdraw their staked NEO
        public static void Withdraw(BigInteger neoAmount)
        {
            UInt160 caller = (UInt160)Runtime.ExecutingScriptHash;
            ExecutionEngine.Assert(Runtime.CheckWitness(caller));

            BigInteger currentStake = Stake(caller);
            ExecutionEngine.Assert(currentStake >= neoAmount, "Insufficient stake");

            // Reduce user's stake
            new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Put(caller, currentStake - neoAmount);
            new StorageMap(Storage.CurrentContext, PREFIXTOTALSTAKE).Put(TotalStake() - neoAmount);

            // Sync rewards before withdrawal
            SyncAccount(caller);

            // Transfer NEO back to user from Agents
            BigInteger remaining = neoAmount;
            for (BigInteger i = 0; remaining > 0; i++)
            {
                UInt160 agent = Agent(i);
                BigInteger agentBalance = NEO.BalanceOf(agent) - 1; // Keep 1 NEO in agent
                if (agentBalance > remaining)
                {
                    Contract.Call(agent, "transfer", CallFlags.All, new object[] { caller, remaining });
                    break;
                }
                else if (agentBalance > 0)
                {
                    Contract.Call(agent, "transfer", CallFlags.All, new object[] { caller, agentBalance });
                    remaining -= agentBalance;
                }
            }
        }

        // ========== Voting Control (Strategist only) ==========

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

        // ========== Administration (Owner only) ==========

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

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ContractManagement.Update(nefFile, manifest, null);
        }

        public static void ClaimGAS(BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, Owner(), amount));
        }
    }
}
