#!/usr/bin/env bash
set -e

# TrustAnchor Testnet Deployment and Testing Script
# This script deploys TrustAnchor contracts to NEO testnet and tests functionality

echo "=========================================="
echo "TrustAnchor Testnet Deployment & Testing"
echo "=========================================="

# Export environment variables
export DEPLOYER_WIF="KzjaqMvqzF1uup6KrTKRxTgjcXE7PbKLRH84e6ckyXDt3fu7afUb"
export STAKER_WIF="Kz5f7PbBh3uxx2DDrQyrCki4xxXnNhJ9YLR7eEcKbJGzQbgStahn"
export RPC_URL="https://n3seed2.ngd.network:10332"
export TESTNET_RPC="https://n3seed2.ngd.network:10332"

# Create environment file
cat > /home/neo/git/bneo/TrustAnchor/.env.testnet <<EOF
WIF=$DEPLOYER_WIF
RPC=$TESTNET_RPC
OWNER_HASH=
SCRIPTS_DIR=../contract
TRUSTANCHOR=
THRESHOLD=100000000
MOD=1
REPRESENTATIVE=
TARGET=
EOF

echo "Environment configured for testnet deployment"
echo "RPC: $TESTNET_RPC"
echo ""
echo "WARNING: This will deploy contracts to NEO testnet"
echo "Press Ctrl+C within 5 seconds to cancel..."
sleep 5

# Build the deployer
echo ""
echo "=========================================="
echo "Building TrustAnchorDeployer..."
echo "=========================================="
cd /home/neo/git/bneo/TrustAnchor
dotnet build TrustAnchorDeployer/TrustAnchorDeployer.csproj -c Release

echo ""
echo "=========================================="
echo "Deployment ready!"
echo "=========================================="
echo ""
echo "To deploy, run:"
echo "  cd /home/neo/git/bneo/TrustAnchor"
echo "  WIF=\$DEPLOYER_WIF RPC=\$TESTNET_RPC dotnet run --project TrustAnchorDeployer"
echo ""
echo "After deployment, note the contract hashes and update .env.testnet"
