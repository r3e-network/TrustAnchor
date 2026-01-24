> Deprecated: superseded by `docs/plans/2026-01-24-trustanchor-onchain-voting-design.md` and `docs/plans/2026-01-24-trustanchor-onchain-voting-implementation.md`.

# TrustAnchor Edge Tests Design

## Goal
Add edge-case and authorization tests around deposits, withdrawals, and strategist-gated actions without changing production behavior unless a test reveals a defect.

## Scope
This design focuses only on test coverage within `code/TrustAnchor.Tests`. It expands the existing `TrustAnchorFixture` harness to invoke methods with explicit signers, fault on invalid behavior, and validate state and call-side effects. The contract code stays unchanged unless a test demonstrates incorrect behavior that requires a fix.

## Approach
Leverage the current in-process compiler flow for TrustAnchor and the embedded TestAgent. Tests will use the same `TestEngine` and explicit signers to ensure witness checks are exercised. Edge cases to cover:
- Withdraw more than staked should fault.
- Withdraw with zero amount should fault.
- Deposit with zero amount should not change stake or total stake.
- `trigTransfer` should fault for non-strategist.
- `trigVote` should fault when the candidate is not whitelisted, and should fault again after revocation.

For fault assertions, tests will invoke `CallFrom` and assert a `TestException` (or an exception derived from a faulted execution). Where needed, the fixture will add minimal helpers (for example, `DisallowCandidate`, `SetStrategist`, and `CallFrom` variants). State checks will continue to use contract getters and the TestAgent’s stored fields for verifying calls.

## Testing Strategy
Each case follows strict TDD: write a single failing test, run it to confirm the expected failure mode, then implement the minimal fixture helper or test adjustment required to pass. After each test is green, move to the next case. Run the full test suite and the existing `scripts/neo-express-test.sh` after all additions to ensure no regressions.

## Risks
The TestEngine behavior may differ from chain execution for candidate vote operations. If `NEO.Vote` faults in the test agent, the test agent will record the vote without calling `NEO.Vote` to isolate TrustAnchor’s gating logic.
