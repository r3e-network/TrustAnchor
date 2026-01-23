using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TrustAnchorStrategist;

public sealed record VoteCandidate(string PubKey, int Weight);

public sealed record VoteConfig(IReadOnlyList<VoteCandidate> Candidates)
{
    public int TotalWeight => Candidates.Sum(c => c.Weight);

    public static VoteConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<VoteConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Invalid config");

        cfg.Validate();
        return cfg;
    }

    public void Validate()
    {
        if (Candidates.Count == 0) throw new InvalidOperationException("No candidates");
        if (Candidates.Any(c => c.Weight <= 0)) throw new InvalidOperationException("Weight must be > 0");
        if (TotalWeight != 21) throw new InvalidOperationException("Weight sum must be 21");
        if (Candidates.Select(c => c.PubKey).Distinct().Count() != Candidates.Count)
            throw new InvalidOperationException("Duplicate pubkey");
    }
}
