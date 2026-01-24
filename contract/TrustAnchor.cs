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
        /// Formula: RPS = Σ(GAS_received × 99% / totalStake)
        /// Units: reward units per NEO (scaled by RPS_SCALE = 100,000,000)
        ///
        /// This global counter increases whenever GAS is received, proportional to:
        /// - Current total stake (more stake = slower RPS growth)
        /// - GAS amount received
        ///
        /// Example: 100 GAS received with 1000 NEO total stake
        ///   RPS += 100 × 99,000,000 / 1000 = 9,900,000
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

        /// <summary>User's staked NEO amount</summary>
        private const byte PREFIXSTAKE = 0x08;

        /// <summary>Total staked NEO across all users</summary>
        private const byte PREFIXTOTALSTAKE = 0x09;

        // ========================================
        // Configuration Storage Prefixes
        // ========================================

        private const byte PREFIXCONFIGREADY = 0x10;

        /// <summary>Agent config: [ECPoint target (33 bytes) + BigInteger weight (variable)]</summary>
        private const byte PREFIXAGENTCONFIG = 0x11;

        /// <summary>Config version - increments on each config update</summary>
        private const byte PREFIXCONFIGVERSION = 0x12;

        /// <summary>Flag indicating config session is active</summary>
        private const byte PREFIXPENDINGACTIVE = 0x20;

        /// <summary>Pending agent configs during config session</summary>
        private const byte PREFIXPENDINGCONFIG = 0x21;

        private const byte PREFIXPENDINGOWNER = 0x30;
        private const byte PREFIXOWNERDELAY = 0x31;
        private const byte PREFIXPAUSED = 0x40;

        // ========================================
        // Constants
        // ========================================

        /// <summary>Number of agent contracts (fixed at 21)</summary>
        private const int MAXAGENTS = 21;
        private static readonly BigInteger MAXAGENTS_BIG = MAXAGENTS;

        /// <summary>Total weight must sum to 21 (for equal voting distribution)</summary>
        private static readonly BigInteger TOTALWEIGHT = 21;

        /// <summary>Scale factor for reward calculations (8 decimal places)</summary>
        private static readonly BigInteger RPS_SCALE = 100000000;

        /// <summary>99% of GAS goes to stakers, 1% reserved for contract operations</summary>
        private static readonly BigInteger DEFAULTCLAIMREMAIN = 99000000;

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

        /// <summary>Get current config version</summary>
        public static BigInteger ConfigVersion() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXCONFIGVERSION });

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

        /// <summary>Get agent's voting target address from active config</summary>
        /// <param name="index">Agent index (0-20)</param>
        /// <returns>ECPoint of the candidate this agent votes for</returns>
        public static ECPoint AgentTarget(BigInteger index)
        {
            var mapKey = (ByteString)index;
            var configMap = new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);
            var data = (ByteString)configMap.Get(mapKey);
            return DeserializeAgentConfig_GetTarget((byte[])(object)data);
        }

        /// <summary>Get agent's voting weight from active config</summary>
        /// <param name="index">Agent index (0-20)</param>
        /// <returns>Weight value for this agent</returns>
        public static BigInteger AgentWeight(BigInteger index)
        {
            var mapKey = (ByteString)index;
            var configMap = new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);
            var data = (ByteString)configMap.Get(mapKey);
            return DeserializeAgentConfig_GetWeight((byte[])(object)data);
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
        /// NEO Payment: Mints new user stake and routes NEO to highest-weight agent.
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
                    BigInteger rps = RPS();

                    // Update RPS accumulator with overflow protection:
                    // RPS += amount × 99% / totalStake
                    //
                    // This ensures:
                    // 1. More GAS received = higher RPS
                    // 2. More total stake = slower RPS growth (diluted)
                    // 3. Each staker earns proportional to their stake × RPS increase
                    //
                    // Example: 100 GAS with 1000 NEO stake
                    //   RPS += 100 × 99,000,000 / 1,000 = 9,900,000
                    //   User with 100 NEO (10%) earns: 100 × 9,900,000 / 100,000,000 = 9.9
                    //
                    // SECURITY: Check for overflow in RPS calculation
                    BigInteger rewardShare = amount * DEFAULTCLAIMREMAIN / ts;
                    ExecutionEngine.Assert(rewardShare >= BigInteger.Zero);  // No overflow
                    BigInteger newRps = rps + rewardShare;
                    ExecutionEngine.Assert(newRps >= rps);  // Verify no overflow occurred
                    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, newRps);
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
                AssertConfigReady();

                BigInteger stakeAmount = amount;
                StorageMap stakeMap = new(Storage.CurrentContext, PREFIXSTAKE);
                BigInteger stake = (BigInteger)stakeMap.Get(from);

                // Update user's stake
                stakeMap.Put(from, stake + stakeAmount);

                // Update total stake
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIXTOTALSTAKE }, TotalStake() + stakeAmount);

                // Route NEO to the agent with highest weight
                // This ensures voting power is distributed according to configured weights
                int targetIndex = SelectHighestWeightAgentIndex();
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
        /// But ΔR = GAS × 99% / TotalStake, so:
        /// Σ(reward_increase) = TotalStake × (GAS × 99% / TotalStake) / SCALE
        ///                   = GAS × 99% / SCALE
        ///
        /// This proves: Σ user rewards = 99% of GAS received
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
        /// 4. Withdraw NEO from agents (lowest weight first)
        ///
        /// Withdrawal Strategy:
        /// - Withdraw from agents with lowest weights first
        /// - This preserves voting weight distribution
        /// - Prevents emptying high-weight agents disproportionately
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

            AssertConfigReady();

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
            // Strategy: Withdraw from agents in order of lowest weight first
            // This minimizes disruption to voting weight distribution
            //
            // Example: If weights are [5,4,3,2,1] and user withdraws 3 NEO:
            //   - Take 2 from weight 1 agent (now 0)
            //   - Take 1 from weight 2 agent (now 1)
            //   Result: Weights become [5,4,3,1,0] (high weights preserved)

            BigInteger remaining = neoAmount;
            bool[] used = new bool[MAXAGENTS];  // Track which agents we've already withdrawn from

            for (int step = 0; step < MAXAGENTS && remaining > BigInteger.Zero; step++)
            {
                // Select the lowest weight agent that hasn't been used yet
                int selected = SelectLowestWeightAgentIndex(used);
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

            // Verify all agents have zero balance (agent contracts are empty)
            for (int i = 0; i < MAXAGENTS; i++)
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
        // Configuration Management
        // ========================================

        /// <summary>
        /// Begin a configuration session to update agent targets and weights.
        ///
        /// Process:
        /// 1. Set PENDINGACTIVE flag
        /// 2. Copy current active config to pending config
        /// 3. Allow incremental updates via SetAgentConfig/SetAgentTarget/SetAgentWeight
        /// 4. Call FinalizeConfig to atomically apply all changes
        ///
        /// This enables safe, batched configuration updates without
        /// leaving the contract in an inconsistent state.
        /// </summary>
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

        /// <summary>Set both target and weight for an agent during config session</summary>
        /// <param name="index">Agent index (0-20)</param>
        /// <param name="target">Voting target (candidate ECPoint)</param>
        /// <param name="weight">Voting weight (must sum to 21 with all agents)</param>
        public static void SetAgentConfig(BigInteger index, ECPoint target, BigInteger weight)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(index >= 0 && index < MAXAGENTS_BIG);
            
            // SECURITY: Enhanced input validation
            ExecutionEngine.Assert(weight >= BigInteger.Zero && weight <= TOTALWEIGHT, "Weight must be 0-21");
            
            // Validate ECPoint format (should be 33 bytes in compressed form)
            var targetBytes = (byte[])(object)target;
            ExecutionEngine.Assert(targetBytes is not null && targetBytes.Length == 33, "Invalid ECPoint format");

            var data = SerializeAgentConfig(target, weight);
            new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG).Put((ByteString)index, data);
        }

        /// <summary>Batch set all 21 agent targets and weights at once</summary>
        public static void SetAgentConfigs(ECPoint[] targets, BigInteger[] weights)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(targets is not null && weights is not null);
            ExecutionEngine.Assert(targets.Length == MAXAGENTS && weights.Length == MAXAGENTS);

            var pendingMap = new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG);
            
            for (int i = 0; i < MAXAGENTS; i++)
            {
                ExecutionEngine.Assert(weights[i] >= BigInteger.Zero && weights[i] <= TOTALWEIGHT, "Weight must be 0-21");
                
                // Validate ECPoint format
                var targetBytes = (byte[])(object)targets[i];
                ExecutionEngine.Assert(targetBytes is not null && targetBytes.Length == 33, "Invalid ECPoint format");
                
                // SECURITY: Check for duplicate targets (O(n^2) but n=21 is small)
                for (int j = 0; j < i; j++)
                {
                    var prevBytes = (byte[])(object)targets[j];
                    if (prevBytes is not null && targetBytes is not null)
                    {
                        bool isDuplicate = true;
                        for (int k = 0; k < 33; k++)
                        {
                            if (prevBytes[k] != targetBytes[k])
                            {
                                isDuplicate = false;
                                break;
                            }
                        }
                        ExecutionEngine.Assert(!isDuplicate, "Duplicate target detected");
                    }
                }
                
                var data = SerializeAgentConfig(targets[i], weights[i]);
                pendingMap.Put(AgentKey(i), data);
            }
        }

        /// <summary>Update only the target for a specific agent (preserves current weight)</summary>
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

        /// <summary>Update only the weight for a specific agent (preserves current target)</summary>
        public static void SetAgentWeight(BigInteger index, BigInteger weight)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());
            ExecutionEngine.Assert(index >= 0 && index < MAXAGENTS_BIG);
            
            // SECURITY: Validate weight bounds
            ExecutionEngine.Assert(weight >= BigInteger.Zero && weight <= TOTALWEIGHT, "Weight must be 0-21");

            // Preserve current target, update weight only
            var currentTarget = AgentConfig_GetTarget(index);
            var targetBytes = (byte[])(object)currentTarget;
            if (targetBytes is null || targetBytes.Length == 0)
            {
                targetBytes = new byte[33]; // Placeholder if no target exists
            }
            var target = (ECPoint)(object)targetBytes;
            var data = SerializeAgentConfig(target, weight);
            new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG).Put((ByteString)index, data);
        }

        /// <summary>Update weights for all agents at once (preserves all targets)</summary>
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
                    targetBytes = new byte[33];
                }
                var target = (ECPoint)(object)targetBytes;
                var data = SerializeAgentConfig(target, weight);
                pendingMap.Put(AgentKey(i), data);
            }
        }

        /// <summary>
        /// Finalize configuration session - atomically apply all pending configs.
        ///
        /// Process:
        /// 1. Validate all configs (weights sum to 21, no duplicate targets)
        /// 2. Atomically apply all pending configs to active config
        /// 3. Mark contract as ready for operations
        /// 4. Clear pending config and session flags
        ///
        /// Validation Rules:
        /// - All 21 agents must have valid configs
        /// - Weights must sum to exactly 21
        /// - No duplicate voting targets (ECPoints)
        /// </summary>
        public static void FinalizeConfig()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(IsPendingConfigActive());

            var pendingMap = new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG);
            var activeMap = new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);

            // ========================================
            // Step 1: Collect and Validate Configs
            // ========================================
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

                // Store for later use
                var tb = (byte[])(object)target;
                targetBytesList[i] = tb;
                weightList[i] = weight;

                // Check for duplicate targets (prevent voting for same candidate twice)
                for (int j = 0; j < i; j++)
                {
                    var prevTargetBytes = targetBytesList[j];
                    if (prevTargetBytes is null || tb is null) continue;

                    // Compare all 33 bytes of ECPoint
                    bool match = true;
                    for (int k = 0; k < 33; k++)
                    {
                        if (tb[k] != prevTargetBytes[k])
                        {
                            match = false;
                            break;
                        }
                    }
                    ExecutionEngine.Assert(!match);  // Duplicate targets not allowed
                }
            }

            // Validate weights sum to 21
            ExecutionEngine.Assert(totalWeight == TOTALWEIGHT);

            // ========================================
            // Step 2: Atomically Apply All Configs
            // ========================================
            for (int i = 0; i < MAXAGENTS; i++)
            {
                var tb = targetBytesList[i];
                var target = tb is null ? default : (ECPoint)(object)tb;
                var data = SerializeAgentConfig(target, weightList[i]);
                activeMap.Put(AgentKey(i), data);
                pendingMap.Delete(AgentKey(i));
            }

            // Increment config version (tracks configuration history)
            var currentVersion = ConfigVersion();
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXCONFIGVERSION }, currentVersion + 1);

            // Clear pending session flags and mark as ready
            Storage.Delete(Storage.CurrentContext, new byte[] { PREFIXPENDINGACTIVE });
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIXCONFIGREADY }, 1);
        }

        // ========================================
        // Core Logic: Rebalancing
        // ========================================

        /// <summary>
        /// Rebalance NEO and votes across agents according to their weights.
        ///
        /// Purpose:
        /// - Ensures NEO is distributed among agents proportionally to weights
        /// - Updates all agent votes to current targets
        ///
        /// Process:
        /// 1. Collect all agent balances and configs
        /// 2. Calculate target balance for each agent (total × weight / 21)
        /// 3. Transfer NEO between agents to achieve target distribution
        /// 4. Vote all agents to their configured targets
        ///
        /// Example:
        /// - Agent 0: weight 5,  NEO 100
        /// - Agent 1: weight 4, NEO 80
        /// - Total NEO: 180
        /// - Agent 0 target: 180 × 5/21 = 42.86 → should have 43
        /// - Agent 1 target: 180 × 4/21 = 34.29 → should have 34
        /// - Transfer: Agent 0 sends 2 to Agent 1
        /// </summary>
        public static void RebalanceVotes()
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            AssertConfigReady();

            // ========================================
            // Step 1: Collect Current State
            // ========================================
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

            // ========================================
            // Step 2: Calculate Target Balances
            // ========================================
            var targetBalances = new BigInteger[MAXAGENTS];
            BigInteger allocated = 0;

            for (int i = 0; i < MAXAGENTS; i++)
            {
                // Target = total × weight / 21 (proportional distribution)
                targetBalances[i] = total * weights[i] / TOTALWEIGHT;
                allocated += targetBalances[i];
            }

            // Distribute rounding remainder to highest weight agent
            BigInteger remainder = total - allocated;
            if (remainder > BigInteger.Zero)
            {
                int highest = SelectHighestWeightAgentIndex();
                targetBalances[highest] += remainder;
            }

            // ========================================
            // Step 3: Calculate Deficits and Excesses
            // ========================================
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

            // ========================================
            // Step 4: Transfer NEO to Balance
            // ========================================
            // Match deficits with excesses
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

            // ========================================
            // Step 5: Vote All Agents
            // ========================================
            for (int i = 0; i < MAXAGENTS; i++)
            {
                Contract.Call(agents[i], "vote", CallFlags.All,
                    new object[] { targets[i] });
            }
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

        /// <summary>Set agent contract address for an index</summary>
        public static void SetAgent(BigInteger i, UInt160 agent)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(i >= 0 && i < MAXAGENTS_BIG);
            ExecutionEngine.Assert(agent != UInt160.Zero);
            
            // SECURITY: Basic agent address validation
            // Full contract verification happens during contract deployment
            
            // Store agent address
            new StorageMap(Storage.CurrentContext, PREFIXAGENT).Put((ByteString)i, agent);
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
            ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
            ExecutionEngine.Assert(amount > BigInteger.Zero);
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, Owner(), amount));
        }

        // ========================================
        // Helper Methods
        // ========================================

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

        /// <summary>Get agent's target address from storage (pending if config active, else active)</summary>
        private static ECPoint AgentConfig_GetTarget(BigInteger index)
        {
            var mapKey = (ByteString)index;
            var configMap = IsPendingConfigActive()
                ? new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG)
                : new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);
            var data = (ByteString)configMap.Get(mapKey);
            return DeserializeAgentConfig_GetTarget((byte[])(object)data);
        }

        /// <summary>Get agent's weight from storage (pending if config active, else active)</summary>
        private static BigInteger AgentConfig_GetWeight(BigInteger index)
        {
            var mapKey = (ByteString)index;
            var configMap = IsPendingConfigActive()
                ? new StorageMap(Storage.CurrentContext, PREFIXPENDINGCONFIG)
                : new StorageMap(Storage.CurrentContext, PREFIXAGENTCONFIG);
            var data = (ByteString)configMap.Get(mapKey);
            return DeserializeAgentConfig_GetWeight((byte[])(object)data);
        }

        /// <summary>Select agent with highest voting weight</summary>
        /// <returns>Agent index (0-20)</returns>
        private static int SelectHighestWeightAgentIndex()
        {
            BigInteger bestWeight = BigInteger.MinusOne;
            int bestIndex = -1;

            for (int i = 0; i < MAXAGENTS; i++)
            {
                BigInteger weight = AgentConfig_GetWeight(i);
                if (weight <= BigInteger.Zero) continue;

                // Select highest weight (or lowest index if tie)
                if (weight > bestWeight || (weight == bestWeight && (bestIndex < 0 || i < bestIndex)))
                {
                    bestWeight = weight;
                    bestIndex = i;
                }
            }
            ExecutionEngine.Assert(bestIndex >= 0);
            return bestIndex;
        }

        /// <summary>Select agent with lowest weight (excluding already used ones)</summary>
        /// <param name="used">Array marking which agents have been used</param>
        /// <returns>Agent index, or -1 if none available</returns>
        private static int SelectLowestWeightAgentIndex(bool[] used)
        {
            int selected = -1;
            BigInteger selectedWeight = BigInteger.Zero;

            for (int i = 0; i < MAXAGENTS; i++)
            {
                if (used[i]) continue;
                BigInteger weight = AgentConfig_GetWeight(i);
                if (weight <= BigInteger.Zero) continue;

                // Select lowest weight (or lowest index if tie)
                if (selected < 0 || weight < selectedWeight || (weight == selectedWeight && i < selected))
                {
                    selected = i;
                    selectedWeight = weight;
                }
            }
            return selected;
        }

        // ========================================
        // Serialization Helpers
        // ========================================

        /// <summary>
        /// Serialize agent configuration as [ECPoint(33 bytes) + weight bytes].
        /// ECPoint is stored in compressed SEC format (33 bytes).
        /// Weight is serialized as BigInteger (minimum 1 byte).
        /// </summary>
        private static byte[] SerializeAgentConfig(ECPoint target, BigInteger weight)
        {
            var targetBytes = (byte[])(object)target;
            if (targetBytes is null || targetBytes.Length != 33)
            {
                targetBytes = new byte[33]; // All zeros = invalid placeholder
            }

            var weightBytes = weight.ToByteArray();
            // Ensure at least 1 byte for weight (zero → {0} not {})
            if (weightBytes.Length == 0)
            {
                weightBytes = new byte[] { 0 };
            }

            // Combine: [target (33 bytes) | weight (variable)]
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

        /// <summary>Extract ECPoint from config (first 33 bytes)</summary>
        private static ECPoint DeserializeAgentConfig_GetTarget(byte[] data)
        {
            if (data is null || data.Length < 33)
                return default;
            var targetBytes = new byte[33];
            for (int i = 0; i < 33; i++) targetBytes[i] = data[i];
            return (ECPoint)(object)targetBytes;
        }

        /// <summary>Extract weight from config (bytes after first 33)</summary>
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
