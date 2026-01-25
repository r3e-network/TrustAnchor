# Simplified Voting Design

**Goal:** Replace the current weight/config-based voting with a simple, manual manager-driven registry of up to 21 agents. The manager registers agents with a target public key and name, can update them later, and controls a per-agent voting priority used only to route new deposits to a single highest-priority agent. No on-chain rebalance, no fixed 21 weight splits.

## Scope and Constraints
- Breaking change is allowed (new storage layout, redeploy/migrate).
- Manager can deploy any number of agent contracts up to 21.
- Agent registration requires: agent contract hash, target public key, and name.
- Target and name can be updated later by the manager.
- Manager manually sets a voting amount by agent id, name, or target public key.
- New staking deposits automatically route to the highest voting agent until the manager changes voting amounts.
- Manager can list agents with id, target public key, name, and voting amount.
- No automatic rebalance; no weighted distribution; all voting actions are manager-initiated.
- Agent names must be unique and limited to 32 bytes (ASCII recommended).

## High-Level Behavior
### Agent Registry
- `RegisterAgent(agentHash, targetPubKey, name)` stores agent data and assigns the next id.
- Names and target pubkeys are unique; registration/update fails on duplicates.
- Updates:
  - `UpdateAgentTargetById/Name/Target` changes target pubkey.
  - `UpdateAgentNameById` changes name.
  - `SetAgentVotingById/Name/Target` sets voting amount (priority).

### Voting Priority (Manual)
- `votingAmount` is a manager-specified priority, not a balance.
- Deposits always route to a single highest-priority agent.
- Tie-break: lowest agent id wins when voting amounts are equal (including zero).
- Manual vote calls:
  - `VoteAgentById/Name/Target` calls agent `vote` with its stored target.
  - `VoteAll` is a convenience loop; no automatic rebalance.

### Deposit Routing
- On NEO deposit, the contract selects the agent with the highest `votingAmount` and transfers the entire new deposit to that agent.
- If no agents exist, the deposit faults.
- Existing NEO remains in whatever agent currently holds it.

## Storage Layout (New)
- `agentCount: BigInteger`
- `agent[i] -> UInt160`
- `agentTarget[i] -> ECPoint`
- `agentName[i] -> string`
- `agentVoting[i] -> BigInteger`
- `nameToId[name] -> BigInteger`
- `targetToId[target] -> BigInteger`

## API Sketch (Owner-only unless noted)
- Views:
  - `Owner()`, `isPaused()`
  - `AgentCount()`, `AgentInfo(id)`
  - `AgentList()`
  - `AgentIndexByName(name)`, `AgentIndexByTarget(target)`
- Mutations:
  - `RegisterAgent(agentHash, targetPubKey, name)`
  - `UpdateAgentTargetById(id, target)`
  - `UpdateAgentTargetByName(name, target)`
  - `UpdateAgentTargetByTarget(oldTarget, newTarget)`
  - `UpdateAgentNameById(id, name)`
  - `SetAgentVotingById(id, amount)`
  - `SetAgentVotingByName(name, amount)`
  - `SetAgentVotingByTarget(target, amount)`
  - `VoteAgentById(id)`
  - `VoteAgentByName(name)`
  - `VoteAgentByTarget(target)`
  - `VoteAll()`

## Validation Rules
- `agentCount < 21`.
- `agentHash != UInt160.Zero`.
- `targetPubKey` must be 33 bytes.
- `name` non-empty, <= 32 bytes, unique.
- `target` unique (registration and updates).
- `votingAmount >= 0`.

## Testing Plan
- Register agent success/failure (duplicate name/target, max 21, bad target).
- Update target/name with uniqueness checks.
- Set voting by id/name/target.
- Deposit routing selects highest voting amount; tie-break by lowest id.
- Deposit with no agents faults.
- Manual vote methods call agent with stored target.

## Migration Notes
- Breaking change: deploy a new contract and re-register agents.
- Provide a one-time migration checklist in ops docs (register agents, set voting amounts, perform initial votes).
