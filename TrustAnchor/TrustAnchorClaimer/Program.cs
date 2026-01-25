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

namespace TrustAnchorClaimer
{
    class Program
    {
        private static readonly BigInteger THRESHOLD = ParseThreshold();
        private static readonly uint MOD = ParseMod();

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

        static uint ParseMod()
        {
            var envValue = Environment.GetEnvironmentVariable("MOD");
            if (string.IsNullOrWhiteSpace(envValue))
            {
                return 1;
            }
            if (!uint.TryParse(envValue, out var result))
            {
                throw new InvalidOperationException($"MOD environment variable '{envValue}' is not a valid unsigned integer.");
            }
            return result;
        }

        static void Main(string[] args)
        {
            uint CHOSEN = (uint)((DateTime.UtcNow.ToTimestamp() / 3600) % MOD);
            UInt160 trustAnchor = GetTrustAnchor();
            BigInteger BLOCKNUM = NativeContract.Ledger.Hash.MakeScript("currentIndex").Call().Single().GetInteger();
            List<UInt160> AGENTS = Enumerable.Range(0, 22).Where(v => v % MOD == CHOSEN).Select(v => trustAnchor.MakeScript("agent", v)).SelectMany(a => a).ToArray().Call().Where(v => v.IsNull == false).Select(v => v.ToU160()).ToList();
            $"CHOSEN: {CHOSEN}, MOD: {MOD}".Log();
            $"BLOCKNUM: {BLOCKNUM}".Log();
            $"AGENTS: {String.Join(", ", AGENTS)}".Log();

            List<BigInteger> UNCLAIMED = AGENTS.Select(v => (BigInteger)v.GetUnclaimedGas()).ToList();
            List<BigInteger> GASBALANCE = AGENTS.Select(v => NativeContract.GAS.Hash.MakeScript("balanceOf", v).Call().Single().GetInteger()).ToList();
            List<BigInteger> MERGED = UNCLAIMED.Zip(GASBALANCE).Select(v => v.First > THRESHOLD ? v.First + v.Second : v.Second).ToList();
            $"UNCLAIMED: {String.Join(", ", UNCLAIMED)}".Log();
            $"GASBALANCE: {String.Join(", ", GASBALANCE)}".Log();
            $"MERGED: {String.Join(", ", MERGED)}".Log();

            List<byte[]> SYNC = AGENTS.Zip(UNCLAIMED).Where(v => v.Second > THRESHOLD).Select(v => v.First.MakeScript("sync")).ToList();
            List<byte[]> CLAIM = AGENTS.Zip(MERGED).Where(v => v.Second > THRESHOLD).Select(v => v.First.MakeScript("claim")).ToList();
            $"SYNC: {String.Join(", ", SYNC.Select(v => v.ToHexString()))}".Log();
            $"CLAIM: {String.Join(", ", CLAIM.Select(v => v.ToHexString()))}".Log();
            SYNC.Concat(CLAIM).SelectMany(v => v).Nullize()?.ToArray().SendTx().Out();
        }

        private static UInt160 GetTrustAnchor()
        {
            var trustAnchorHash = Environment.GetEnvironmentVariable("TRUSTANCHOR");
            if (string.IsNullOrWhiteSpace(trustAnchorHash))
            {
                throw new InvalidOperationException("TRUSTANCHOR env var is required.");
            }
            return UInt160.Parse(trustAnchorHash);
        }
    }
}
