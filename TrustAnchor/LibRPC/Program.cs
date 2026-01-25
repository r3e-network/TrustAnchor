using System;
using System.Linq;
using LibHelper;
using Neo;
using Neo.Cryptography;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;

namespace LibRPC
{
    public static class Program
    {
        private static readonly string RPC = Environment.GetEnvironmentVariable("RPC") ?? "https://testnet1.neo.coz.io:443";
        private static readonly Uri URI = new(RPC);
        
        // Configure for N3 testnet (network: 894710606)
        private static readonly ProtocolSettings settings = new ProtocolSettings
        {
            AddressVersion = 53,
            Network = 894710606,  // Testnet network ID
            MillisecondsPerBlock = 3000,
            MaxTraceableBlocks = 2102400,
            MaxValidUntilBlockIncrement = 5760,
            MaxTransactionsPerBlock = 5000,
            MemoryPoolMaxTransactions = 50000,
            ValidatorsCount = 7
        };
        
        private static readonly RpcClient CLI = new(URI, null, null, settings);
        private static readonly TransactionManagerFactory factory = new(CLI);
        static void Main(string[] args)
        {
            Call(new byte[] { ((byte)OpCode.RET) });
            "OK!".Log();
        }
        public static StackItem[] Call(this byte[] script)
        {
            RpcInvokeResult result = CLI.InvokeScriptAsync(script).GetAwaiter().GetResult();
            if (result.State != VMState.HALT)
            {
                throw new Exception();
            }
            return result.Stack;
        }
        public static long GetUnclaimedGas(this string address) => CLI.GetUnclaimedGasAsync(address).GetAwaiter().GetResult().Unclaimed;
        public static long GetUnclaimedGas(this UInt160 account) => GetUnclaimedGas(account.ToAddress(settings.AddressVersion));
        public static UInt256 Send(this Transaction tx) => CLI.SendRawTransactionAsync(tx).GetAwaiter().GetResult();
        public static TransactionManager TxMgr(this byte[] script, Signer[] signers = null) => factory.MakeTransactionAsync(script, signers).GetAwaiter().GetResult();

        // Standard signing method - no workaround
        public static Transaction SignAndSend(this byte[] script, Signer[] signers, KeyPair keypair)
        {
            var txMgr = script.TxMgr(signers);
            var signedTx = txMgr.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();

            $"Transaction: {signedTx.Hash}, NetworkFee: {signedTx.NetworkFee}, SystemFee: {signedTx.SystemFee}".Log();

            return signedTx;
        }
    }
}
