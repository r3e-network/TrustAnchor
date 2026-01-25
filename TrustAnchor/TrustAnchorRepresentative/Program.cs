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
        private static readonly BigInteger THRESHOLD = ParseThreshold();

        static BigInteger ParseThreshold()
        {
            var envValue = Environment.GetEnvironmentVariable("THRESHOLD");
            if (string.IsNullOrWhiteSpace(envValue))
            {
                throw new InvalidOperationException("THRESHOLD environment variable is required and must be a valid integer.");
            }
            if (!BigInteger.TryParse(envValue, out var result))
            {
                throw new InvalidOperationException($"THRESHOLD environment variable '{envValue}' is not a valid BigInteger.");
            }
            if (result <= 0)
            {
                throw new InvalidOperationException("THRESHOLD must be a positive value.");
            }
            return result;
        }

        static void Main(string[] args)
        {
            UInt160 target = GetTarget();
            UInt160 REPRESENTATIVE = GetRepresentative();
            BigInteger BLOCKNUM = NativeContract.Ledger.Hash.MakeScript("currentIndex").Call().Single().GetInteger();
            BigInteger GASBALANCE = NativeContract.GAS.Hash.MakeScript("balanceOf", new object[]{ REPRESENTATIVE }).Call().Single().GetInteger();

            $"GASBALANCE: {GASBALANCE}".Log();

            if (GASBALANCE < THRESHOLD || GASBALANCE < 1_0000_0000) {
                $"GASBALANCE < THRESHOLD: {GASBALANCE} < {THRESHOLD} || GASBALANCE < 1_0000_0000".Log();
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

        private static UInt160 GetRepresentative()
        {
            var repHash = Environment.GetEnvironmentVariable("REPRESENTATIVE");
            if (string.IsNullOrWhiteSpace(repHash))
            {
                throw new InvalidOperationException("REPRESENTATIVE env var is required.");
            }
            return UInt160.Parse(repHash);
        }
    }
}
