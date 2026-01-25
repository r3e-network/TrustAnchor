#!/usr/bin/env bash
set -e

# TrustAnchor Testnet Deployment Script
# Run this on your local machine where you have access to testnet

echo "============================================"
echo "TrustAnchor Testnet Deployment"
echo "============================================"

# Configuration
WIF="KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb"
RPC="https://testnet1.neo.coz.io:443"
TRUSTANCHOR_OWNER=""

# Install Neo compiler if not installed
if ! command -v nccs &> /dev/null; then
    echo "Installing Neo Compiler..."
    dotnet tool install --global Neo.Compiler.CSharp --version 3.8.1
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Build the deployer
echo "Building deployer..."
cd /home/neo/git/bneo/TrustAnchor
dotnet build TrustAnchorDeployer/TrustAnchorDeployer.csproj -c Release

# Run deployment
echo "Deploying to testnet..."
export WIF
export RPC
export OWNER_HASH="$TRUSTANCHOR_OWNER"
dotnet run --project TrustAnchorDeployer --configuration Release

echo ""
echo "============================================"
echo "After deployment, configure Agent 1:"
echo "============================================"
echo ""
echo "Configure Agent 1 with vote target:"
echo "  cd /home/neo/git/bneo/TrustAnchor"
echo "  dotnet run --project ConfigureAgent -- <WIF> <TRUSTANCHOR_HASH> 1 <VOTE_TARGET> 21"
echo ""
echo "Example:"
echo "  dotnet run --project ConfigureAgent -- KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb 0xcf04873522e5d75e76cb7f14f801cefe75d710b3 1 036d13bdb7da325738167f2adde14e424e8cbfebc5501437a22f4e7668a3116168 21"
