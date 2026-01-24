# TrustAnchor Smart Contracts

## Overview

TrustAnchor is a decentralized voting delegation system built on the NEO blockchain. Our mission is to amplify the voices of **active contributors** and **reputable community members** in the NEO ecosystem, rather than prioritizing profit motives.

### Core Philosophy

Unlike traditional voting systems that often concentrate power based on wealth, TrustAnchor enables NEO token holders to delegate their voting power to agents who commit to:

- **Support Active Contributors** - Vote for developers, researchers, and community members who actively contribute to the NEO ecosystem
- **Reward Good Reputation** - Prioritize candidates with proven track records of integrity and technical excellence
- **Long-term Vision** - Make decisions that benefit the ecosystem's sustainability over short-term gains

## How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                         NEO Holders                               │
│  (Deposit NEO → Earn Voting Power + GAS Rewards)                │
└────────────────────────┬────────────────────────────────────────┘
                         │ Delegate
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                      TrustAnchor Contract                        │
│  - Accepts NEO deposits                                         │
│  - Tracks voting power (stake)                                  │
│  - Distributes GAS rewards                                      │
│  - Routes NEO to 21 Agents based on voting weights              │
└────────────────────────┬────────────────────────────────────────┘
                         │ Distribute by Weight
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     TrustAnchorAgent[0-20]                        │
│  - Holds NEO from TrustAnchor                                    │
│  - Votes on behalf of delegators                                 │
│  - Target: Active Contributors & Reputable Candidates            │
└─────────────────────────────────────────────────────────────────┘
```

## Key Features

- **Non-Custodial Staking**: Users always maintain control of their assets
- **Flexible Delegation**: Choose agents based on their voting targets and philosophy
- **GAS Rewards**: Earn a share of GAS distributed to the contract
- **Transparent Voting**: All agent votes are publicly visible on-chain
- **Secure Config**: Time-locked owner transfers and pause mechanisms for safety

## notice

TrustAnchor is derived from an earlier NEO3-era contract design.

The compiler and toolkits at that time were not developed well and were not compatible with the latest version.

If you want to know how the contract code is compiled, view [Github Workflow YML file](.github/workflows/dotnet.yml).

Due to the limitations of the toolkits at that time, this code used some tricky skills, so it is **NOT** recommended that the new version of the compiler restore its compatibility.

In order to keep consistency with the running smart contract code on the mainnet, the code will not be switched to the new version of the compiler for the time being.

## Registry

| contract name      | script hash | address |
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
| TrustAnchorAgent21 | `TBD`       | `TBD`   |
