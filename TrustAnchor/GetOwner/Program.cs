using System;
using System.Linq;
using LibHelper;
using LibRPC;
using Neo;
using Neo.Wallets;

namespace GetOwner
{
    class Program
    {
        private const byte AddressVersion = 53;

        static void Main(string[] args)
        {
            string trustAnchorHash = args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable("TRUSTANCHOR");

            if (string.IsNullOrWhiteSpace(trustAnchorHash))
            {
                throw new InvalidOperationException("TRUSTANCHOR is required. Set TRUSTANCHOR env var or pass as first argument.");
            }

            var trustAnchor = UInt160.Parse(trustAnchorHash);
            var owner = trustAnchor.MakeScript("owner").Call().Single().ToU160();

            Console.WriteLine($"TrustAnchor: {trustAnchor}");
            Console.WriteLine($"Owner: {owner}");
            Console.WriteLine($"Owner Address: {owner.ToAddress(AddressVersion)}");
        }
    }
}
