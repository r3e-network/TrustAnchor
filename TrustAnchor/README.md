# TrustAnchor Operations

Operational tools for managing TrustAnchor on NEO blockchain via GitHub Actions.

## Overview

The TrustAnchor tools automate operational tasks for TrustAnchor:

- **GAS Claiming** - Automatically claim GAS rewards from Agent contracts
- **GAS Distribution** - Distribute collected GAS to contributors
- **Key Generation** - Generate secure NEO wallets

## Projects

### Core Tools

| Project                       | Purpose                                      | Schedule      |
| ----------------------------- | -------------------------------------------- | ------------- |
| **TrustAnchorDeployer**       | Deploy TrustAnchor and 21 Agent contracts   | Manual        |
| **TrustAnchorClaimer**        | Claims unclaimed GAS from 21 Agent contracts | Every 6 hours |
| **TrustAnchorRepresentative** | Distributes GAS to contributors              | Every 6 hours |
| **KeyGenerator**              | Generates NEO keypairs                       | Manual        |

### Libraries

| Library       | Purpose                                   |
| ------------- | ----------------------------------------- |
| **LibHelper** | Utility methods and extensions            |
| **LibRPC**    | NEO RPC client wrapper                    |
| **LibWallet** | Wallet operations and transaction signing |

## Deployment Tool

### TrustAnchorDeployer

One-time deployment tool for:

1. **Deploy TrustAnchor Contract** - Compiles and deploys the main staking contract
2. **Deploy 21 Agent Contracts** - Creates all required agent contracts
3. **Register Agents** - Configures agent addresses in TrustAnchor
4. **Initial Configuration** - Sets up equal weight distribution

**Usage:**
```bash
cd TrustAnchor
dotnet run --project TrustAnchorDeployer
```

**Environment Variables:**
- `WIF` - Deployer wallet private key
- `RPC` - NEO RPC endpoint (testnet/mainnet)
- `OWNER_HASH` - Owner address script hash
- `CONTRACTS_DIR` - Path to contract source files (default: contract/)
- `NCCS_PATH` - Optional override for the `nccs` compiler path

**Workflow:** `.github/workflows/TrustAnchorDeployer.yml`

## Configuration

### Required Environment Variables

| Variable         | Used By                 | Description                            |
| ---------------- | ----------------------- | -------------------------------------- |
| `TRUSTANCHOR`    | Claimer, Representative | TrustAnchor contract script hash       |
| `WIF`            | All                     | Wallet private key (WIF format)        |
| `RPC`            | All                     | NEO RPC endpoint URL                   |
| `THRESHOLD`     | Claimer, Representative | Minimum GAS amount to trigger action   |
| `MOD`            | Claimer                 | Modulo for agent rotation (default: 1) |
| `REPRESENTATIVE` | Representative          | Representative address holding GAS     |
| `TARGET`         | Representative          | Target address to receive GAS          |

### Example Configuration

```yaml
env:
  TRUSTANCHOR: "0x1234...abcd" # Your TrustAnchor contract hash
  WIF: ${{ secrets.TEEWIF }} # Encrypted wallet WIF
  RPC: "https://n3seed2.ngd.network:10332"
  THRESHOLD: "17179869184" # ~17 GAS
  REPRESENTATIVE: "0xabcd...1234"
  TARGET: "0x5678...efgh"
```

## GitHub Actions Workflows

### TrustAnchorClaimer

Runs every 6 hours to:

1. Check Agent contracts for unclaimed GAS
2. Call `sync()` on agents with significant unclaimed GAS
3. Call `claim()` to collect GAS to Representative

**Workflow**: `.github/workflows/TrustAnchorClaimer.yml`

### TrustAnchorRepresentative

Runs every 6 hours to:

1. Check Representative GAS balance
2. If above threshold, transfer GAS to TARGET
3. Reserve ~1 GAS for transaction fees

**Workflow**: `.github/workflows/TrustAnchorRepresentative.yml`

### GenerateKey

Manual workflow to:

1. Generate new NEO keypair
2. Encrypt WIF and store as GitHub secret
3. Output public address

**Workflow**: `.github/workflows/GenerateKey.yml`

## Building Locally

```bash
# Build all projects
cd TrustAnchor
dotnet restore TrustAnchor.sln
dotnet build TrustAnchor.sln

# Run specific tool
export TRUSTANCHOR="0x..."
export WIF="..."
export RPC="https://..."
export THRESHOLD="17179869184"
dotnet run --project TrustAnchorClaimer
```

## Target Framework

- **.NET 10.0** - All TrustAnchor projects use .NET 10

## Security Notes

1. **Private Keys** - Never commit WIF keys to repository
2. **Secrets** - Use GitHub Encrypted Secrets for sensitive data
3. **Access Control** - Limit who can trigger workflows
4. **Threshold** - Set appropriate thresholds to avoid dust transactions

## Contract Integration

TrustAnchor tools integrate with TrustAnchor contract methods:

```csharp
// Agent contract methods
agent.sync()     // Trigger NEO.Vote() to claim GAS
agent.claim()    // Transfer all GAS to caller

// TrustAnchor contract methods
trustAnchor.agent(index)  // Get agent address by index
```

## Troubleshooting

### "nccs tool not found"

- This error occurs in contract tests, not TrustAnchor tools
- TrustAnchor tools don't require Neo compiler

### "REPRESENTATIVE env var is required"

- Add REPRESENTATIVE to workflow secrets or environment

### Low GAS balance

- Adjust THRESHOLD based on your network's GAS prices
- Reserve at least 1 GAS for transaction fees

## License

Same as parent TrustAnchor project.
