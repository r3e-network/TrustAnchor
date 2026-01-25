using System;
using System.Numerics;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace ConfigureAgent
{
    class Program
    {
        private static readonly string RPC = Environment.GetEnvironmentVariable("RPC") ?? "https://testnet1.neo.coz.io:443";
        private static readonly Uri URI = new Uri(RPC);
        private static readonly ProtocolSettings settings = new ProtocolSettings
        {
            AddressVersion = 53,
            Network = 894710606,
            MillisecondsPerBlock = 3000,
            MaxTraceableBlocks = 2102400,
            MaxValidUntilBlockIncrement = 5760,
            MaxTransactionsPerBlock = 5000,
            MemoryPoolMaxTransactions = 50000,
            ValidatorsCount = 7
        };
        private static readonly Neo.Network.RPC.RpcClient CLI = new Neo.Network.RPC.RpcClient(URI, null, null, settings);
        private static readonly Neo.Network.RPC.TransactionManagerFactory factory = new Neo.Network.RPC.TransactionManagerFactory(CLI);

        static void Main(string[] args)
        {
            string wif = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("WIF");
            string trustAnchorHash = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("TRUSTANCHOR");
            int agentIndex = args.Length > 2 ? int.Parse(args[2]) : 1;
            string voteTarget = args.Length > 3 ? args[3] : "036d13bdb7da325738167f2adde14e424e8cbfebc5501437a22f4e7668a3116168";
            BigInteger weight = args.Length > 4 ? BigInteger.Parse(args[4]) : 21;

            Console.WriteLine($"=== Configuring Agent {agentIndex} ===");
            Console.WriteLine($"TrustAnchor: {trustAnchorHash}");
            Console.WriteLine($"Vote Target: {voteTarget}");
            Console.WriteLine($"Weight: {weight}");

            var keypair = Neo.Network.RPC.Utility.GetKeyPair(wif);
            var deployer = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            
            var signers = new[] 
            { 
                new Signer 
                { 
                    Scopes = WitnessScope.Global, 
                    Account = deployer 
                } 
            };

            var trustAnchor = UInt160.Parse(trustAnchorHash);
            var voteBytes = Convert.FromHexString(voteTarget);

            Console.WriteLine($"\nDeployer: {deployer}");

            // Begin config
            Console.WriteLine("\nStep 1: Beginning configuration...");
            var beginScript = new ScriptBuilder();
            beginScript.EmitPush(System.Text.Encoding.UTF8.GetBytes("beginConfig"));
            beginScript.EmitPush(trustAnchor.GetSpan());
            beginScript.EmitSysCall(0x62e1b114);
            var txHash = SendTx(beginScript.ToArray(), signers, keypair);
            Console.WriteLine($"BeginConfig TX: {txHash}");

            // Set agent config
            Console.WriteLine($"\nStep 2: Setting Agent {agentIndex} config...");
            var configScript = new ScriptBuilder();
            configScript.EmitPush(weight);
            configScript.EmitPush(voteBytes);
            configScript.EmitPush(new BigInteger(agentIndex));
            configScript.EmitPush(System.Text.Encoding.UTF8.GetBytes("setAgentConfig"));
            configScript.EmitPush(trustAnchor.GetSpan());
            configScript.EmitSysCall(0x62e1b114);
            txHash = SendTx(configScript.ToArray(), signers, keypair);
            Console.WriteLine($"SetAgentConfig TX: {txHash}");

            // Finalize config
            Console.WriteLine("\nStep 3: Finalizing configuration...");
            var finalizeScript = new ScriptBuilder();
            finalizeScript.EmitPush(System.Text.Encoding.UTF8.GetBytes("finalizeConfig"));
            finalizeScript.EmitPush(trustAnchor.GetSpan());
            finalizeScript.EmitSysCall(0x62e1b114);
            txHash = SendTx(finalizeScript.ToArray(), signers, keypair);
            Console.WriteLine($"FinalizeConfig TX: {txHash}");

            Console.WriteLine($"\n=== Agent {agentIndex} Configuration Complete ===");
        }

        static UInt256 SendTx(byte[] script, Signer[] signers, KeyPair keypair)
        {
            var txMgr = factory.MakeTransactionAsync(script, signers).GetAwaiter().GetResult();
            var signedTx = txMgr.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            return CLI.SendRawTransactionAsync(signedTx).GetAwaiter().GetResult();
        }
    }
}
