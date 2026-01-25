using System;
using LibHelper;
using LibRPC;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;

namespace LibWallet
{
    public static class Program
    {
        private static (KeyPair keypair, UInt160 contract, Signer[] signers) GetWallet()
        {
            var wif = Environment.GetEnvironmentVariable("WIF");
            if (string.IsNullOrWhiteSpace(wif))
            {
                throw new InvalidOperationException("WIF environment variable is required.");
            }

            var keypair = Neo.Network.RPC.Utility.GetKeyPair(wif);
            var contract = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = contract } };
            return (keypair, contract, signers);
        }
        static void Main(string[] args)
        {
            var (_, contract, _) = GetWallet();
            contract.Out();
            string SCRIPT = Environment.GetEnvironmentVariable("SCRIPT");
            if (SCRIPT is null) return;
            Convert.FromHexString(SCRIPT).SendTx().Out();
        }
        public static UInt256 SendTx(this byte[] script)
        {
            var (keypair, _, signers) = GetWallet();
            return script.TxMgr(signers).AddSignature(keypair).SignAsync().GetAwaiter().GetResult().Send();
        }
    }
}
