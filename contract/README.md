# TrustAnchor Smart Contracts

## Overview

TrustAnchor is a **non-profit** decentralized voting delegation system built on the NEO blockchain. Our mission is to amplify the voices of **active contributors** and **reputable community members** in the NEO ecosystem.

### Non-Profit Commitment

- **0% Platform Fees** - All GAS rewards are distributed to stakers
- **Proportional Distribution** - Rewards based on staked NEO amount
- **Transparent** - All operations on-chain and verifiable

### Core Philosophy

Unlike traditional voting systems that concentrate power based on wealth or extract profits, TrustAnchor enables NEO token holders to delegate their voting power to agents who commit to:

- **Support Active Contributors** - Vote for developers, researchers, and community members actively contributing to the NEO ecosystem
- **Reward Good Reputation** - Prioritize candidates with proven track records of integrity and technical excellence
- **Long-term Vision** - Make decisions benefiting ecosystem sustainability over short-term gains

## How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                         NEO Holders                               │
│  Deposit NEO → Earn Voting Power + Share of GAS (No Fees)        │
└────────────────────────┬────────────────────────────────────────┘
                         │ Delegate
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                      TrustAnchor Contract                        │
│  - Accepts NEO deposits                                         │
│  - Tracks staked amount per user                                │
│  - Routes NEO to highest voting agent                           │
│  - Distributes 100% of GAS to stakers                           │
└────────────────────────┬────────────────────────────────────────┘
                         │ Manual voting target
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     TrustAnchorAgent[0-20]                        │
│  - Holds NEO from TrustAnchor                                    │
│  - Votes on behalf of delegators                                 │
│  - Claims GAS from NEO voting                                   │
│  - Returns GAS to contract                                       │
└─────────────────────────────────────────────────────────────────┘
```

## Key Features

- **Non-Custodial Staking**: Users always maintain control of their assets
- **Zero Fees**: 100% of GAS rewards distributed to stakers
- **Proportional Rewards**: GAS distributed based on staked NEO amount
- **Manual Voting Control**: Manager sets agent targets, names, and voting priority
- **Transparent Voting**: All agent votes are publicly visible on-chain
- **Secure Admin**: Time-locked owner transfers and pause mechanisms

## Agent Management

- **Max 21 Agents**: Manager can register up to 21 agent contracts
- **Updatable Metadata**: Agent target and name can be updated later
- **Manual Priority**: Voting amount is only used to route new deposits (no on-chain rebalance)

## Reward Distribution

GAS rewards from NEO voting are distributed to stakers **proportionally to their staked NEO amount**:

```
Your GAS Share = (Your Staked NEO / Total Staked NEO) × Total GAS Rewards
```

**Example:**

- Total staked: 1,000,000 NEO
- Your stake: 10,000 NEO (1%)
- Total GAS earned: 100 GAS
- **Your reward: 1 GAS (exactly 1%)**

**No platform fees taken.**

## Notice

TrustAnchor is derived from an earlier NEO3-era contract design.

The compiler and toolkits at that time were not well developed and were not compatible with the latest version.

If you want to know how the contract code is compiled, view the [GitHub Workflow YAML file](../.github/workflows/dotnet.yml).

Due to the limitations of the toolkits at that time, this code used some tricky techniques, so it is **NOT** recommended to restore compatibility with newer compiler versions.

In order to keep consistency with the smart contract code running on mainnet, the code will not be switched to a new compiler version for the time being.

**Emergency Operations:** The `WithdrawGAS` method has been removed to ensure all rewards remain with stakers.

**Reward Handling:** GAS received before any staking begins is held and distributed once the first stake is created.

## Registry

Populate after deployment; values below are intentionally left blank until recorded.

| Contract Name      | Script Hash | Address |
| ------------------ | ----------- | ------- |
| TrustAnchor        | `-`         | `-`     |
| TrustAnchorAgent0  | `-`         | `-`     |
| TrustAnchorAgent1  | `-`         | `-`     |
| TrustAnchorAgent2  | `-`         | `-`     |
| TrustAnchorAgent3  | `-`         | `-`     |
| TrustAnchorAgent4  | `-`         | `-`     |
| TrustAnchorAgent5  | `-`         | `-`     |
| TrustAnchorAgent6  | `-`         | `-`     |
| TrustAnchorAgent7  | `-`         | `-`     |
| TrustAnchorAgent8  | `-`         | `-`     |
| TrustAnchorAgent9  | `-`         | `-`     |
| TrustAnchorAgent10 | `-`         | `-`     |
| TrustAnchorAgent11 | `-`         | `-`     |
| TrustAnchorAgent12 | `-`         | `-`     |
| TrustAnchorAgent13 | `-`         | `-`     |
| TrustAnchorAgent14 | `-`         | `-`     |
| TrustAnchorAgent15 | `-`         | `-`     |
| TrustAnchorAgent16 | `-`         | `-`     |
| TrustAnchorAgent17 | `-`         | `-`     |
| TrustAnchorAgent18 | `-`         | `-`     |
| TrustAnchorAgent19 | `-`         | `-`     |
| TrustAnchorAgent20 | `-`         | `-`     |
