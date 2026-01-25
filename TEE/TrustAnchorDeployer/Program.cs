using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using LibHelper;
using LibRPC;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace TrustAnchorDeployer
{
    class Program
    {
        public static readonly string WIF = Environment.GetEnvironmentVariable("WIF");
        private static readonly string OWNER_HASH = Environment.GetEnvironmentVariable("OWNER_HASH");
        private static readonly string CONTRACTS_DIR = Environment.GetEnvironmentVariable("CONTRACTS_DIR") ?? "../../contract";
        private static readonly uint MAXAGENTS = 21;

        public static readonly KeyPair keypair = Neo.Network.RPC.Utility.GetKeyPair(WIF);
        private static readonly UInt160 deployer = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
        public static readonly Signer[] signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = deployer } };

        static void Main(string[] args)
        {
            $"=== TrustAnchor Deployment Tool ===".Log();
            $"Deployer: {deployer}".Log();
            $"Owner: {OWNER_HASH}".Log();
            $"Contracts: {CONTRACTS_DIR}".Log();

            // Step 1: Deploy TrustAnchor contract
            UInt160 trustAnchorHash = DeployTrustAnchor();
            $"TrustAnchor deployed: {trustAnchorHash}".Log();

            // Step 2: Deploy 21 Agent contracts
            var agentHashes = DeployAgents(trustAnchorHash);
            $"Deployed {agentHashes.Count} Agent contracts".Log();

            // Step 3: Register all agents
            RegisterAgents(trustAnchorHash, agentHashes);
            $"All agents registered".Log();

            // Step 4: Initial configuration
            ConfigureInitialSetup(trustAnchorHash);
            $"Initial configuration complete".Log();

            $"=== Deployment Summary ===".Log();
            $"TrustAnchor: {trustAnchorHash}".Log();
            for (int i = 0; i < agentHashes.Count; i++)
            {
                $"Agent {i}: {agentHashes[i]}".Log();
            }
        }

        static UInt160 DeployTrustAnchor()
        {
            $"Step 1: Deploying TrustAnchor contract...".Log();

            // Compile contract and get NEF file
            var (nefPath, nefBytes, scriptHash) = CompileContract("TrustAnchor.cs", "TrustAnchor", OWNER_HASH);
            var manifestJson = File.ReadAllText(nefPath.Replace(".nef", ".manifest.json"));

            // Create deployment script using EmitDynamicCall
            var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", nefBytes, System.Text.Encoding.UTF8.GetBytes(manifestJson));

            var scriptBytes = script.ToArray();
            var txHash = scriptBytes.SendTx();

            $"Deployed with TX: {txHash}".Log();
            return scriptHash;
        }

        static System.Collections.Generic.List<UInt160> DeployAgents(UInt160 trustAnchorHash)
        {
            $"\nStep 2: Deploying {MAXAGENTS} Agent contracts...".Log();

            var agentHashes = new System.Collections.Generic.List<UInt160>();

            for (uint i = 0; i < MAXAGENTS; i++)
            {
                $"Deploying Agent {i}/{MAXAGENTS}...".Log();

                // Compile Agent contract with TrustAnchor hash
                var (nefPath, nefBytes, scriptHash) = CompileContract("TrustAnchorAgent.cs", $"TrustAnchorAgent{i}", trustAnchorHash.ToString());
                var manifestJson = File.ReadAllText(nefPath.Replace(".nef", ".manifest.json"));
                agentHashes.Add(scriptHash);

                // Create deployment script using EmitDynamicCall
                var script = new ScriptBuilder();
                script.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", nefBytes, System.Text.Encoding.UTF8.GetBytes(manifestJson));

                var scriptBytes = script.ToArray();
                var txHash = scriptBytes.SendTx();

                $"Agent {i} deployed: {scriptHash}, TX: {txHash}".Log();
            }

            return agentHashes;
        }

        static void RegisterAgents(UInt160 trustAnchorHash, System.Collections.Generic.List<UInt160> agentHashes)
        {
            $"\nStep 3: Registering agents...".Log();

            for (uint i = 0; i < agentHashes.Count; i++)
            {
                $"Registering Agent {i}...".Log();

                var script = new ScriptBuilder();
                script.EmitDynamicCall(trustAnchorHash, "setAgent", i, agentHashes[(int)i]);

                var scriptBytes = script.ToArray();
                var txHash = scriptBytes.SendTx();

                $"Agent {i} registered, TX: {txHash}".Log();
            }
        }

        static void ConfigureInitialSetup(UInt160 trustAnchorHash)
        {
            $"\nStep 4: Initial configuration...".Log();

            // Begin config
            $"Beginning configuration...".Log();
            var script = new ScriptBuilder();
            script.EmitDynamicCall(trustAnchorHash, "beginConfig");
            script.ToArray().SendTx();

            // Set all 21 agents with weight 1 each (temporary equal distribution)
            $"Setting agent weights to 1 each...".Log();
            for (uint i = 0; i < MAXAGENTS; i++)
            {
                // Generate dummy target (placeholder, will be configured later)
                var dummyTarget = new byte[33]; // 33 bytes for compressed ECPoint
                dummyTarget[32] = 0x02; // Even y coordinate

                var weight = BigInteger.One;

                var setConfigScript = new ScriptBuilder();
                setConfigScript.EmitDynamicCall(trustAnchorHash, "setAgentConfig", i, dummyTarget, weight);

                setConfigScript.ToArray().SendTx();
            }

            // Finalize config
            $"Finalizing configuration...".Log();
            var finalizeScript = new ScriptBuilder();
            finalizeScript.EmitDynamicCall(trustAnchorHash, "finalizeConfig");
            finalizeScript.ToArray().SendTx();

            $"Initial config complete".Log();
        }

        static (string nefPath, byte[] nefBytes, UInt160 scriptHash) CompileContract(string sourceFileName, string contractName, string argsHash)
        {
            var sourcePath = Path.GetFullPath(Path.Combine(CONTRACTS_DIR, sourceFileName));
            var source = File.ReadAllText(sourcePath);
            source = source.Replace("[TODO]: ARGS", $"\"{argsHash}\"");

            // Write to temp file
            var tempDir = Path.Combine(Path.GetTempPath(), contractName);
            Directory.CreateDirectory(tempDir);
            var tempSource = Path.Combine(tempDir, $"{contractName}.cs");
            File.WriteAllText(tempSource, source);

            // Compile using nccs
            var psi = new ProcessStartInfo
            {
                FileName = "nccs",
                Arguments = $"build \"{tempDir}\" -o \"{tempDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Compilation failed: {error}{output}");
            }

            var nefPath = Path.Combine(tempDir, $"{contractName}.nef");
            if (!File.Exists(nefPath))
            {
                throw new Exception($"NEF file not found: {nefPath}");
            }

            var nefBytes = File.ReadAllBytes(nefPath);

            // Compute script hash from NEF bytes (skip header, use script portion)
            // NEF file format: Magic(4) + Compiler(4) + Version(2) + ScriptLength(2) + Script(...) + Checksum(4)
            int scriptOffset = 12; // 4+4+2+2
            int scriptLength = BitConverter.ToUInt16(nefBytes, 10);
            byte[] scriptBytes = new byte[scriptLength];
            Array.Copy(nefBytes, scriptOffset, scriptBytes, 0, scriptLength);

            // Use SHA256 to compute script hash
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(scriptBytes);
            var scriptHash = new UInt160(hash.Take(20).ToArray());

            return (nefPath, nefBytes, scriptHash);
        }
    }

    // Extension methods
    public static class Extensions
    {
        public static UInt256 SendTx(this byte[] script)
        {
            var txMgr = script.TxMgr(Program.signers);
            var signedTx = txMgr.AddSignature(Program.keypair).SignAsync().GetAwaiter().GetResult();
            return signedTx.Send();
        }
    }
}
