# TrustAnchor Ops Production-Ready Design

**Goal:** Make TrustAnchor operational tooling production-ready by removing embedded secrets, aligning workflows and scripts with repo layout, hardening input validation, and adding basic tests without requiring RPC access.

## Context

The repository contains TrustAnchor smart contracts under `contract/` and operational tools under `TrustAnchor/`. Existing workflows live under `TrustAnchor/.github/workflows` and include hardcoded credentials, incorrect paths, and configuration drift. Several tools log or embed WIF private keys, and one staking helper has a method name typo.

## Architecture

- **Ops tools:** `TrustAnchorDeployer`, `TrustAnchorClaimer`, `TrustAnchorRepresentative`, `StakeNEO`, and helper libraries under `TrustAnchor/`.
- **Workflows:** Move to repo-root `.github/workflows` so Actions execute for this repo; update to reference `TrustAnchor/<Project>` paths.
- **Scripts:** `deploy-testnet*.sh` and `testnet-workflow.sh` become portable, env-driven, and free of real keys.
- **Tests:** Add a small `TrustAnchor/TrustAnchorOps.Tests` xUnit project to validate env parsing and method names without RPC calls.

## Data Flow

1. **Workflows** load secrets into environment variables (WIF, RPC, TRUSTANCHOR, OWNER_HASH, THRESHOLD, REPRESENTATIVE, TARGET).
2. **Ops tools** validate required inputs, parse values, and create scripts/transactions.
3. **RPC submission** occurs only in tools when inputs are valid; tests never touch the network.
4. **Logs** include only public identifiers (script hashes, tx hashes), not secrets.

## Error Handling and Security

- Fail fast with clear errors when required env/args are missing or malformed.
- No hardcoded WIFs, no WIF logging, no unsafe string substring on null values.
- Avoid static initialization that throws before `Main`.
- Replace absolute `/home/neo` paths with repo-relative paths.
- Scripts use placeholders and require explicit env configuration.

## Testing and Verification

- **Unit tests** cover env parsing, method name correctness (`stakeOf`), and safe handling of missing WIF.
- **Contract tests** remain in `contract/TrustAnchor.Tests`.
- **Verification commands:**
  - `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
  - `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
  - `dotnet build TrustAnchor/TrustAnchor.sln`

## Rollout Plan

1. Move workflows to repo root and align paths/secrets.
2. Remove embedded secrets and update scripts/docs.
3. Add tests and fix stake method typo and validation.
4. Run verification commands and re-scan for secrets.

## Non-Goals

- No on-chain contract logic changes.
- No RPC-dependent tests.
- No redesign of staking economics.
