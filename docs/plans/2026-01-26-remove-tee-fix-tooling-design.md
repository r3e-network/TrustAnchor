# Remove TEE + Align Tooling Design

**Goal:** Remove the legacy `TEE/` tooling entirely and make the remaining TrustAnchor tooling consistent, correct, and production-ready for the current on-chain contract APIs.

**Scope**
- Delete `TEE/` and scrub all references to it across docs/scripts.
- Fix TrustAnchor tooling to use current contract methods and correct N3 call semantics.
- Correct contract script-hash derivation in deployer tools.
- Add/adjust ops tests to validate input handling and API strings without RPC access.

**Out of Scope**
- On-chain contract logic changes (no new behavior).
- CI/CD pipeline changes beyond removing TEE references.

**Architecture Overview**
- Canonical workflow (supported):
  1) Deploy core (`TrustAnchor`) with owner hash injected.
  2) Deploy agents with core hash injected.
  3) Register each agent via `registerAgent(agentHash, targetPubKey, name)`.
  4) Set priority via `setAgentVotingById`, then update target/name via `updateAgentTargetById` and `updateAgentNameById`.
  5) Manual vote triggers via `voteAgentById`, `voteAgentByName`, `voteAgentByTarget`.
- Tooling uses `EmitDynamicCall` (preferred) or proper `System.Contract.Call` with `CallFlags` + args array.

**Key Fixes**
- Script hash derivation: use `Hash160(nef.Script)` rather than `SHA256(script)` truncation.
- Remove obsolete contract calls from tooling (`setAgent`, `beginConfig`, `setAgentConfig`, `finalizeConfig`).
- Normalize and validate inputs (hashes, ECPoints, integers) with clear errors before RPC/tx.

**Error Handling**
- Fail fast on missing/invalid env vars or args.
- Validate ECPoint targets as 33-byte compressed keys (hex length and parsing).
- Guard invalid agent indices and voting amounts.

**Testing**
- Extend `TrustAnchorOps.Tests` to cover:
  - Missing/invalid env vars for deploy/config/stake tools.
  - Method name constants for contract operations.
  - Script-hash derivation with a known NEF script payload.
- Keep tests RPC-free.

**Docs**
- Remove all TEE references.
- Document the TrustAnchor toolchain as the single supported path.

**Success Criteria**
- `TEE/` removed with no residual references.
- Tooling compiles, calls valid methods, and computes correct script hashes.
- Tests pass locally; no RPC required for tests.
