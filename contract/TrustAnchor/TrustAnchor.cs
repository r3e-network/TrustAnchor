using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace TrustAnchor
{
#pragma warning disable CS8604 // NEO Storage.Get returns nullable, handled by contract logic
#pragma warning disable CS8602 // NEO framework nullable dereferences are safe
#pragma warning disable CS8625 // Null literals for NEO framework compatibility
#pragma warning disable CS8603 // NEO framework returns are handled

    [ManifestExtra("Author", "R3E Network")]
    [ManifestExtra("Email", "developer@r3e.network")]
    [ManifestExtra("Description", "TrustAnchor: Non-profit voting delegation for active contributors and reputable community members")]
    [ContractPermission("*", "transfer", "vote", "sync", "claim", "isPaused", "owner")]
    [ContractPermission("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5", "transfer", "vote")]
    [ContractPermission("0xd2a4cff31913016155e38e474a2c06d08be276cf", "transfer")]
    [ContractPermission("0xfffdc93764dbaddd97c48f252a53ea4643faa3fd", "update", "getContract")]
    public partial class TrustAnchor : SmartContract
    {
        // ========================================
        // Contract Lifecycle
        // ========================================

        /// <summary>Contract deployment initialization</summary>
        public static void _deploy(object data, bool update)
        {
            if (update) return;
            ExecutionEngine.Assert(DEFAULT_OWNER != UInt160.Zero, "Owner not set");
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, DEFAULT_OWNER);
        }

        // ========================================
        // Core Logic: Payment & Reward Distribution
        // ========================================

        /// <summary>
        /// Handles NEO and GAS payments to the contract.
        ///
        /// GAS Payment: Updates the RPS accumulator to distribute rewards to stakers.
        /// NEO Payment: Mints new user stake and routes NEO to highest voting agent (priority only).
        /// </summary>
        /// <param name="from">Sender address</param>
        /// <param name="amount">Token amount</param>
        /// <param name="data">Optional data field</param>
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // ========================================
            // Part 1: GAS Reward Distribution
            // ========================================
            if (Runtime.CallingScriptHash == GAS.Hash && amount > BigInteger.Zero)
            {
                BigInteger ts = TotalStake();
                if (ts > BigInteger.Zero)
                {
                    DistributeReward(amount, ts);
                }
                else
                {
                    AddPendingReward(amount);
                }
            }

            // ========================================
            // Part 2: Sync User Rewards (if sender exists)
            // ========================================
            if (from is null || from == UInt160.Zero) return;

            // Always sync the sender's account before any operation
            // This captures any pending rewards they've earned from previous RPS increases
            SyncAccount(from);

            // ========================================
            // Part 3: Handle NEO Deposit (Staking)
            // ========================================
            if (Runtime.CallingScriptHash == NEO.Hash && amount > BigInteger.Zero)
            {
                if (IsRegisteredAgent(from))
                {
                    ExecutionEngine.Assert(IsPaused(), "Agent return only allowed while paused");
                    return;
                }

                ExecutionEngine.Assert(!IsPaused(), "Contract is paused");
                ExecutionEngine.Assert(AgentCount() > BigInteger.Zero);

                BigInteger stakeAmount = amount;
                StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
                BigInteger stake = (BigInteger)stakeMap.Get(from);
                BigInteger previousTotalStake = TotalStake();

                // Update user's stake
                stakeMap.Put(from, stake + stakeAmount);

                // Update total stake
                BigInteger newTotalStake = previousTotalStake + stakeAmount;
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, newTotalStake);

                if (previousTotalStake == BigInteger.Zero)
                {
                    BigInteger pending = PendingReward();
                    if (pending > BigInteger.Zero)
                    {
                        DistributeReward(pending, newTotalStake);
                        Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGREWARD });
                    }
                }

                // Route NEO to the agent with highest voting amount (priority only)
                int targetIndex = SelectHighestVotingAgentIndex();
                UInt160 targetAgent = Agent(targetIndex);
                ExecutionEngine.Assert(targetAgent != UInt160.Zero);

                // Transfer only the deposited NEO amount to the agent contract
                // SECURITY: Do NOT use BalanceOf(self) — that would sweep any NEO
                // returned during emergency drain, causing accounting mismatch
                ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, targetAgent, amount));
            }
        }

        // ========================================
        // Core Logic: Withdrawal
        // ========================================

        /// <summary>
        /// Withdraw staked NEO from the contract.
        ///
        /// Process:
        /// 1. Sync user's rewards first (capture pending rewards)
        /// 2. Verify user has enough staked NEO
        /// 3. Reduce user's stake
        /// 4. Withdraw NEO from agents (lowest voting amount first)
        ///
        /// Withdrawal Strategy:
        /// - Withdraw from agents with lowest voting amounts first
        /// - This preserves manual priority ordering
        /// - Prevents emptying high-priority agents disproportionately
        /// </summary>
        /// <param name="account">User withdrawing NEO</param>
        /// <param name="neoAmount">Amount of NEO to withdraw</param>
        public static void Withdraw(UInt160 account, BigInteger neoAmount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            ExecutionEngine.Assert(neoAmount > BigInteger.Zero);

            // IMPORTANT: Sync rewards BEFORE modifying stake
            // This ensures user earns rewards up to the withdrawal moment
            SyncAccount(account);

            BigInteger stakeAmount = neoAmount;
            StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
            BigInteger stake = (BigInteger)stakeMap.Get(account);

            // Verify sufficient stake
            ExecutionEngine.Assert(stake >= stakeAmount);

            // Update user's stake
            stakeMap.Put(account, stake - stakeAmount);

            // Update total stake
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() - stakeAmount);

            // ========================================
            // NEO Withdrawal from Agents
            // ========================================
            // Strategy: Withdraw from agents in order of lowest voting amount first
            // This minimizes disruption to manual voting priority
            //
            // Example: If voting amounts are [5,4,3,2,1] and user withdraws 3 NEO:
            //   - Take 2 from voting 1 agent (now 0)
            //   - Take 1 from voting 2 agent (now 1)
            //   Result: Voting amounts unchanged (priority preserved)

            BigInteger remaining = neoAmount;
            int count = (int)AgentCount();
            bool[] used = new bool[count];  // Track which agents we've already withdrawn from

            for (int step = 0; step < count && remaining > BigInteger.Zero; step++)
            {
                // Select the lowest voting agent that hasn't been used yet
                int selected = SelectLowestVotingAgentIndex(used);
                if (selected < 0) break;  // No more agents available
                used[selected] = true;

                UInt160 agent = Agent(selected);
                ExecutionEngine.Assert(agent != UInt160.Zero);

                // Check agent's NEO balance
                BigInteger balance = NEO.BalanceOf(agent);
                if (balance <= BigInteger.Zero) continue;

                // Calculate amount to withdraw (min of remaining needed or agent balance)
                BigInteger transferAmount = remaining > balance ? balance : remaining;

                if (transferAmount > BigInteger.Zero)
                {
                    // Call agent's transfer method to send NEO back to user
                    Contract.Call(agent, "transfer", CallFlags.States | CallFlags.AllowCall,
                        new object[] { account, transferAmount });
                    remaining -= transferAmount;
                }
            }

            // Ensure all requested NEO was withdrawn
            ExecutionEngine.Assert(remaining == BigInteger.Zero);
        }

        /// <summary>
        /// Emergency withdraw function for extreme scenarios.
        /// SECURITY: Only callable when:
        /// 1. Contract is paused (by owner)
        /// 2. All agent contracts have zero NEO balance
        /// 3. User has staked NEO in the contract
        /// 
        /// This prevents permanent fund lock when all agents are empty.
        /// </summary>
        /// <param name="account">User requesting emergency withdraw</param>
        public static void EmergencyWithdraw(UInt160 account)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account) || Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPaused()); // Only when paused
            
            BigInteger stake = StakeOf(account);
            ExecutionEngine.Assert(stake > BigInteger.Zero); // Must have stake

            // Verify all registered agents have zero balance (agent contracts are empty)
            int count = (int)AgentCount();
            for (int i = 0; i < count; i++)
            {
                UInt160 agent = Agent(i);
                if (agent != null && agent != UInt160.Zero)
                {
                    BigInteger balance = (BigInteger)Contract.Call(NEO.Hash, "balanceOf", CallFlags.ReadStates, agent);
                    ExecutionEngine.Assert(balance == BigInteger.Zero, "All agents must be empty");
                }
            }

            // Sync rewards before withdrawal
            SyncAccount(account);

            // Update user's stake
            new StorageMap(Storage.CurrentContext, PREFIXSTAKE).Put(account, BigInteger.Zero);

            // Update total stake
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() - stake);

            // IMPORTANT: Direct NEO transfer from contract
            // This bypasses agent contracts since they are all empty
            // User gets their NEO back, though this is a rare emergency scenario
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, account, stake));
        }

        // ========================================
        // Owner Transfer
        // ========================================

        /// <summary>
        /// Propose ownership transfer (two-step pattern).
        /// The new owner must call AcceptOwner() to complete the transfer.
        /// </summary>
        public static void ProposeOwner(UInt160 newOwner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(newOwner != UInt160.Zero);
            ExecutionEngine.Assert(newOwner != Owner());
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER }, newOwner);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNERTRANSFERTIME }, Runtime.Time);
        }

        /// <summary>
        /// Accept ownership transfer. Must be called by the pending owner.
        /// </summary>
        public static void AcceptOwner()
        {
            var pending = Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER });
            ExecutionEngine.Assert(pending is not null, "No pending owner");
            UInt160 pendingOwner = (UInt160)(byte[])pending;
            ExecutionEngine.Assert(Runtime.CheckWitness(pendingOwner));

            // Transfer ownership
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNER }, pendingOwner);

            // Clear pending state
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER });
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXOWNERTRANSFERTIME });
        }

        /// <summary>Cancel a pending ownership transfer (owner only)</summary>
        public static void CancelOwnerProposal()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER });
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXOWNERTRANSFERTIME });
        }

        /// <summary>Update contract (migration)</summary>
        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(!IsPaused());
            ContractManagement.Update(nefFile, manifest, null);
        }

        /// <summary>Pause contract (emergency stop)</summary>
        public static void Pause()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPAUSED }, 1);
        }

        /// <summary>Unpause contract</summary>
        public static void Unpause()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPAUSED });
        }
    }
}
