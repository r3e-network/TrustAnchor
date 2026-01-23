using System;
using System.Numerics;
using System.Linq;
using Neo;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Collections.Generic;
using LibHelper;
using LibRPC;
using LibWallet;
using Neo.Cryptography.ECC;

namespace BurgerTransfer
{
    class Program
    {
        private const string DefaultTrustAnchor = "0x48c40d4666f93408be1bef038b6722404d9a4c2a";
        private static readonly BigInteger FROM = BigInteger.Parse(Environment.GetEnvironmentVariable("FROM"));
        private static readonly BigInteger TO = BigInteger.Parse(Environment.GetEnvironmentVariable("TO"));
        private static readonly BigInteger AMOUNT = BigInteger.Parse(Environment.GetEnvironmentVariable("AMOUNT"));

        static void Main(string[] args)
        {
            var trustAnchorHash = Environment.GetEnvironmentVariable("TRUSTANCHOR") ?? DefaultTrustAnchor;
            UInt160 trustAnchor = UInt160.Parse(trustAnchorHash);
            $"TRANSFER: {FROM} -> {TO}, AMOUNT = {AMOUNT}".Log();
            Byte[] SCRIPT = trustAnchor.MakeScript("trigTransfer", FROM, TO, AMOUNT).ToArray();
            SCRIPT.ToHexString().Out();
            SCRIPT.ToArray().SendTx().Out();;
        }
    }
}
