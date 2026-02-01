using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
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
        // Reward Helpers
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
    }
}
