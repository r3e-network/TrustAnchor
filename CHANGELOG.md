# Changelog

All notable changes to the TrustAnchor project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Two-step owner transfer pattern (propose → accept → cancel) replacing immediate transfer
- Agent registry with uniqueness checks for name and target public key
- Manual vote methods (`VoteAgentById`, `VoteAgentByName`, `VoteAgentByTarget`)
- Voting setters by name and target (`SetAgentVotingByName`, `SetAgentVotingByTarget`)
- Agent info and list query methods (`AgentInfo`, `AgentCount`)
- Emergency withdraw for paused contract with empty agents
- Contract existence validation on agent registration (`ContractManagement.GetContract`)
- ConfigureAgent input validation
- Comprehensive test coverage for TrustAnchor contract
- Web dashboard with React + TypeScript + Tailwind CSS
- WalletConnect integration for NEO N3 wallet connectivity
- Admin panel for owner operations (agent management, pause/unpause, ownership transfer)
- Staking and reward claim UI
- CI/CD workflows for testnet and mainnet deployment
- CheckStake diagnostic tool with environment variable configuration
- `toContractParam` type-safe converter in `useWallet` hook

### Changed

- Deposit routing uses highest voting agent (priority-based)
- Withdrawal drains from lowest voting agent first (preserves priority)
- Batched React state updates in `useTrustAnchor` to reduce re-renders
- Moved contract sources into `contract/` project directory
- Updated deployer for contract project structure
- Aligned ops tools with manual voting flow
- Solution file now includes all 12 projects

### Fixed

- Pre-stake GAS reward distribution (deferred until first staker arrives)
- Emergency withdraw safety for partial agent registry
- Agent contract auth for core-only calls
- Contract hash computation using `Hash160`
- Deployer WIF parsing deferred to avoid startup crash
- CI/CD secret interpolation replaced with shell-based checks
- Unsafe `args as sc.ContractParam[]` cast replaced with proper conversion
- ESM-compatible test mocking (`vi.mock` with async `import()`)

### Security

- Two-step owner transfer prevents single-transaction takeover
- Agent registration requires deployed contract verification
- Voting amount of 0 documented as soft-deactivation mechanism
- Emergency withdraw gated by pause state and empty agent balances
- Mainnet deployer requires GitHub environment protection rules

### Removed

- Legacy external tooling references
- Legacy config and automatic rebalance flow
- Disabled `WithdrawGAS` stub
