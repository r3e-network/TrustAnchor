# ConfigureAgent Validation and Warning Cleanup Design

## Goal
Harden the ConfigureAgent CLI with explicit input validation and friendly errors, and clean existing compiler/test warnings without changing contract behavior.

## Scope
- Add a small internal input parser in `TrustAnchor/ConfigureAgent/Program.cs`.
- Add unit tests for parsing/validation in `TrustAnchor/TrustAnchorOps.Tests`.
- Fix current warnings in contract/tests with behavior-preserving changes only.

## Requirements
- Require `WIF`, `TRUSTANCHOR`, and `VOTE_TARGET` inputs.
- Validate vote target hex is exactly 33 bytes (66 hex characters) and valid hex.
- Reject negative `agentIndex` and negative `votingAmount` (zero allowed).
- Keep transaction behavior unchanged for valid inputs.
- Reduce warnings without changing contract behavior.

## Non-Goals
- No new on-chain features or API changes.
- No changes to voting logic beyond validation.

## Approach
1. **Input parsing helper**
   - Add an internal helper (record/tuple) that parses `args` + env vars and returns normalized values.
   - Validate required fields and format constraints with clear `InvalidOperationException` messages.
   - Expose to tests via `InternalsVisibleTo` in `ConfigureAgent.csproj`.

2. **Tests (TDD)**
   - New `ConfigureAgentInputTests` in `TrustAnchorOps.Tests`.
   - Cover missing inputs, invalid trust anchor hash, invalid hex content/length, negative index/amount, and a happy path.

3. **Warning cleanup (behavior-preserving)**
   - Replace null-unsafe casts in `contract/TrustAnchor.cs` with explicit null handling.
   - Initialize or mark nullable `AuthAgentHash` in tests.
   - Avoid null literal in `InvokeNeoPayment`.
   - Replace `Assert.Equal(1, list.Count)` with `Assert.Single(list)`.

## Testing
- `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
- `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
