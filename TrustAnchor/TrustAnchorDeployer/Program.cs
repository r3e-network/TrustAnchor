using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LibRPC;
using Neo;
using Neo.Cryptography;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace TrustAnchorDeployer
{
    class Program
    {
        private static readonly string OWNER_HASH = Environment.GetEnvironmentVariable("OWNER_HASH");
        private static readonly uint MAXAGENTS = 21;
        private static readonly string RPC = Environment.GetEnvironmentVariable("RPC") ?? "https://testnet1.neo.coz.io:443";

        internal static string ResolveContractsDir()
        {
            var overrideDir = Environment.GetEnvironmentVariable("CONTRACTS_DIR")
                ?? Environment.GetEnvironmentVariable("SCRIPTS_DIR");
            if (!string.IsNullOrWhiteSpace(overrideDir))
            {
                return Path.GetFullPath(overrideDir);
            }

            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "contract");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Contract source directory not found. Set CONTRACTS_DIR.");
        }

        internal static string ResolveNccsPath()
        {
            var overridePath = Environment.GetEnvironmentVariable("NCCS_PATH");
            return string.IsNullOrWhiteSpace(overridePath) ? "nccs" : overridePath;
        }

        private static KeyPair GetKeyPair()
        {
            var wif = Environment.GetEnvironmentVariable("WIF");
            if (string.IsNullOrWhiteSpace(wif))
            {
                throw new InvalidOperationException("WIF is required. Set WIF env var.");
            }
            return Neo.Network.RPC.Utility.GetKeyPair(wif);
        }

        private static (KeyPair keypair, UInt160 deployer, Signer[] signers) GetWallet()
        {
            var keypair = GetKeyPair();
            var deployer = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            var signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = deployer } };
            return (keypair, deployer, signers);
        }

        static void Main(string[] args)
        {
            var (_, deployer, _) = GetWallet();
            Console.WriteLine($"=== TrustAnchor Deployment Tool ===");
            Console.WriteLine($"Deployer: {deployer}");
            Console.WriteLine($"RPC: {RPC}");

            // Deploy TrustAnchor
            var trustAnchorHash = DeployTrustAnchor(deployer);
            Console.WriteLine($"TrustAnchor: {trustAnchorHash}");

            // Deploy Agents
            var agentHashes = DeployAgents(trustAnchorHash);
            Console.WriteLine($"Deployed {agentHashes.Count} agents");

            // Register agents
            for (int i = 0; i < agentHashes.Count; i++)
            {
                var txHash = InvokeContract(trustAnchorHash, "setAgent", i, agentHashes[i]);
                Console.WriteLine($"Agent {i} registered: {txHash}");
            }

            // Configure
            InvokeContract(trustAnchorHash, "beginConfig");
            for (int i = 0; i < MAXAGENTS; i++)
            {
                InvokeContract(trustAnchorHash, "setAgentConfig", i, new byte[33], BigInteger.One);
            }
            var finalizeTx = InvokeContract(trustAnchorHash, "finalizeConfig");
            Console.WriteLine($"FinalizeConfig: {finalizeTx}");

            Console.WriteLine("=== Deployment Complete ===");
        }

        static UInt160 DeployTrustAnchor(UInt160 deployer)
        {
            Console.WriteLine("Deploying TrustAnchor...");
            var (nefPath, nefBytes, scriptHash) = CompileContract("TrustAnchor.cs", "TrustAnchor", OWNER_HASH ?? deployer.ToString());
            var manifestPath = Path.Combine(Path.GetDirectoryName(nefPath), Path.GetFileNameWithoutExtension(nefPath) + ".manifest.json");
            var manifestJson = File.ReadAllText(manifestPath);

            var txHash = SendTransaction(nefBytes, manifestJson);
            Console.WriteLine($"TrustAnchor TX: {txHash}");
            return scriptHash;
        }

        static List<UInt160> DeployAgents(UInt160 trustAnchorHash)
        {
            Console.WriteLine($"Deploying {MAXAGENTS} agents...");
            var agentHashes = new List<UInt160>();

            for (uint i = 0; i < MAXAGENTS; i++)
            {
                var (nefPath, nefBytes, scriptHash) = CompileContract("TrustAnchorAgent.cs", $"TrustAnchorAgent{i}", trustAnchorHash.ToString());
                var manifestPath = Path.Combine(Path.GetDirectoryName(nefPath), Path.GetFileNameWithoutExtension(nefPath) + ".manifest.json");
                var manifestJson = File.ReadAllText(manifestPath);
                agentHashes.Add(scriptHash);

                var txHash = SendTransaction(nefBytes, manifestJson);
                Console.WriteLine($"Agent {i}: {scriptHash}, TX: {txHash}");
            }

            return agentHashes;
        }

        static string InvokeContract(UInt160 contract, string method, params object[] args)
        {
            var script = BuildScript(contract, method, args);
            return SendTransaction(script);
        }

        static byte[] BuildScript(UInt160 contract, string method, params object[] args)
        {
            var sb = new ScriptBuilder();
            
            foreach (var arg in args.Reverse())
            {
                switch (arg)
                {
                    case BigInteger bi:
                        sb.EmitPush(bi);
                        break;
                    case int i:
                        sb.EmitPush(i);
                        break;
                    case byte[] bytes:
                        sb.EmitPush(bytes);
                        break;
                    case UInt160 u:
                        sb.EmitPush(u.GetSpan());
                        break;
                    default:
                        throw new NotSupportedException(arg.GetType().ToString());
                }
            }
            
            sb.EmitPush(method);
            sb.EmitPush(contract.GetSpan());
            sb.EmitSysCall(0x627C3A32); // System.Contract.Call
            
            return sb.ToArray();
        }

        static string SendTransaction(byte[] script)
        {
            var (keypair, _, signers) = GetWallet();
            var txMgr = script.TxMgr(signers);
            var signedTx = txMgr.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            return signedTx.Send().ToString();
        }

        static string SendTransaction(byte[] nef, string manifest)
        {
            var sb = new ScriptBuilder();
            sb.EmitPush(System.Text.Encoding.UTF8.GetBytes(manifest));
            sb.EmitPush(nef);
            sb.EmitPush("deploy");
            sb.EmitPush(NativeContract.ContractManagement.Hash.GetSpan());
            sb.EmitSysCall(0x627C3A32);
            
            return SendTransaction(sb.ToArray());
        }

        static string PostRpc(string method, string paramsJson)
        {
            var client = new System.Net.Http.HttpClient();
            var json = $"{{\"jsonrpc\":\"2.0\",\"method\":\"{method}\",\"params\":{paramsJson},\"id\":1}}";
            var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = client.PostAsync(RPC, content).GetAwaiter().GetResult();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        static string ParseHash(string json)
        {
            var start = json.IndexOf("\"result\":\"") + 10;
            var end = json.IndexOf("\"", start);
            return json.Substring(start, end - start);
        }

        static (string nefPath, byte[] nefBytes, UInt160 scriptHash) CompileContract(string sourceFileName, string contractName, string argsHash)
        {
            var contractsDir = ResolveContractsDir();
            string sourcePath = Path.Combine(contractsDir, sourceFileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Cannot find {sourceFileName} in {contractsDir}");
            }

            var source = File.ReadAllText(sourcePath);
            source = source.Replace("[TODO]: ARGS", argsHash);

            if (contractName.StartsWith("TrustAnchorAgent") && contractName != "TrustAnchorAgent")
            {
                var index = contractName.Replace("TrustAnchorAgent", "");
                source = source.Replace("class TrustAnchorAgent", $"class TrustAnchorAgent{index}");
                var uniqueCode = $"\n        private const byte AGENT_INDEX = {index};\n\n        public static byte GetAgentIndex() => AGENT_INDEX;\n";
                var braceIdx = source.IndexOf("{", source.IndexOf($"class TrustAnchorAgent{index}"));
                if (braceIdx >= 0)
                {
                    source = source.Insert(braceIdx + 1, uniqueCode);
                }
            }

            var tempDir = Path.Combine(Path.GetTempPath(), contractName);
            Directory.CreateDirectory(tempDir);
            var tempSource = Path.Combine(tempDir, $"{contractName}.cs");
            File.WriteAllText(tempSource, source);

            var psi = new ProcessStartInfo
            {
                FileName = ResolveNccsPath(),
                Arguments = $"\"{tempSource}\" -o \"{tempDir}\"",
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

            var nefPath = Directory.GetFiles(tempDir, "*.nef").FirstOrDefault();
            if (nefPath == null)
            {
                throw new Exception("NEF file not found");
            }

            var nefBytes = File.ReadAllBytes(nefPath);
            int headerSize = 76;
            int scriptLen = nefBytes.Length - headerSize - 4;
            var script = new byte[scriptLen];
            Array.Copy(nefBytes, headerSize, script, 0, scriptLen);

            var hash = SHA256.Create().ComputeHash(script);
            var scriptHash = new UInt160(hash.Take(20).ToArray());

            return (nefPath, nefBytes, scriptHash);
        }
    }
}
