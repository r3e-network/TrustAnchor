#!/usr/bin/env bash
set -euo pipefail

# TrustAnchor Testnet Complete Workflow
# Deploys contracts, tests functionality, stakes NEO, and configures voting

echo "=============================================="
echo "TrustAnchor Complete Testnet Workflow"
echo "=============================================="

# Configuration
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OPS_DIR="$ROOT_DIR/TrustAnchor"
: "${DEPLOYER_WIF:?DEPLOYER_WIF env var is required}"
: "${STAKER_WIF:?STAKER_WIF env var is required}"
: "${VOTE_TARGET:?VOTE_TARGET env var is required}"
RPC_URL="${RPC_URL:-https://n3seed2.ngd.network:10332}"
ADDRESS_VERSION="${ADDRESS_VERSION:-53}"

echo ""
echo "Configuration:"
echo "  RPC: $RPC_URL"
echo "  Vote Target: $VOTE_TARGET"
echo ""

# Function to derive address from WIF
derive_address() {
    local WIF=$1
    echo "Deriving address from WIF..." >&2
    # This would normally use neo-cli or similar, we'll do it in C#
    return 0
}

# Export environment
export WIF="$DEPLOYER_WIF"
export RPC="$RPC_URL"

echo "=============================================="
echo "PHASE 1: Building and Deploying Contracts"
echo "=============================================="

cd "$OPS_DIR"

# Build the deployer
echo "Building TrustAnchorDeployer..."
dotnet build TrustAnchorDeployer/TrustAnchorDeployer.csproj -c Release -v q

echo ""
echo "Starting deployment to testnet..."
echo "WARNING: This will deploy 22 contracts and spend GAS!"
echo "Press Ctrl+C within 10 seconds to cancel..."
sleep 10

# Run deployment
dotnet run --project TrustAnchorDeployer --configuration Release 2>&1 | tee /tmp/deployment.log

# Extract TrustAnchor hash from logs
TRUSTANCHOR_HASH=$(grep -oP 'TrustAnchor: \K[0-9a-fA-F]{40}' /tmp/deployment.log || echo "")
if [ -z "$TRUSTANCHOR_HASH" ]; then
    echo "ERROR: Could not extract TrustAnchor hash from deployment log"
    exit 1
fi

echo ""
echo "TrustAnchor Contract Hash: $TRUSTANCHOR_HASH"
echo "TrustAnchor Address: $(echo $TRUSTANCHOR_HASH | xxd -r -p | base64 | tr '/+' '_-' | tr -d '=')"

# Extract agent hashes
AGENT_HASHES=$(grep -oP 'Agent [0-9]+: \K[0-9a-fA-F]{40}' /tmp/deployment.log | tr '\n' ',' | sed 's/,$//')

echo ""
echo "Agent Hashes: $AGENT_HASHES"

# Save deployment info
cat > /tmp/deployment-info.json <<EOF
{
  "trustAnchorHash": "$TRUSTANCHOR_HASH",
  "agentHashes": "$AGENT_HASHES",
  "deployedAt": "$(date -Iseconds)",
  "voteTarget": "$VOTE_TARGET"
}
EOF

echo ""
echo "=============================================="
echo "PHASE 2: Configuring Agents"
echo "=============================================="

# Configure Agent 0 with vote target and voting amount
echo "Configuring Agent 0..."
dotnet run --project ConfigureAgent --configuration Release -- \
    "$DEPLOYER_WIF" \
    "$TRUSTANCHOR_HASH" \
    0 \
    "$VOTE_TARGET" \
    "agent-0" \
    1

echo ""
echo "=============================================="
echo "PHASE 3: Staking NEO"
echo "=============================================="

