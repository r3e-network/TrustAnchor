# ConfigureAgent Validation and Warning Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add explicit ConfigureAgent input validation with tests, and clean existing warnings without changing contract behavior.

**Architecture:** Add a small internal input parser in ConfigureAgent, exposed to tests via InternalsVisibleTo, and keep transaction flow unchanged. Warning cleanup is a refactor-only pass after tests are green.

**Tech Stack:** .NET (C#), xUnit, Neo SDK.

### Task 1: Add failing tests for ConfigureAgent input parsing

**Files:**
- Create: `TrustAnchor/TrustAnchorOps.Tests/ConfigureAgentInputTests.cs`
- Modify: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj` (if needed for new file)
- Modify: `TrustAnchor/ConfigureAgent/ConfigureAgent.csproj` (InternalsVisibleTo)

**Step 1: Write the failing tests**

```csharp
using System;
using System.Numerics;
using ConfigureAgent;
using Xunit;

namespace TrustAnchorOps.Tests
{
    public class ConfigureAgentInputTests
    {
        [Fact]
        public void ParseInputs_requires_wif()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(Array.Empty<string>(), null, null, null));
            Assert.Contains("WIF", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_requires_trustanchor()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(new[] { "wif" }, null, null, null));
            Assert.Contains("TRUSTANCHOR", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_requires_vote_target()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(new[] { "wif", "0xabcdef" }, null, null, null));
            Assert.Contains("VOTE_TARGET", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_rejects_invalid_vote_target_hex()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(new[] { "wif", "0xabcdef", "0", "zz" }, null, null, null));
            Assert.Contains("hex", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_rejects_invalid_vote_target_length()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(new[] { "wif", "0xabcdef", "0", "aa" }, null, null, null));
            Assert.Contains("33 bytes", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_rejects_negative_agent_index()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(new[] { "wif", "0xabcdef", "-1", new string('a', 66) }, null, null, null));
            Assert.Contains("agent", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_rejects_negative_voting_amount()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Program.ParseInputs(new[] { "wif", "0xabcdef", "0", new string('a', 66), "agent", "-1" }, null, null, null));
            Assert.Contains("voting", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ParseInputs_parses_happy_path()
        {
            var input = Program.ParseInputs(
                new[] { "wif", "0xabcdef", "1", new string('a', 66), "agent-1", "2" },
                null,
                null,
                null);

            Assert.Equal("wif", input.Wif);
            Assert.Equal("0xabcdef", input.TrustAnchorHash);
            Assert.Equal(1, input.AgentIndex);
            Assert.Equal(33, input.VoteTargetBytes.Length);
            Assert.Equal("agent-1", input.Name);
            Assert.Equal(new BigInteger(2), input.VotingAmount);
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
Expected: FAIL due to missing `Program.ParseInputs` and internals access.

**Step 3: Add InternalsVisibleTo to ConfigureAgent**

```xml
<ItemGroup>
  <InternalsVisibleTo Include="TrustAnchorOps.Tests" />
</ItemGroup>
```

**Step 4: Re-run test to verify it still fails**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
Expected: FAIL because `ParseInputs` not implemented yet.

**Step 5: Commit**

```bash
git add TrustAnchor/TrustAnchorOps.Tests/ConfigureAgentInputTests.cs TrustAnchor/ConfigureAgent/ConfigureAgent.csproj
git commit -m "test: add ConfigureAgent input validation cases"
```

### Task 2: Implement minimal ConfigureAgent input parsing

**Files:**
- Modify: `TrustAnchor/ConfigureAgent/Program.cs`

**Step 1: Write minimal implementation**

```csharp
internal readonly record struct ParsedInputs(
    string Wif,
    string TrustAnchorHash,
    int AgentIndex,
    byte[] VoteTargetBytes,
    string Name,
    BigInteger VotingAmount);

internal static ParsedInputs ParseInputs(
    string[] args,
    string? envWif,
    string? envTrustAnchor,
    string? envVoteTarget)
{
    var wif = args.Length > 0 ? args[0] : envWif;
    if (string.IsNullOrWhiteSpace(wif))
        throw new InvalidOperationException("WIF is required. Set WIF env var or pass as first argument.");

    var trustAnchorHash = args.Length > 1 ? args[1] : envTrustAnchor;
    if (string.IsNullOrWhiteSpace(trustAnchorHash))
        throw new InvalidOperationException("TRUSTANCHOR is required. Set TRUSTANCHOR env var or pass as second argument.");

    var agentIndex = args.Length > 2 ? int.Parse(args[2]) : 0;
    if (agentIndex < 0)
        throw new InvalidOperationException("agentIndex must be >= 0.");

    var voteTarget = args.Length > 3 ? args[3] : envVoteTarget;
    if (string.IsNullOrWhiteSpace(voteTarget))
        throw new InvalidOperationException("VOTE_TARGET is required. Set VOTE_TARGET env var or pass as fourth argument.");

    if (voteTarget.Length != 66)
        throw new InvalidOperationException("VOTE_TARGET must be 33 bytes (66 hex characters).");

    byte[] voteBytes;
    try
    {
        voteBytes = Convert.FromHexString(voteTarget);
    }
    catch (FormatException ex)
    {
        throw new InvalidOperationException("VOTE_TARGET must be valid hex.", ex);
    }

    var name = args.Length > 4 ? args[4] : $"agent-{agentIndex}";
    var votingAmount = args.Length > 5 ? BigInteger.Parse(args[5]) : BigInteger.One;
    if (votingAmount < 0)
        throw new InvalidOperationException("votingAmount must be >= 0.");

    return new ParsedInputs(wif, trustAnchorHash, agentIndex, voteBytes, name, votingAmount);
}
```

**Step 2: Wire `Main` to use ParseInputs**

```csharp
var inputs = ParseInputs(args, Environment.GetEnvironmentVariable("WIF"), Environment.GetEnvironmentVariable("TRUSTANCHOR"), Environment.GetEnvironmentVariable("VOTE_TARGET"));
```

Use `inputs.*` throughout `Main` and remove inline parsing.

**Step 3: Run tests to verify they pass**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
Expected: PASS.

**Step 4: Commit**

```bash
git add TrustAnchor/ConfigureAgent/Program.cs
git commit -m "feat: validate ConfigureAgent inputs"
```

### Task 3: Warning cleanup (behavior-preserving refactor)

**Files:**
- Modify: `contract/TrustAnchor.cs`
- Modify: `contract/TrustAnchor.Tests/TestContracts.cs`
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Make storage reads null-safe**

```csharp
var data = new StorageMap(Storage.CurrentContext, PREFIXAGENT_TARGET).Get((ByteString)index);
return data is null ? default : (ECPoint)(byte[])data;
```

```csharp
var current = targetMap.Get((ByteString)index);
```

**Step 2: Fix test warnings**

- Initialize `AuthAgentHash` to `UInt160.Zero` or mark it nullable.
- Pass a non-null payload to `Invoke(TrustHash, "onNEP17Payment", from, amount, new byte[0])`.
- Replace `Assert.Equal(1, list.Count)` with `Assert.Single(list)`.

**Step 3: Run tests to verify pass**

Run:
- `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
- `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`

Expected: PASS and warnings reduced.

**Step 4: Commit**

```bash
git add contract/TrustAnchor.cs contract/TrustAnchor.Tests/TestContracts.cs contract/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "chore: clean up warnings"
```
