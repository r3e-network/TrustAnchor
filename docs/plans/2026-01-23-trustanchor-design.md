# TrustAnchor design (legacy-derived, no deposit token)

## Context
A legacy system uses a NEP-17 deposit token to represent deposits and distribute GAS rewards via an on-chain reward-per-share (RPS) mechanism. Voting decisions (who to vote for and how much) are computed off-chain in the TEE strategist tool and submitted as `trigVote`/`trigTransfer` calls. The goal is to remove the deposit token and the existing profit-based voting heuristic, while keeping the operational flow, reward distribution, and TEE-driven voting.

## Goals
- Replace the legacy deposit token with an internal deposit ledger tied to NEO deposits.
- Keep GAS reward distribution using the same RPS/Reward/Paid mechanism.
- Keep the agent/strategist/whitelist control flow on-chain.
- Move voting decisions entirely off-chain via a config file with weights.
- Preserve the TEE operational pattern (claimer/representative/strategist).

## Non-goals
- No on-chain NEP-17 behavior or transferability.
- No on-chain voting heuristics or optimization logic.
- No change to the agent pattern or the TEE deployment model.

## Architecture
### On-chain: TrustAnchor (new core contract)
- Roles: `Owner`, `Strategist`, `Agent(i)` and candidate whitelist (same as the legacy system).
- Internal stake ledger: `Stake(account)` and `TotalStake` stored in contract storage.
- GAS reward accounting: `RPS`, `Reward(account)`, `Paid(account)` using the same formula as the legacy system.
- Voting controls:
  - `TrigVote(i, candidate)` only by strategist and only for whitelisted candidates.
  - `TrigTransfer(i, j, amount)` only by strategist.
- Administration: set owner/strategist/agents, whitelist candidates, update contract.

### Off-chain: TEE tools
- `TrustAnchorStrategist`: rewritten to load a weight-based config file and compute target vote allocations.
- `TrustAnchorClaimer`/`TrustAnchorRepresentative`: keep existing logic but target the TrustAnchor script hash.
- Config is updated via GitHub, and the TEE process is restarted to apply changes.

## Contract behavior
### Deposits
- `OnNEP17Payment` handles NEO deposits only.
- When NEO is sent to TrustAnchor:
  - Credit `Stake(sender)` by `amount * 1e8` (same scaling as the legacy system).
  - Increase `TotalStake` by the same amount.
  - Transfer the NEO to `Agent(0)` (existing flow).
- On GAS transfers to TrustAnchor:
  - Increase `RPS` by `amount * DEFAULT_CLAIM_REMAIN / TotalStake`.

### Rewards
- `SyncAccount(account)` uses `Stake(account)` instead of token balance.
- `ClaimReward()` pays GAS reward to the caller and resets stored reward.

### Withdrawals
- `Withdraw(neoAmount)` callable by the depositor (witness required).
- Decrease `Stake(caller)` by `neoAmount * 1e8` (revert if insufficient).
- Transfer NEO out by iterating over agents and calling `agent.transfer`.
- Preserve the existing “leave 1 NEO in agent” behavior unless explicitly removed.

## Voting config (off-chain)
### Config format
- File path provided by `VOTE_CONFIG` env var (or `--config`).
- Format is JSON or YAML:

```yaml
candidates:
  - pubkey: "03ab..."
    weight: 4
  - pubkey: "02cd..."
    weight: 3
  - pubkey: "02ef..."
    weight: 2
  - pubkey: "03aa..."
    weight: 1
```

### Validation
- Sum of weights must be exactly `21` (hard fail otherwise).
- No duplicate pubkeys; no zero/negative weights.
- All pubkeys must be whitelisted on-chain (fail fast if not).

### Allocation algorithm
- Compute total voting power as the sum of NEO held by all agents.
- Target vote per candidate: `target = totalPower * weight / 21`.
- Assign agents to candidates to meet targets using a greedy allocator:
  - Sort agents by current holdings (desc).
  - Repeatedly assign the largest agent to the candidate with the largest remaining target.
  - Compute diffs between current and target holdings per agent.
- Build `trigVote` calls for any agent whose target candidate changes.
- Build `trigTransfer` calls to rebalance holdings to target per candidate.
- If no changes are required, skip transaction broadcast.

## Error handling
- Fail fast on invalid config or unwhitelisted candidates.
- Log computed targets, diffs, and planned actions before broadcasting.
- Add a dry-run mode to print planned actions without sending a transaction.

## Testing
### On-chain
- Deposit/withdraw consistency and stake accounting.
- GAS reward distribution via RPS matching legacy behavior.
- Strategist-only enforcement for vote/transfer.

### Off-chain
- Config validation (sum == 21, duplicates, negative weights).
- Correct target computation and rounding.
- No-op when already aligned.

## Rollout
1. Deploy TrustAnchor.
2. Set owner/strategist and register agents.
3. Whitelist candidate pubkeys.
4. Point TEE tools at TrustAnchor hash.
5. Apply config changes via GitHub and restart TEE.
