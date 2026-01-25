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
┌───────���─────────────────────────────────────────────────────────┐
│                         NEO Holders                               │
│  Deposit NEO → Earn Voting Power + Share of GAS (No Fees)        │
└────────────────────────┬────────────────────────────────────────┘
                         │ Delegate
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                      TrustAnchor Contract                        │
│  - Accepts NEO deposits                                         │
│  - Tracks staked amount per user                                │
│  - Routes NEO to 21 Agents by weight                            │
│  - Distributes 100% of GAS to stakers                           │
└────────────────────────┬────────────────────────────────────────┘
                         │ Distribute by Weight
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
- **Flexible Delegation**: Choose agents based on their voting targets
- **Transparent Voting**: All agent votes are publicly visible on-chain
- **Secure Config**: Time-locked owner transfers and pause mechanisms

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

**Emergency Operations:** `WithdrawGAS` is only permitted when the contract is paused and is intended for emergency use, not normal reward distribution.

## Registry

| Contract Name      | Script Hash | Address |
| ------------------ | ----------- | ------- |
| TrustAnchor        | `TBD`       | `TBD`   |
| TrustAnchorAgent0  | `TBD`       | `TBD`   |
| TrustAnchorAgent1  | `TBD`       | `TBD`   |
| TrustAnchorAgent2  | `TBD`       | `TBD`   |
| TrustAnchorAgent3  | `TBD`       | `TBD`   |
| TrustAnchorAgent4  | `TBD`       | `TBD`   |
| TrustAnchorAgent5  | `TBD`       | `TBD`   |
| TrustAnchorAgent6  | `TBD`       | `TBD`   |
| TrustAnchorAgent7  | `TBD`       | `TBD`   |
| TrustAnchorAgent8  | `TBD`       | `TBD`   |
| TrustAnchorAgent9  | `TBD`       | `TBD`   |
| TrustAnchorAgent10 | `TBD`       | `TBD`   |
| TrustAnchorAgent11 | `TBD`       | `TBD`   |
| TrustAnchorAgent12 | `TBD`       | `TBD`   |
| TrustAnchorAgent13 | `TBD`       | `TBD`   |
| TrustAnchorAgent14 | `TBD`       | `TBD`   |
| TrustAnchorAgent15 | `TBD`       | `TBD`   |
| TrustAnchorAgent16 | `TBD`       | `TBD`   |
| TrustAnchorAgent17 | `TBD`       | `TBD`   |
| TrustAnchorAgent18 | `TBD`       | `TBD`   |
| TrustAnchorAgent19 | `TBD`       | `TBD`   |
| TrustAnchorAgent20 | `TBD`       | `TBD`   |
