using System;
using System.IO;

namespace TrustAnchorDeployer
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== TrustAnchor Deployment Script Generator ===");
            Console.WriteLine();

            var ownerHash = Environment.GetEnvironmentVariable("OWNER_HASH");
            if (string.IsNullOrEmpty(ownerHash))
            {
                Console.WriteLine("Error: OWNER_HASH environment variable is required");
                Console.WriteLine("Usage: OWNER_HASH=<your_hash> dotnet run");
                return;
            }

            var outputDir = Path.Combine("output", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"Output directory: {outputDir}");

            // Generate TrustAnchor deployment scripts
            GenerateTrustAnchorDeployment(ownerHash, outputDir);

            // Generate Agent deployment scripts
            GenerateAgentDeployment(outputDir);

            // Generate registration scripts
            GenerateRegistrationScripts(ownerHash, outputDir);

            // Generate configuration script
            GenerateConfigurationScript(ownerHash, outputDir);

            Console.WriteLine("\n=== Deployment Scripts Generated ===");
            Console.WriteLine($"Location: {outputDir}/");
            Console.WriteLine("\nNext steps:");
            Console.WriteLine("1. Review generated scripts in the output directory");
            Console.WriteLine("2. Execute scripts in order:");
            Console.WriteLine("   1_deploy_trustanchor.sh");
            Console.WriteLine("   2_deploy_agents.sh");
            Console.WriteLine("   3_register_agents.sh");
            Console.WriteLine("   4_initial_config.sh");
        }

        static void GenerateTrustAnchorDeployment(string ownerHash, string outputDir)
        {
            Console.WriteLine("Generating TrustAnchor deployment script...");

            var script = $"#!/usr/bin/env bash\n" +
                $"set -euo pipefail\n\n" +
                $"echo \"Deploying TrustAnchor contract...\"\n" +
                $"OWNER_HASH=\"{ownerHash}\"\n\n" +
                $"# Compile contract\n" +
                $"sourcePath=\"contract/TrustAnchor.cs\"\n" +
                $"tempDir=\"build/trustanchor\"\n" +
                $"mkdir -p \"$tempDir\"\n\n" +
                $"# Replace [TODO]: ARGS with owner hash\n" +
                $"sed \"s/\\\\[TODO\\\\]: ARGS/$OWNER_HASH/g\" \"$sourcePath\" > \"$tempDir/TrustAnchor.cs\"\n\n" +
                $"# Compile with nccs\n" +
                $"nccs build \"$tempDir\" -o \"$tempDir\" || {{ echo \"Compilation failed\"; exit 1; }}\n\n" +
                $"echo \"TrustAnchor compiled successfully\"\n" +
                $"echo \"NEF: $tempDir/TrustAnchor.nef\"\n" +
                $"echo \"Manifest: $tempDir/TrustAnchor.manifest.json\"\n" +
                $"echo \"\"\n" +
                $"echo \"Contract hash can be found by running:\"\n" +
                $"echo \"  neo-cli contract hash $tempDir/TrustAnchor.nef\"\n";

            File.WriteAllText(Path.Combine(outputDir, "1_deploy_trustanchor.sh"), script);
            Console.WriteLine("  Created: 1_deploy_trustanchor.sh");
        }

        static void GenerateAgentDeployment(string outputDir)
        {
            Console.WriteLine("Generating Agent deployment scripts...");

            var script = $"#!/usr/bin/env bash\n" +
                $"set -euo pipefail\n\n" +
                $"echo \"Deploying 21 Agent contracts...\"\n\n" +
                $"TRUSTANCHOR_HASH=\"$1\"  # Passed as argument\n" +
                $"sourcePath=\"contract/TrustAnchorAgent.cs\"\n" +
                $"outputDir=\"build/agents\"\n" +
                $"mkdir -p \"$outputDir\"\n\n" +
                $"for i in {{0..20}}\n" +
                $"do\n" +
                $"  echo \"Deploying Agent $i...\"\n" +
                $"  tempDir=\"build/agent$i\"\n" +
                $"  mkdir -p \"$tempDir\"\n\n" +
                $"  # Replace [TODO]: ARGS with TrustAnchor hash\n" +
                $"  sed \"s/\\\\[TODO\\\\]: ARGS/$TRUSTANCHOR_HASH/g\" \"$sourcePath\" > \"$tempDir/TrustAnchorAgent.cs\"\n\n" +
                $"  # Compile\n" +
                $"  nccs build \"$tempDir\" -o \"$tempDir\" || {{ echo \"Compilation failed\"; exit 1; }}\n\n" +
                $"  echo \"Agent $i compiled:\"\n" +
                $"  echo \"  NEF: $tempDir/TrustAnchorAgent.nef\"\n" +
                $"  echo \"  Manifest: $tempDir/TrustAnchorAgent.manifest.json\"\n" +
                $"  echo \"\"\n" +
                $"  # Get contract hash\n" +
                $"  agentHash=$(neo-cli contract hash \"$tempDir/TrustAnchorAgent.nef\")\n" +
                $"  echo \"Agent $i hash: $agentHash\"\n" +
                $"  echo \"$agentHash\" > \"$outputDir/agent_$i.hash\"\n" +
                $"done\n" +
                $"echo \"All 21 agents deployed\"\n" +
                $"echo \"Agent hashes saved to $outputDir/agent_*.hash\"\n" +
                $"echo \"\"\n" +
                $"cat \"$outputDir\"/agent_*.hash\n" +
                $"cat \"$outputDir\"/agent_*.hash > \"$outputDir/all_agents.txt\"\n" +
                $"echo \"All agent hashes saved to $outputDir/all_agents.txt\"\n" +
                $"echo \"\"\n" +
                $"echo \"IMPORTANT: Save these hashes for the registration step\"\n" +
                $"echo \"Note: Deploy 21 agent contracts even if you only need a subset initially\"\n";

            File.WriteAllText(Path.Combine(outputDir, "2_deploy_agents.sh"), script);
            Console.WriteLine("  Created: 2_deploy_agents.sh");
        }

        static void GenerateRegistrationScripts(string ownerHash, string outputDir)
        {
            Console.WriteLine("Generating agent registration scripts...");

            var script = $"#!/usr/bin/env bash\n" +
                $"set -euo pipefail\n\n" +
                $"TRUSTANCHOR_HASH=\"$1\"  # TrustAnchor contract hash\n" +
                $"AGENT_HASHES_FILE=\"$2\"  # File containing agent hashes (one per line)\n" +
                $"WIF=\"$3\"  # Wallet WIF for signing transactions\n\n" +
                $"echo \"Registering agents with TrustAnchor contract...\"\n" +
                $"echo \"TrustAnchor: $TRUSTANCHOR_HASH\"\n" +
                $"echo \"\"\n" +
                $"index=0\n" +
                $"while IFS= read -r agentHash\n" +
                $"do\n" +
                $"  echo \"Registering Agent $index: $agentHash\"\n" +
                $"  \n" +
                $"  # Build setAgent invocation script\n" +
                $"  # This would need to be done using neo-cli with proper wallet RPC\n" +
                $"  neo-cli contract invoke \"$TRUSTANCHOR_HASH\" setAgent \\[\n" +
                $"    index: $index,\\\n" +
                $"    agent: $agentHash\\\n" +
                $"  ] -- --wif \"$WIF\" --rpc-endpoint <your-rpc>\n" +
                $"  \\\n" +
                $"  ((index++))\n" +
                $"done < \"$AGENT_HASHES_FILE\"\n" +
                $"\\\n" +
                $"echo \"All agents registered\"\n" +
                $"echo \"\"\n" +
                $"echo \"Verification:\"\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" agent --rpc-endpoint <your-rpc> --index 0\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" agent --rpc-endpoint <your-rpc> --index 1\n" +
                $"# ... check all 21 agents\n" +
                $"\\\n" +
                $"echo \"Agents should all be registered and callable\"\n" +
                $"echo \"\"\n" +
                $"echo \"Note: You can verify agent registration by checking:\"\n" +
                $"echo \"  neo-cli contract invoke \"$TRUSTANCHOR_HASH\" agent --index <0-20>\"\n" +
                $"echo \"\"\n" +
                $"echo \"Next: Run initial configuration to set up weights and targets\"\n" +
                $"echo \"       ./4_initial_config.sh $TRUSTANCHOR_HASH\"\n" +
                $"echo \"\"\n" +
                $"echo \"IMPORTANT: Before running initial_config.sh:\"\n" +
                $"echo \"1. Ensure all 21 agents are registered\"\n" +
                $"echo \"2. Fund the TrustAnchor contract with NEO (for testing)\"\n" +
                $"echo \"3. Fund each agent contract with NEO (for testing)\"\n" +
                $"echo \"4. Configure appropriate voting targets (ECPoint of candidates)\"\n" +
                $"echo \"5. Set appropriate weights based on your distribution strategy\"\n";

            File.WriteAllText(Path.Combine(outputDir, "3_register_agents.sh"), script);
            Console.WriteLine("  Created: 3_register_agents.sh");
        }

        static void GenerateConfigurationScript(string ownerHash, string outputDir)
        {
            Console.WriteLine("Generating initial configuration script...");

            var script = $"#!/usr/bin/env bash\n" +
                $"set -euo pipefail\n\n" +
                $"TRUSTANCHOR_HASH=\"$1\"\n" +
                $"WIF=\"$2\"\n" +
                $"RPC_ENDPOINT=\"$3\"\n\n" +
                $"echo \"Configuring TrustAnchor contract...\"\n" +
                $"echo \"\"\n" +
                $"echo \"Step 1: Begin configuration\"\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" beginConfig \\\n" +
                $"  --wif \"$WIF\" --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"echo \"\"\n" +
                $"echo \"Step 2: Configure agent targets and weights\"\n" +
                $"echo \"IMPORTANT: Update the target addresses below with actual candidate ECPoints!\"\n" +
                $"echo \"\"\n" +
                $"# Example: Configure first 3 agents with equal weights\n" +
                $"# You would replace the target bytes with actual ECPoint values\n" +
                $"\\\n" +
                $"# Agent 0: weight 7, target = [candidate 1 ECPOINT]\n" +
                $"TARGET_0=\"0x\" # Replace with actual 33-byte ECPoint\n" +
                $"WEIGHT_0=7\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" setAgentConfig \\[\n" +
                $"  index: 0,\\\n" +
                $"  target: $TARGET_0,\\\n" +
                $"  weight: $WEIGHT_0\\\n" +
                $"] \\\n" +
                $"  --wif \"$WIF\" --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"\\\n" +
                $"# Agent 1: weight 7, target = [candidate 2 ECPOINT]\n" +
                $"TARGET_1=\"0x\" # Replace with actual 33-byte ECPoint\n" +
                $"WEIGHT_1=7\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" setAgentConfig \\[\n" +
                $"  index: 1,\\\n" +
                $"  target: $TARGET_1,\\\n" +
                $"  weight: $WEIGHT_1\\\n" +
                $"] \\\n" +
                $"  --wif \"$WIF\" --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"\\\n" +
                $"# Continue for all 21 agents... weights must sum to 21\n" +
                $"# You can use setAgentWeights to set all weights at once\n" +
                $"\\\n" +
                $"echo \"\"\n" +
                $"echo \"Step 3: Finalize configuration\"\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" finalizeConfig \\ \n" +
                $"  --wif \"$WIF\" --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"echo \"\"\n" +
                $"echo \"Configuration complete!\"\n" +
                $"echo \"\"\n" +
                $"echo \"Verification:\" \n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" configVersion --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" agentTarget --index 0 --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"neo-cli contract invoke \"$TRUSTANCHOR_HASH\" agentWeight --index 0 --rpc-endpoint \"$RPC_ENDPOINT\"\n" +
                $"echo \"\"\n" +
                $"echo \"IMPORTANT:\"\n" +
                $"echo \"- After deployment, send NEO to TrustAnchor to enable staking\"\n" +
                $"echo \"- Send NEO to each agent contract for voting power\"\n" +
                $"echo \"- Configure voting targets with actual candidate addresses\"\n" +
                $"echo \"- Weights must sum to 21 across all 21 agents\"\n" +
                $"echo \"\"\n" +
                $"echo \"Your contract is now live!\"\n" +
                $"echo \"\"\n" +
                $"echo \"TrustAnchor: $TRUSTANCHOR_HASH\"\n" +
                $"echo \"Owner: {ownerHash}\"\n" +
                $"echo \"\"\n" +
                $"echo \"Next steps:\"" +
                $"echo \"1. Fund TrustAnchor with NEO (users will stake to this contract)\n" +
                $"echo \"2. Call RebalanceVotes to distribute NEO to agents\"\n" +
                $"echo \"3. Agents will vote for configured targets and earn GAS\"\n" +
                $"echo \"4. Use TrustAnchorClaimer/Representative to distribute GAS to stakers\"\n" +
                $"echo \"\"\n" +
                $"echo \"See TEE/README.md for operational tools documentation\"\n";

            File.WriteAllText(Path.Combine(outputDir, "4_initial_config.sh"), script);
            Console.WriteLine("  Created: 4_initial_config.sh");
        }
    }
}
