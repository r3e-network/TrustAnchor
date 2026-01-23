using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibHelper;
using LibRPC;
using LibWallet;
using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract.Native;
using Neo.VM;

namespace TrustAnchorStrategist
{
    class Program
    {
        private const string DefaultTrustAnchor = "0x48c40d4666f93408be1bef038b6722404d9a4c2a";

        static void Main(string[] args)
        {
            var trustAnchorHash = Environment.GetEnvironmentVariable("TRUSTANCHOR") ?? DefaultTrustAnchor;
            var configPath = Environment.GetEnvironmentVariable("VOTE_CONFIG");
            if (string.IsNullOrWhiteSpace(configPath))
                throw new Exception("VOTE_CONFIG is required");

            UInt160 trustAnchor = UInt160.Parse(trustAnchorHash);
            VoteConfig config = VoteConfig.Load(configPath);

            List<UInt160> agents = Enumerable.Range(0, 21)
                .Select(v => trustAnchor.MakeScript("agent", v))
                .SelectMany(a => a)
                .ToArray()
                .Call()
                .TakeWhile(v => v.IsNull == false)
                .Select(v => v.ToU160())
                .ToList();

            if (agents.Count == 0)
            {
                "NO AGENTS".Log();
                return;
            }

            if (config.Candidates.Count != agents.Count)
                throw new Exception("Candidate count must match agent count");

            List<ECPoint> candidates = config.Candidates
                .Select(c => ECPoint.Parse(c.PubKey, ECCurve.Secp256r1))
                .ToList();

            foreach (ECPoint candidate in candidates)
            {
                var allowed = trustAnchor.MakeScript("candidate", candidate).Call().Single();
                if (allowed.IsNull)
                    throw new Exception($"Candidate not whitelisted: {candidate}");
            }

            List<Neo.VM.Types.StackItem> agentStates = agents
                .Select(v => NativeContract.NEO.Hash.MakeScript("getAccountState", v))
                .SelectMany(a => a)
                .ToArray()
                .Call()
                .ToList();

            if (agentStates.Count != agents.Count)
                throw new Exception("Agent state count mismatch");

            List<byte[]> agentTo = new();
            List<BigInteger> agentHold = new();
            foreach (var item in agentStates)
            {
                if (item.IsNull)
                {
                    agentTo.Add(Array.Empty<byte>());
                    agentHold.Add(BigInteger.Zero);
                    continue;
                }

                var state = item.ToVMStruct();
                agentTo.Add(state[2].ToBytes());
                agentHold.Add(state.First().GetInteger());
            }

            BigInteger totalPower = agentHold.Sum();
            List<int> weights = config.Candidates.Select(c => c.Weight).ToList();
            List<BigInteger> selectHold = VotePlanner.AssignTargets(agents.Count, VoteAllocator.ComputeTargets(totalPower, weights)).ToList();

            $"TRUSTANCHOR: {trustAnchor}".Log();
            $"AGENTS: {String.Join(", ", agents)}".Log();
            $"CANDIDATES: {String.Join(", ", candidates.Select(v => v.ToString()))}".Log();
            $"WEIGHTS: {String.Join(", ", weights)}".Log();
            $"AGENT_TO: {String.Join(", ", agentTo.Select(v => v.ToHexString()))}".Log();
            $"AGENT_HOLD: {String.Join(", ", agentHold)}".Log();
            $"TARGET_HOLD: {String.Join(", ", selectHold)}".Log();

            List<byte[]> scriptVotes = new();
            for (var i = 0; i < agents.Count; i++)
            {
                var desired = candidates[i];
                var desiredBytes = desired.EncodePoint(true);
                var current = agentTo[i];
                if (current.SequenceEqual(desiredBytes))
                    continue;
                scriptVotes.Add(trustAnchor.MakeScript("trigVote", i, desired));
            }
            $"SCRIPTVOTES: {String.Join(", ", scriptVotes.Select(v => v.ToHexString()))}".Log();

            List<BigInteger> agentDiff = selectHold.Zip(agentHold).Select(v => v.First - v.Second).ToList();
            $"AGENT_DIFF: {String.Join(", ", agentDiff)}".Log();

            (List<int> transfers, List<BigInteger> transferAmount) = agentDiff
                .Select((v, i) => (v, i))
                .Where(v => v.v.IsZero == false)
                .OrderBy(v => v.v)
                .Map2(v => v.i, v => v.v);

            $"TRANSFERS: {String.Join(", ", transfers)}".Log();
            $"TRANSFER_AMOUNT: {String.Join(", ", transferAmount)}".Log();

            List<byte[]> scriptTransfers = new();
            if (transferAmount.Count > 0)
            {
                List<BigInteger> actions = transferAmount.Aggregate(
                    (BigInteger.Zero, Enumerable.Empty<BigInteger>()),
                    (stack, v) => (stack.Item1 - v, stack.Item2.Append(stack.Item1 - v))
                ).Item2.ToList();

                if (actions.Last() != 0) throw new Exception();

                List<BigInteger> actionList = actions.SkipLast(1).ToList();
                scriptTransfers = actionList
                    .Select((v, i) => trustAnchor.MakeScript("trigTransfer", transfers[i], transfers[i + 1], v))
                    .ToList();
            }

            $"SCRIPTTRANSFERS: {String.Join(", ", scriptTransfers.Select(v => v.ToHexString()))}".Log();

            if (scriptVotes.Count == 0 && scriptTransfers.Count == 0)
            {
                "NO ACTIONS".Log();
                return;
            }

            scriptVotes.Concat(scriptTransfers).SelectMany(a => a).ToArray().SendTx().Out();
        }
    }
}
