#!/usr/bin/env bash
set -euo pipefail

# TrustAnchor Testnet Deployment Script
# Run this on your local machine where you have access to testnet

echo "============================================"
echo "TrustAnchor Testnet Deployment"
echo "============================================"

# Configuration
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OPS_DIR="$ROOT_DIR/TrustAnchor"
: "${WIF:?WIF env var is required}"
RPC="${RPC:-https://testnet1.neo.coz.io:443}"
OWNER_HASH="${OWNER_HASH:-}"

# Install Neo compiler if not installed
if ! command -v nccs &> /dev/null; then
    echo "Installing Neo Compiler..."
    dotnet tool install --global Neo.Compiler.CSharp --version 3.8.1
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Build the deployer
echo "Building deployer..."
cd "$OPS_DIR"
dotnet build TrustAnchorDeployer/TrustAnchorDeployer.csproj -c Release

# Run deployment
echo "Deploying to testnet..."
export WIF
export RPC
export OWNER_HASH
dotnet run --project TrustAnchorDeployer --configuration Release

echo ""
echo "============================================"
echo "After deployment, configure Agent 1:"
echo "============================================"
echo ""
echo "Configure Agent 1 with vote target:"
echo "  cd \"$OPS_DIR\""
echo "  dotnet run --project ConfigureAgent -- <WIF> <TRUSTANCHOR_HASH> 1 <VOTE_TARGET> 21"
echo ""
echo "Example:"
echo "  dotnet run --project ConfigureAgent -- <WIF> <TRUSTANCHOR_HASH> 1 <VOTE_TARGET> 21"
