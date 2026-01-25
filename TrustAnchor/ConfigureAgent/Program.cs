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
            int agentIndex = args.Length > 2 ? int.Parse(args[2]) : 0;
            string voteTarget = args.Length > 3 ? args[3] : Environment.GetEnvironmentVariable("VOTE_TARGET");
            string name = args.Length > 4 ? args[4] : $"agent-{agentIndex}";
            BigInteger votingAmount = args.Length > 5 ? BigInteger.Parse(args[5]) : BigInteger.One;

            Console.WriteLine($"=== Configuring Agent {agentIndex} ===");
            Console.WriteLine($"TrustAnchor: {trustAnchorHash}");
            Console.WriteLine($"Vote Target: {voteTarget}");
            Console.WriteLine($"Name: {name}");
            Console.WriteLine($"Voting Amount: {votingAmount}");

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

            // Update target
            Console.WriteLine($"\nStep 1: Updating Agent {agentIndex} target...");
            var targetScript = new ScriptBuilder();
            targetScript.EmitPush(voteBytes);
            targetScript.EmitPush(new BigInteger(agentIndex));
            targetScript.EmitPush(System.Text.Encoding.UTF8.GetBytes("updateAgentTargetById"));
            targetScript.EmitPush(trustAnchor.GetSpan());
            targetScript.EmitSysCall(0x62e1b114);
            var txHash = SendTx(targetScript.ToArray(), signers, keypair);
            Console.WriteLine($"UpdateAgentTargetById TX: {txHash}");

            // Update name
            Console.WriteLine($"\nStep 2: Updating Agent {agentIndex} name...");
            var nameScript = new ScriptBuilder();
            nameScript.EmitPush(System.Text.Encoding.UTF8.GetBytes(name));
            nameScript.EmitPush(new BigInteger(agentIndex));
            nameScript.EmitPush(System.Text.Encoding.UTF8.GetBytes("updateAgentNameById"));
            nameScript.EmitPush(trustAnchor.GetSpan());
            nameScript.EmitSysCall(0x62e1b114);
            txHash = SendTx(nameScript.ToArray(), signers, keypair);
            Console.WriteLine($"UpdateAgentNameById TX: {txHash}");

            // Set voting amount
            Console.WriteLine($"\nStep 3: Setting Agent {agentIndex} voting amount...");
            var votingScript = new ScriptBuilder();
            votingScript.EmitPush(votingAmount);
            votingScript.EmitPush(new BigInteger(agentIndex));
            votingScript.EmitPush(System.Text.Encoding.UTF8.GetBytes("setAgentVotingById"));
            votingScript.EmitPush(trustAnchor.GetSpan());
            votingScript.EmitSysCall(0x62e1b114);
            txHash = SendTx(votingScript.ToArray(), signers, keypair);
            Console.WriteLine($"SetAgentVotingById TX: {txHash}");

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
