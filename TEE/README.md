# TEE
TrustAnchor trusted execution environment on github

## TrustAnchor configuration

The TEE tools now target the TrustAnchor core contract by default.

Environment variables:

- `TRUSTANCHOR`: TrustAnchor script hash (defaults to a placeholder hash for compatibility).

Voting configuration now happens on-chain:

- `beginConfig`
- `setAgentConfig`
- `finalizeConfig`
- `rebalanceVotes`
