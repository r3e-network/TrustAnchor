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
    [ManifestExtra("Description", "TrustAnchor: Non-profit voting delegation for active contributors and reputable community members")]
    [ContractPermission("*", "transfer", "vote", "sync", "claim")]
    [ContractPermission("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5", "transfer", "vote")]
    [ContractPermission("0xd2a4cff31913016155e38e474a2c06d08be276cf", "transfer")]
    [ContractPermission("0xfffdc93764dbaddd97c48f252a53ea4643faa3fd", "update")]
    public class TrustAnchor : SmartContract
    {
        // ========================================
        // Storage Prefixes
        // ========================================

        /// <summary>Owner address (time-locked transfer support)</summary>
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

        private const byte PREFIXPENDINGOWNER = 0x30;
        private const byte PREFIXOWNERDELAY = 0x31;
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

        /// <summary>3 day delay for owner transfer (security mechanism)</summary>
        private static readonly BigInteger OWNER_CHANGE_DELAY = 3 * 24 * 3600;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_OWNER = default;

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
            var data = (ByteString)new StorageMap(Storage.CurrentContext, PREFIXAGENT_TARGET).Get((ByteString)index);
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

        // ========================================
        // Contract Lifecycle
        // ========================================

        /// <summary>Contract deployment initialization</summary>
        public static void _deploy(object data, bool update)
        {
            if (update) return;
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

                // Transfer NEO to the agent contract
                ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, targetAgent, NEO.BalanceOf(Runtime.ExecutingScriptHash)));
            }
        }

        // ========================================
        // Core Logic: Reward Accounting
        // ========================================

        /// <summary>
        /// Sync a user's rewards by calculating their earned share of RPS increases since last sync.
        ///
        /// MATHEMATICAL PROOF OF CORRECTNESS:
        ///
        /// Let:
        /// - s = user's stake
        /// - R_current = current RPS
        /// - R_last = user's paid value (RPS at last sync)
        /// - ΔR = R_current - R_last (RPS increase since last sync)
        ///
        /// User's reward increase = s × ΔR / SCALE
        ///
        /// For all users, Σ(reward_increase) = Σ(s_i × ΔR / SCALE)
        ///                              = (Σ s_i) × ΔR / SCALE
        ///                              = TotalStake × ΔR / SCALE
        ///
        /// But ΔR = GAS × 100% / TotalStake, so:
        /// Σ(reward_increase) = TotalStake × (GAS × 100% / TotalStake) / SCALE
        ///                   = GAS × 100% / SCALE
        ///
        /// This proves: Σ user rewards = 100% of GAS received
        /// No user can earn more than their fair share!
        /// </summary>
        /// <param name="account">User address to sync</param>
        /// <returns>Always returns true</returns>
        public static bool SyncAccount(UInt160 account)
        {
            BigInteger rps = RPS();                    // Current global RPS accumulator
            BigInteger stake = StakeOf(account);       // User's staked NEO

            if (stake > BigInteger.Zero)
            {
                BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
                BigInteger paid = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXPAID).Get(account);

                // Calculate user's earned rewards:
                // earned = stake × (currentRPS - lastPaidRPS) / SCALE + previousReward
                //
                // This calculates the user's fair share of all RPS increases
                // from their last sync point (paid) to current (rps).
                //
                // Example:
                // - User stakes 100 NEO at RPS=0
                // - RPS grows to 9,900,000 (GAS received)
                // - User's earned = 100 × 9,900,000 / 100,000,000 = 9.9
                BigInteger earned = stake * (rps - paid) / RPS_SCALE + reward;

                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, earned);
            }

            // Update user's paid to current RPS (marking this RPS as "accounted for")
            // Next sync will only count RPS increases from this point forward
            new StorageMap(Storage.CurrentContext, PREFIXPAID).Put(account, rps);

            return true;
        }

        /// <summary>
        /// Claim accumulated GAS rewards for a user.
        ///
        /// Process:
        /// 1. Sync account to calculate latest rewards
        /// 2. Transfer available GAS to user
        /// 3. Reset reward balance to zero
        /// </summary>
        /// <param name="account">User claiming rewards</param>
        public static void ClaimReward(UInt160 account)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));

            // Sync first to capture any pending rewards
            SyncAccount(account);

            BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
            if (reward > BigInteger.Zero)
            {
                // Reset reward balance after claiming
                new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, 0);

                // Transfer GAS to user
                ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, account, reward));
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
                    // Call agent's transfer method to send NEO back to contract
                    Contract.Call(agent, "transfer", CallFlags.All,
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
        // Agent Registry
        // ========================================

        /// <summary>Register a new agent contract with target and name</summary>
        public static void RegisterAgent(UInt160 agent, ECPoint target, string name)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(agent != UInt160.Zero);

            ExecutionEngine.Assert(!string.IsNullOrEmpty(name));
            ExecutionEngine.Assert(Utf8Length(name) <= 32);

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
            var current = (ByteString)targetMap.Get((ByteString)index);
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
            ExecutionEngine.Assert(Utf8Length(name) <= 32);

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

        // ========================================
        // Owner Transfer Security
        // ========================================

        /// <summary>Initiate time-delayed owner transfer (3 day wait period)</summary>
        public static void InitiateOwnerTransfer(UInt160 newOwner)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(newOwner != UInt160.Zero);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGOWNER }, newOwner);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXOWNERDELAY }, Runtime.Time + OWNER_CHANGE_DELAY);
        }

        /// <summary>Accept owner transfer after delay period expires</summary>
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

        /// <summary>Withdraw GAS from contract (e.g., for operations or testing)</summary>
        public static void WithdrawGAS(BigInteger amount)
        {
            // Disabled: rewards must remain with stakers.
            ExecutionEngine.Assert(false);
        }

        // ========================================
        // Helper Methods
        // ========================================

        private static BigInteger PendingReward()
        {
            return (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPENDINGREWARD });
        }

        private static void AddPendingReward(BigInteger amount)
        {
            BigInteger pending = PendingReward();
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGREWARD }, pending + amount);
        }

        private static void DistributeReward(BigInteger amount, BigInteger totalStake)
        {
            ExecutionEngine.Assert(totalStake > BigInteger.Zero);
            BigInteger rps = RPS();

            // Update RPS accumulator with overflow protection:
            // RPS += amount × 100% / totalStake
            //
            // This ensures:
            // 1. More GAS received = higher RPS
            // 2. More total stake = slower RPS growth (diluted)
            // 3. Each staker earns proportional to their stake × RPS increase
            //
            // Example: 100 GAS with 1000 NEO stake
            //   RPS += 100 × 100,000,000 / 1,000 = 10,000,000
            //   User with 100 NEO (10%) earns: 100 × 9,900,000 / 100,000,000 = 9.9
            //
            // SECURITY: Check for overflow in RPS calculation
            BigInteger rewardShare = amount * DEFAULTCLAIMREMAIN / totalStake;
            ExecutionEngine.Assert(rewardShare >= BigInteger.Zero);  // No overflow
            BigInteger newRps = rps + rewardShare;
            ExecutionEngine.Assert(newRps >= rps);  // Verify no overflow occurred
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, newRps);
        }

        private static int Utf8Length(string value)
        {
            int total = 0;
            for (int i = 0; i < value.Length; i++)
            {
                int ch = value[i];
                if (ch < 0x80)
                {
                    total += 1;
                    continue;
                }

                if (ch < 0x800)
                {
                    total += 2;
                    continue;
                }

                if (ch >= 0xD800 && ch <= 0xDBFF)
                {
                    ExecutionEngine.Assert(i + 1 < value.Length, "Invalid UTF-16");
                    int low = value[i + 1];
                    ExecutionEngine.Assert(low >= 0xDC00 && low <= 0xDFFF, "Invalid UTF-16");
                    total += 4;
                    i++;
                    continue;
                }

                ExecutionEngine.Assert(!(ch >= 0xDC00 && ch <= 0xDFFF), "Invalid UTF-16");
                total += 3;
            }
            return total;
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

        private static bool IsPaused()
        {
            return Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPAUSED }) is not null;
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
