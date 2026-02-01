# TrustAnchor Contract Projects Design

**Goal:** Create separate Neo smart contract projects for TrustAnchor and TrustAnchorAgent, move sources into those projects, and update all tooling to the new paths.

## Decisions
- Create two Neo smart contract projects:
  - `contract/TrustAnchor/TrustAnchor.csproj`
  - `contract/TrustAnchorAgent/TrustAnchorAgent.csproj`
- Move sources into project folders (no linking from root).
- Update all tests, scripts, and deploy tooling to the new paths.

## File Layout
- Move TrustAnchor sources into `contract/TrustAnchor/`:
  - `TrustAnchor.cs`
  - `TrustAnchor.Constants.cs`
  - `TrustAnchor.Storage.cs`
  - `TrustAnchor.View.cs`
  - `TrustAnchor.Rewards.cs`
  - `TrustAnchor.Agents.cs`
- Move agent source into `contract/TrustAnchorAgent/`:
  - `TrustAnchorAgent.cs`
- Keep `contract/LICENSE` and `contract/README.md` in the root.

## Build and Tooling Updates
- **Contract tests**
  - Update `contract/TrustAnchor.Tests/TestContracts.cs` to compile sources from `contract/TrustAnchor/`.
  - Update `contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj` to include the moved files from the new folder.
- **Deployer**
  - Update `TrustAnchor/TrustAnchorDeployer/Program.cs` to resolve sources from `contract/TrustAnchor/` and `contract/TrustAnchorAgent/`.
- **Neo-express script**
  - Update `scripts/neo-express-test.sh` to copy/compile from `contract/TrustAnchor/` and `contract/TrustAnchorAgent/`.
- **Docs**
  - Update any README references to old paths, if present.

## Verification Plan
1. `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
2. `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj -v minimal`
3. `cd web && npm test`
4. `scripts/neo-express-test.sh` (manual check for deploy + register flow)

## Non-Goals
- No behavior changes to contract logic.
- No compatibility shims for old source paths.
