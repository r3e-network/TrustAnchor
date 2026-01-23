# TEE
neoburger trusted execution environment on github

## TrustAnchor configuration

The TEE tools now target the TrustAnchor core contract by default.

Environment variables:

- `TRUSTANCHOR`: TrustAnchor script hash (defaults to the legacy BurgerNEO hash for compatibility).
- `VOTE_CONFIG`: Path to the vote config JSON used by `BurgerStrategist`.

Vote config rules:

- The config must include exactly one entry per agent.
- The sum of `weight` values must be `21`.
- All configured pubkeys must be whitelisted on-chain.

Example config: `TEE/BurgerStrategist/vote-config.example.json`

Suggested workflow: update the config in GitHub and restart the TEE process so the new weights take effect.