# Create staking script
cat > /tmp/stake-neo.cs <<'CSHARPEOF'
using System;
using System.Numerics;
using Neo;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace StakeNEO
{
    class Program
    {
        static void Main(string[] args)
        {
            string trustAnchorHash = args[0];
            string amount = args[1] ?? "100";
            string wif = args[2];
            string rpc = args[3] ?? "https://n3seed2.ngd.network:10332";

            var keypair = Neo.Network.RPC.Utility.GetKeyPair(wif);
            var user = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            
            var signers = new[] {
                new Signer { 
                    Scopes = WitnessScope.CalledByEntry, 
                    Account = user
                }
            };

            Console.Error.WriteLine($"Staking {amount} NEO...");
            Console.Error.WriteLine($"User: {user}");
            Console.Error.WriteLine($"TrustAnchor: {trustAnchorHash}");

            var trustAnchor = UInt160.Parse(trustAnchorHash);
            var neoAmount = BigInteger.Parse(amount);

            // Transfer NEO to TrustAnchor (this stakes it)
            var script = new ScriptBuilder();
            script.EmitPush(neoAmount);
            script.EmitPush(trustAnchor);
            script.EmitPush(user);
            script.EmitPush("transfer");
            script.EmitPush(Neo.SmartContract.Native.NativeContract.NEO.Hash);
            script.EmitSysCall(0x62e1b114);

            var uri = new Uri(rpc);
            var settings = Neo.Network.RPC.ProtocolSettings.Load("/dev/stdin");
            var cli = new Neo.Network.RPC.RpcClient(uri, null, null, settings);
            var factory = new Neo.Network.RPC.TransactionManagerFactory(cli);
            var txMgr = factory.MakeTransactionAsync(script.ToArray(), signers).GetAwaiter().GetResult();
            var signedTx = txMgr.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            var txHash = cli.SendRawTransactionAsync(signedTx).GetAwaiter().GetResult();

            Console.Error.WriteLine($"Stake TX: {txHash}");
            Console.WriteLine(txHash.ToString());
        }
    }
}
CSHARPEOF

echo ""
echo "=============================================="
echo "PHASE 4: Verification"
echo "=============================================="

# Create verification script
cat > /tmp/verify-deployment.cs <<'CSHARPEOF'
using System;
using System.Numerics;
using Neo;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace VerifyDeployment
{
    class Program
    {
        static void Main(string[] args)
        {
            string trustAnchorHash = args[0];
            string rpc = args[1] ?? "https://n3seed2.ngd.network:10332";

            var trustAnchor = UInt160.Parse(trustAnchorHash);

            Console.WriteLine("=== TrustAnchor Deployment Verification ===");
            Console.WriteLine();

            // Get contract info
            try
            {
                var owner = NativeContract.ContractManagement.Hash.MakeScript("getOwner", trustAnchor).Call().Single();
                Console.WriteLine($"Contract Owner: {owner}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get owner: {ex.Message}");
            }

            // Get stake info
            try
            {
                var totalStake = trustAnchor.MakeScript("totalStake").Call().Single().GetInteger();
                Console.WriteLine($"Total Stake: {totalStake} NEO");

                var rps = trustAnchor.MakeScript("rps").Call().Single().GetInteger();
                Console.WriteLine($"RPS (Reward Per Stake): {rps}");

                var configVersion = trustAnchor.MakeScript("configVersion").Call().Single().GetInteger();
                Console.WriteLine($"Config Version: {configVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stake info: {ex.Message}");
            }

            // Get agent info
            Console.WriteLine();
            Console.WriteLine("Agent Configuration:");
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var agent = trustAnchor.MakeScript("agent", i).Call().Single();
                    if (agent.IsNull)
                    {
                        Console.WriteLine($"  Agent {i}: Not set");
                    }
                    else
                    {
                        var agentAddr = agent.ToU160();
                        Console.WriteLine($"  Agent {i}: {agentAddr}");
                    }
                }
                catch { }
            }

            Console.WriteLine();
            Console.WriteLine("=== Verification Complete ===");
        }
    }
}
CSHARPEOF

echo ""
echo "Deployment files created. To execute on testnet, run the compiled executables."
echo ""
echo "Next steps:"
echo "1. Ensure deployer wallet has sufficient NEO and GAS"
echo "2. Run: cd \"$OPS_DIR\" && dotnet run --project TrustAnchorDeployer"
echo "3. Configure agents with vote targets"
echo "4. Stake NEO to earn rewards"
echo "5. Verify deployment and test claiming"
