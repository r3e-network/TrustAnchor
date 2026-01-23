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

namespace TrustAnchorVote
{
    class Program
    {
        private const string DefaultTrustAnchor = "0x48c40d4666f93408be1bef038b6722404d9a4c2a";
        private static readonly BigInteger I = BigInteger.Parse(Environment.GetEnvironmentVariable("I"));
        private static readonly ECPoint TARGET = ECPoint.Parse(Environment.GetEnvironmentVariable("TARGET"), ECCurve.Secp256r1);

        static void Main(string[] args)
        {
            var trustAnchorHash = Environment.GetEnvironmentVariable("TRUSTANCHOR") ?? DefaultTrustAnchor;
            UInt160 trustAnchor = UInt160.Parse(trustAnchorHash);
            $"VOTE: {I} -> {TARGET}".Log();
            Byte[] SCRIPT = trustAnchor.MakeScript("trigVote", I, TARGET).ToArray();
            SCRIPT.ToHexString().Out();
            SCRIPT.ToArray().SendTx().Out();;
        }
    }
}
