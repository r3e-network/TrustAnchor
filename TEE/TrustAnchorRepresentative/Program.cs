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

namespace TrustAnchorRepresentative
{
    class Program
    {
        private static readonly BigInteger THREASHOLD = BigInteger.Parse(Environment.GetEnvironmentVariable("THREASHOLD"));

        static void Main(string[] args)
        {
            UInt160 target = GetTarget();
            UInt160 REPRESENTATIVE = UInt160.Parse("0x329aeff39c13550337f02296d5ffc82583acaba3");
            BigInteger BLOCKNUM = NativeContract.Ledger.Hash.MakeScript("currentIndex").Call().Single().GetInteger();
            BigInteger GASBALANCE = NativeContract.GAS.Hash.MakeScript("balanceOf", new object[]{ REPRESENTATIVE }).Call().Single().GetInteger();

            $"GASBALANCE: {GASBALANCE}".Log();

            if (GASBALANCE < THREASHOLD || GASBALANCE < 1_0000_0000) {
                $"GASBALANCE < THREASHOLD: {GASBALANCE} < {THREASHOLD} || GASBALANCE < 1_0000_0000".Log();
                return;
            }

            byte[] REWARD = NativeContract.GAS.Hash.MakeScript("transfer", new object[]{ REPRESENTATIVE, target, GASBALANCE-1_0000_0000, "reward"});

            REWARD.SendTx().Out();
        }

        private static UInt160 GetTarget()
        {
            var targetHash = Environment.GetEnvironmentVariable("TARGET");
            var trustAnchorHash = Environment.GetEnvironmentVariable("TRUSTANCHOR");
            var selected = string.IsNullOrWhiteSpace(targetHash) ? trustAnchorHash : targetHash;
            if (string.IsNullOrWhiteSpace(selected))
            {
                throw new InvalidOperationException("TARGET or TRUSTANCHOR env var is required.");
            }
            return UInt160.Parse(selected);
        }
    }
}
