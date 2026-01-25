# TrustAnchor

**Non-Profit** decentralized NEO voting delegation system. We distribute 100% of GAS rewards to stakers based on their staked amount—no fees, no profit.

## Mission

TrustAnchor exists solely to amplify the voices of **active contributors** and **reputable community members** in the NEO ecosystem. We do not charge any fees. All GAS rewards earned from voting are distributed back to users proportionally to their staked NEO.

## Overview

TrustAnchor enables NEO token holders to delegate their voting power to agents committed to supporting ecosystem developers, researchers, and community members with proven track records of integrity and technical excellence.

### Non-Profit Commitment

- **0% Fees** - We charge nothing. All rewards go to stakers.
- **100% Distribution** - GAS rewards are distributed based on staked NEO amount.
- **Transparent** - All operations and rewards are on-chain and verifiable.
- **Community-First** - Built for the ecosystem, not for profit.

## Quick Start

```bash
# Clone the repository
git clone https://github.com/r3e-network/TrustAnchor.git
cd TrustAnchor

# Build contracts
cd contract
dotnet build TrustAnchor.Tests/TrustAnchor.Tests.csproj

# Run tests
dotnet test TrustAnchor.Tests/TrustAnchor.Tests.csproj
```

## Project Structure

```
TrustAnchor/
├── contract/                       # Smart contract source code
│   ├── TrustAnchor.cs             # Main staking and delegation contract
│   ├── TrustAnchorAgent.cs        # Agent contract for voting
│   ├── TrustAnchor.Tests/         # Test suite
│   └── README.md                  # Contract documentation
├── TrustAnchor/                   # Operational automation tools
│   ├── TrustAnchorClaimer         # Automated GAS claiming
│   ├── TrustAnchorRepresentative  # Automated GAS distribution
│   └── README.md                  # Ops documentation
├── TEE/                           # Legacy ops tools (kept for compatibility)
└── .github/workflows/              # CI/CD workflows
```

## Key Features

- **Non-Custodial Staking**: Users always maintain control of their assets
- **Zero Fees**: 100% of GAS rewards distributed to stakers
- **Proportional Rewards**: GAS distributed based on staked NEO amount
- **Flexible Delegation**: Choose agents based on their voting targets
- **Transparent Voting**: All agent votes are publicly visible on-chain
- **Secure Config**: Time-locked owner transfers and pause mechanisms
- **Emergency Control**: Owner GAS withdrawal is only permitted while paused and is not part of normal rewards

## How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                         NEO Holders                               │
│  Deposit NEO → Earn Voting Power + Share of GAS Rewards         │
│  (No fees charged - 100% of rewards returned to stakers)         │
└────────────────────────┬────────────────────────────────────────┘
                         │ Delegate
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                      TrustAnchor Contract                        │
│  - Accepts NEO deposits                                         │
│  - Tracks staked amount per user                                │
│  - Routes NEO to 21 Agents by weight                            │
│  - Calculates GAS reward share per user                         │
└────────────────────────┬────────────────────────────────────────┘
                         │ Distribute by Weight
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     TrustAnchorAgent[0-20]                       │
│  - Holds NEO from TrustAnchor                                    │
│  - Votes for active contributors                                │
│  - Claims GAS rewards (from NEO voting)                         │
└─────────────────────────────────────────────────────────────────┘
                         │ Returns GAS
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Stakers Receive GAS                           │
│  - Distributed proportionally to staked NEO                     │
│  - No platform fees taken                                       │
└─────────────────────────────────────────────────────────────────┘
```

## Reward Distribution

GAS rewards from NEO voting are distributed to stakers **proportionally to their staked NEO amount**:

```
Your GAS Share = (Your Staked NEO / Total Staked NEO) × Total GAS Rewards
```

**Example:**

- Total staked: 1,000,000 NEO
- Your stake: 10,000 NEO (1%)
- Total GAS earned: 100 GAS
- **Your reward: 1 GAS (1% - exactly your share)**

**No fees, no deductions.**

## Core Philosophy

Unlike traditional voting systems that concentrate power based on wealth or extract profits, TrustAnchor enables NEO token holders to delegate their voting power to agents who commit to:

- **Support Active Contributors** - Vote for developers, researchers, and community members actively contributing to the NEO ecosystem
- **Reward Good Reputation** - Prioritize candidates with proven track records of integrity and technical excellence
- **Long-term Vision** - Make decisions benefiting ecosystem sustainability over short-term gains
- **Non-Profit Operation** - All rewards returned to stakers, zero fees

## Development

### Prerequisites

- .NET 9.0 SDK (contracts/tests)
- .NET 10.0 SDK (ops tools)
- Neo.Compiler.CSharp 3.8.1+

### Building Contracts

The project uses a custom compilation process via the NEO compiler. See [contract/README.md](contract/README.md) for detailed contract documentation.

### Running Tests

```bash
cd contract
dotnet test TrustAnchor.Tests/TrustAnchor.Tests.csproj
```

## Operations

The [TrustAnchor/](TrustAnchor/) folder contains automated tools for operational tasks:

- **TrustAnchorClaimer** - Automatically claims GAS from Agent contracts
- **TrustAnchorRepresentative** - Distributes GAS back to stakers
- **KeyGenerator** - Generates secure NEO wallets

See [TrustAnchor/README.md](TrustAnchor/README.md) for details.

The [TEE/](TEE/) folder is legacy tooling retained for compatibility.

## License

[Add your license here]

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Links

- [NEO Blockchain](https://neo.org/)
- [NEO Developer Documentation](https://docs.neo.org/)
- [GitHub Repository](https://github.com/r3e-network/TrustAnchor)
