# TrustAnchor on-chain voting design (no strategist/TEE)

## Context
TrustAnchor currently relies on a strategist role and TEE tooling to decide votes (`trigVote`) and rebalance agent funds (`trigTransfer`). We want to remove the strategist and make voting purely on-chain. The owner will periodically rebalance using on-chain configuration that sets a target candidate and weight per agent. Deposits and withdrawals should route deterministically based on these weights.

## Goals
- Remove strategist/whitelist flows from the core contract.
- Add an owner-managed, multi-transaction config flow for per-agent target and weight.
- Keep the existing stake ledger and GAS reward-per-share behavior intact.
- Route deposits to the highest-weight target; withdraw from the lowest non-zero weight targets first.
- Rebalance on-chain: transfer NEO between agents and apply votes automatically.

## Non-goals
- No TEE strategist logic or off-chain vote planning.
- No new token or staking model changes.
- No automatic (timer-based) rebalancing.

## Architecture
### On-chain config
- Owner calls `BeginConfig()` to clear pending agent targets/weights.
- Owner calls `SetAgentConfig(agentIndex, candidate, weight)` for each agent (0-20).
- Owner calls `FinalizeConfig()` to validate and activate the configuration.

Validation rules:
- All agents 0-20 must be configured.
- Sum of weights must equal 21.
- Weights must be >= 0.
- Candidate pubkeys must be unique (21 candidates / 21 agents).

### Deposits
- NEO deposits update `Stake`/`TotalStake` as before.
- Contract transfers all NEO to the agent with the highest weight (tie-break by lowest agent index).

### Withdrawals
- Withdrawals reduce stake/total stake as before.
- Contract transfers NEO out starting with agents that have the lowest non-zero weight (tie-break by lowest agent index). If an agent balance is insufficient, continue to the next lowest non-zero weight agent.

### Rebalancing
- `RebalanceVotes()` is owner-only and uses the active agent weights to compute target balances:
  - `target[i] = totalAgentNEO * weight[i] / 21` (integer division).
  - Any remainder is allocated deterministically to the highest-weight agents (tie-break by index).
- Transfers move NEO from agents with excess to agents with deficits.
- Each agent is instructed to vote for its configured candidate.

## Error handling
- Config methods fail fast on invalid indices, missing data, or invalid totals.
- Deposit/withdraw/rebalance require an active config.

## Testing
- Config validation: weight sum, duplicate candidates, missing agent configs.
- Deposit routing: highest-weight agent receives NEO.
- Withdrawal routing: lowest non-zero weight agents are used first.
- Rebalancing: balances converge to targets and votes are applied.
- Existing stake/reward behavior remains intact.
