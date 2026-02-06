using System;
using System.Numerics;
using Neo;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace ConfigureAgent
{
    class Program
    {
        internal const string UpdateTargetMethod = "updateAgentTargetById";
        internal const string UpdateNameMethod = "updateAgentNameById";
        internal const string SetVotingMethod = "setAgentVotingById";

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

        internal readonly record struct ParsedInputs(
            string Wif,
            string TrustAnchorHash,
            int AgentIndex,
            byte[] VoteTargetBytes,
            string Name,
            BigInteger VotingAmount);

        internal static ParsedInputs ParseInputs(
            string[] args,
            string? envWif,
            string? envTrustAnchor,
            string? envVoteTarget)
        {
            var wif = args.Length > 0 ? args[0] : envWif;
            if (string.IsNullOrWhiteSpace(wif))
            {
                throw new InvalidOperationException("WIF is required. Set WIF env var or pass as first argument.");
            }

            var trustAnchorHash = args.Length > 1 ? args[1] : envTrustAnchor;
            if (string.IsNullOrWhiteSpace(trustAnchorHash))
            {
                throw new InvalidOperationException("TRUSTANCHOR is required. Set TRUSTANCHOR env var or pass as second argument.");
            }

            var agentIndex = args.Length > 2 ? int.Parse(args[2]) : 0;
            if (agentIndex < 0)
            {
                throw new InvalidOperationException("agentIndex must be >= 0.");
            }

            var voteTarget = args.Length > 3 ? args[3] : envVoteTarget;
            if (string.IsNullOrWhiteSpace(voteTarget))
            {
                throw new InvalidOperationException("VOTE_TARGET is required. Set VOTE_TARGET env var or pass as fourth argument.");
            }

            if (voteTarget.Length != 66)
            {
                throw new InvalidOperationException("VOTE_TARGET must be 33 bytes (66 hex characters).");
            }

            byte[] voteBytes;
            try
            {
                voteBytes = Convert.FromHexString(voteTarget);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("VOTE_TARGET must be valid hex.", ex);
            }

            var name = args.Length > 4 ? args[4] : $"agent-{agentIndex}";
            var votingAmount = args.Length > 5 ? BigInteger.Parse(args[5]) : BigInteger.One;
            if (votingAmount < 0)
            {
                throw new InvalidOperationException("votingAmount must be >= 0.");
            }

            return new ParsedInputs(wif, trustAnchorHash, agentIndex, voteBytes, name, votingAmount);
        }

        static void Main(string[] args)
        {
            var inputs = ParseInputs(
                args,
                Environment.GetEnvironmentVariable("WIF"),
                Environment.GetEnvironmentVariable("TRUSTANCHOR"),
                Environment.GetEnvironmentVariable("VOTE_TARGET"));

            Console.WriteLine($"=== Configuring Agent {inputs.AgentIndex} ===");
            Console.WriteLine($"TrustAnchor: {inputs.TrustAnchorHash}");
            Console.WriteLine($"Vote Target: {Convert.ToHexString(inputs.VoteTargetBytes)}");
            Console.WriteLine($"Name: {inputs.Name}");
            Console.WriteLine($"Voting Amount: {inputs.VotingAmount}");

            var keypair = Neo.Network.RPC.Utility.GetKeyPair(inputs.Wif);
            var deployer = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            
            var signers = new[] 
            { 
                new Signer 
                { 
                    Scopes = WitnessScope.CalledByEntry,
                    Account = deployer 
                } 
            };

            var trustAnchor = UInt160.Parse(inputs.TrustAnchorHash);

            Console.WriteLine($"\nDeployer: {deployer}");

            // Update target
            Console.WriteLine($"\nStep 1: Updating Agent {inputs.AgentIndex} target...");
            var targetScript = new ScriptBuilder();
            targetScript.EmitDynamicCall(trustAnchor, UpdateTargetMethod, new BigInteger(inputs.AgentIndex), inputs.VoteTargetBytes);
            var txHash = SendTx(targetScript.ToArray(), signers, keypair);
            Console.WriteLine($"UpdateAgentTargetById TX: {txHash}");

            // Update name
            Console.WriteLine($"\nStep 2: Updating Agent {inputs.AgentIndex} name...");
            var nameScript = new ScriptBuilder();
            nameScript.EmitDynamicCall(trustAnchor, UpdateNameMethod, new BigInteger(inputs.AgentIndex), inputs.Name);
            txHash = SendTx(nameScript.ToArray(), signers, keypair);
            Console.WriteLine($"UpdateAgentNameById TX: {txHash}");

            // Set voting amount
            Console.WriteLine($"\nStep 3: Setting Agent {inputs.AgentIndex} voting amount...");
            var votingScript = new ScriptBuilder();
            votingScript.EmitDynamicCall(trustAnchor, SetVotingMethod, new BigInteger(inputs.AgentIndex), inputs.VotingAmount);
            txHash = SendTx(votingScript.ToArray(), signers, keypair);
            Console.WriteLine($"SetAgentVotingById TX: {txHash}");

            Console.WriteLine($"\n=== Agent {inputs.AgentIndex} Configuration Complete ===");
        }

        static UInt256 SendTx(byte[] script, Signer[] signers, KeyPair keypair)
        {
            var txMgr = factory.MakeTransactionAsync(script, signers).GetAwaiter().GetResult();
            var signedTx = txMgr.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            return CLI.SendRawTransactionAsync(signedTx).GetAwaiter().GetResult();
        }
    }
}
