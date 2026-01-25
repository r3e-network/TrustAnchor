#!/usr/bin/env bash
set -euo pipefail

# TrustAnchor Testnet Deployment and Testing Script
# This script deploys TrustAnchor contracts to NEO testnet and tests functionality

echo "=========================================="
echo "TrustAnchor Testnet Deployment & Testing"
echo "=========================================="

# Export environment variables
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OPS_DIR="$ROOT_DIR/TrustAnchor"
: "${DEPLOYER_WIF:?DEPLOYER_WIF env var is required}"
RPC_URL="${RPC_URL:-https://n3seed2.ngd.network:10332}"
TESTNET_RPC="${TESTNET_RPC:-$RPC_URL}"

# Create environment file
cat > "$OPS_DIR/.env.testnet" <<EOF
WIF=$DEPLOYER_WIF
RPC=$TESTNET_RPC
OWNER_HASH=
CONTRACTS_DIR=$ROOT_DIR/contract
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
cd "$OPS_DIR"
dotnet build TrustAnchorDeployer/TrustAnchorDeployer.csproj -c Release

echo ""
echo "=========================================="
echo "Deployment ready!"
echo "=========================================="
echo ""
echo "To deploy, run:"
echo "  cd \"$OPS_DIR\""
echo "  WIF=\$DEPLOYER_WIF RPC=\$TESTNET_RPC dotnet run --project TrustAnchorDeployer"
echo ""
echo "After deployment, note the contract hashes and update .env.testnet"
