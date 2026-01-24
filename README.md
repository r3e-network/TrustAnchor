# TrustAnchor

Decentralized NEO voting delegation system amplifying voices of active contributors and reputable community members.

## Overview

TrustAnchor enables NEO token holders to delegate their voting power to agents committed to supporting ecosystem developers, researchers, and community members with proven track records of integrity and technical excellence—rather than prioritizing profit motives.

## Quick Start

```bash
# Clone the repository
git clone https://github.com/r3e-network/TrustAnchor.git
cd TrustAnchor

# Build contracts
cd code
dotnet build TrustAnchor.Tests/TrustAnchor.Tests.csproj

# Run tests
dotnet test TrustAnchor.Tests/TrustAnchor.Tests.csproj
```

## Project Structure

```
TrustAnchor/
├── code/                           # Smart contract source code
│   ├── TrustAnchor.cs             # Main staking and delegation contract
│   ├── TrustAnchorAgent.cs        # Agent contract for voting
│   ├── TrustAnchor.Tests/         # Test suite
│   └── README.md                  # Contract documentation
├── .github/workflows/              # CI/CD workflows
└── TEE/                           # Trusted Execution Environment scripts
```

## Key Features

- **Non-Custodial Staking**: Users always maintain control of their assets
- **Flexible Delegation**: Choose agents based on their voting targets and philosophy
- **GAS Rewards**: Earn a share of GAS distributed to the contract
- **Transparent Voting**: All agent votes are publicly visible on-chain
- **Secure Config**: Time-locked owner transfers and pause mechanisms for safety

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
│                     TrustAnchorAgent[0-20]                       │
│  - Holds NEO from TrustAnchor                                    │
│  - Votes on behalf of delegators                                 │
│  - Target: Active Contributors & Reputable Candidates            │
└─────────────────────────────────────────────────────────────────┘
```

## Core Philosophy

Unlike traditional voting systems that often concentrate power based on wealth, TrustAnchor enables NEO token holders to delegate their voting power to agents who commit to:

- **Support Active Contributors** - Vote for developers, researchers, and community members who actively contribute to the NEO ecosystem
- **Reward Good Reputation** - Prioritize candidates with proven track records of integrity and technical excellence
- **Long-term Vision** - Make decisions that benefit the ecosystem's sustainability over short-term gains

## Development

### Prerequisites

- .NET 9.0 SDK
- Neo.Compiler.CSharp 3.8.1+

### Building Contracts

The project uses a custom compilation process via the NEO compiler. See [code/README.md](code/README.md) for detailed contract documentation.

### Running Tests

```bash
cd code
dotnet test TrustAnchor.Tests/TrustAnchor.Tests.csproj
```

## License

[Add your license here]

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Links

- [NEO Blockchain](https://neo.org/)
- [NEO Developer Documentation](https://docs.neo.org/)
