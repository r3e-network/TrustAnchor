# TrustAnchor on-chain voting design

## Context
TrustAnchor currently assumes an off-chain strategist (TEE) that computes vote targets and submits `TrigVote`/`TrigTransfer` calls. We want a simplified workflow where voting configuration is fully on-chain, with the Owner updating weights and triggering periodic rebalances. Deposits should immediately route to the highest-weight candidate, and withdrawals should drain from the lowest-weight candidates first.

## Goals
- Remove any dependency on TEE for voting decisions.
- Store candidate weights and agent assignments on-chain.
- Keep the agent model (21 agents) while simplifying control flow.
- Preserve existing stake ledger and reward distribution logic.

## Non-goals
- No on-chain governance voting for weight changes.
- No new NEP-17 token or transferability features.
- No dynamic agent discovery; all 21 agents are explicitly assigned.

## Architecture (on-chain only)
### Roles
- **Owner**: sole authority for config updates and rebalancing.
- **Strategist**: removed (or left unused).

### Configuration model
- The candidate weight list is the whitelist.
- Max 21 candidates and 21 agents.
- Weights are non-negative integers summing to 21.
- All 21 agents must be assigned to exactly one candidate.

### Staged config updates (multi-transaction safe)
To support updates over multiple transactions, config changes are staged:
- `BeginConfig()` creates/clears a pending config.
- `SetCandidate(i, pubkey, weight)` updates pending candidates/weights.
- `AssignAgent(agentIndex, candidateIndex)` updates pending agent mapping.
- `FinalizeConfig()` validates and atomically swaps pending → active.

Validation rules in `FinalizeConfig()`:
- Candidate count ≤ 21, weights sum to 21.
- No duplicate pubkeys.
- All 21 agents assigned exactly once to a valid candidate.
- At least one candidate with weight > 0.

## Deposit routing
When NEO is deposited to TrustAnchor:
- Determine the **highest-weight candidate** among weights > 0.
- If tie, use pubkey lexicographic order.
- Select the next agent for that candidate using a per-candidate round-robin cursor.
- Transfer the NEO to that agent (agent already mapped to that candidate).

## Withdrawal routing
When a user withdraws:
- Start with the **lowest non-zero weight** candidate.
- Withdraw from that candidate’s agents in round-robin order.
- If still short, move to the next lowest non-zero candidate, and continue.
- Preserve the existing “leave 1 NEO in agent” buffer (unless explicitly removed).

## Rebalancing
Owner calls `RebalanceVotes()` periodically:
- Compute total NEO across all agents.
- Target per candidate = `total * weight / 21`.
- Compute surplus/deficit per candidate from current agent balances.
- Transfer NEO from surplus agents to deficit agents (greedy, bounded loops).
- Ensure each agent votes for its assigned candidate.

## Storage layout (active and pending)
Active:
- `candidateCount`, `candidate[i]`, `weight[i]`
- `agentToCandidate[agentIndex]`
- `candidateAgentList[candidateIndex][]`
- `rrCursor[candidateIndex]`

Pending mirrors active layout plus `pendingActive` flag.

## Error handling
- Any invalid config -> `FinalizeConfig()` aborts.
- Deposit/withdraw abort if no weight > 0 (should not happen if sum == 21).
- Rebalance aborts if config missing or incomplete.

## Testing
- Config validation: sum == 21, unique pubkeys, full agent assignment.
- Deposit routing to highest-weight candidate (tie-break by pubkey).
- Withdraw routing from lowest non-zero weights, with spill to next lowest.
- Rebalance: transfers + votes match target allocations.

## Rollout
1. Deploy updated TrustAnchor contract.
2. Owner runs config script: `BeginConfig()`, `SetCandidate`, `AssignAgent`, `FinalizeConfig()`.
3. Owner triggers initial `RebalanceVotes()`.
