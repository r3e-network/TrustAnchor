using System;
using System.Linq;
using System.Numerics;
using LibRPC;
using Neo;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace StakeNEO
{
    class Program
    {
        internal const string StakeOfMethodName = "stakeOf";
        internal const string TransferMethodName = "transfer";
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
            BigInteger amount = args.Length > 2 ? BigInteger.Parse(args[2]) : new BigInteger(100);

            if (string.IsNullOrWhiteSpace(wif))
            {
                throw new InvalidOperationException("WIF is required. Set WIF env var or pass as first argument.");
            }
            if (string.IsNullOrWhiteSpace(trustAnchorHash))
            {
                throw new InvalidOperationException("TRUSTANCHOR is required. Set TRUSTANCHOR env var or pass as second argument.");
            }
            if (amount <= 0)
            {
                throw new InvalidOperationException("Amount must be a positive integer.");
            }

            Console.WriteLine($"=== Staking NEO ===");
            Console.WriteLine($"TrustAnchor: {trustAnchorHash}");
            Console.WriteLine($"Amount: {amount} NEO");

            var keypair = Neo.Network.RPC.Utility.GetKeyPair(wif);
            var user = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            var trustAnchor = UInt160.Parse(trustAnchorHash);

            Console.WriteLine($"\nUser: {user}");

            // Check balances before
            Console.WriteLine("\nChecking balances before staking...");
            var neoBefore = NativeContract.NEO.Hash.MakeScript("balanceOf", user).Call().Single().GetInteger();
            var gasBefore = NativeContract.GAS.Hash.MakeScript("balanceOf", user).Call().Single().GetInteger();
            Console.WriteLine($"NEO Balance: {neoBefore}");
            Console.WriteLine($"GAS Balance: {gasBefore}");

            // Transfer NEO to TrustAnchor (this stakes it)
            Console.WriteLine($"\nStaking {amount} NEO to TrustAnchor...");
            var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.NEO.Hash, TransferMethodName, user, trustAnchor, amount, null);

            var signers = new[] 
            { 
                new Signer 
                { 
                    Scopes = WitnessScope.CalledByEntry, 
                    Account = user 
                } 
            };

            var txHash = SendTx(script.ToArray(), signers, keypair);
            Console.WriteLine($"Stake TX: {txHash}");

            // Check balances after
            Console.WriteLine("\nChecking balances after staking...");
            var neoAfter = NativeContract.NEO.Hash.MakeScript("balanceOf", user).Call().Single().GetInteger();
            var gasAfter = NativeContract.GAS.Hash.MakeScript("balanceOf", user).Call().Single().GetInteger();
            Console.WriteLine($"NEO Balance: {neoAfter}");
            Console.WriteLine($"GAS Balance: {gasAfter}");

            // Check TrustAnchor stake
            Console.WriteLine("\nChecking TrustAnchor stake...");
            var totalStake = trustAnchor.MakeScript("totalStake").Call().Single().GetInteger();
            var userStake = trustAnchor.MakeScript(StakeOfMethodName, user).Call().Single().GetInteger();
            Console.WriteLine($"Total Stake: {totalStake}");
            Console.WriteLine($"User Stake: {userStake}");

            Console.WriteLine($"\n=== Staking Complete ===");
        }

        static UInt256 SendTx(byte[] script, Signer[] signers, KeyPair keypair)
        {
            var txMgr = factory.MakeTransactionAsync(script, signers).GetAwaiter().GetResult();
            var signedTx = txMgr.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            return CLI.SendRawTransactionAsync(signedTx).GetAwaiter().GetResult();
        }
    }
}
