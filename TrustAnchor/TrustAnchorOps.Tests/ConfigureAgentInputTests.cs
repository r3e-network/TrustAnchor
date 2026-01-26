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
