using System.Text.Json;
using Kurx.Domain.Entities;
using Kurx.Domain.Enums;

namespace Kurx.Infrastructure.Events;

/// <summary>The deterministic scoring engine (V3 §10.3). Pure function of the stored scores + votes + policy — same
/// inputs always yield the same ranking, and every tie is resolved (an unresolved tie is a defect). Judge scores are
/// optionally per-judge z-score normalised, aggregated (sum/mean/trimmed-mean/median/rank), min-max scaled to [0,1];
/// public votes contribute a capped share; the weighted blend is ranked with explicit tie-breaks then a deterministic
/// subject-id fallback. No statistical anomaly detection here — that is a later analytics concern.</summary>
internal static class ScoreAggregator
{
    internal readonly record struct Subject(CompetitionSubjectType Type, Guid Id);
    internal readonly record struct Ranked(CompetitionSubjectType Type, Guid Id, int Rank, decimal Final, string Breakdown);

    public static List<Ranked> Compute(ScoringPolicy? policy, List<StageParticipant> roster, List<JudgeScore> scores, List<PublicVote> votes)
    {
        // Universe = everyone on the roster, scored, or voted for — deterministically ordered.
        var subjects = roster.Select(r => new Subject(r.SubjectType, r.SubjectId))
            .Concat(scores.Select(s => new Subject(s.SubjectType, s.SubjectId)))
            .Concat(votes.Select(v => new Subject(v.SubjectType, v.SubjectId)))
            .Distinct().OrderBy(s => s.Type).ThenBy(s => s.Id).ToList();
        if (subjects.Count == 0) return [];

        var seed = roster.GroupBy(r => new Subject(r.SubjectType, r.SubjectId)).ToDictionary(g => g.Key, g => g.Min(x => x.Seed));
        var voteCount = subjects.ToDictionary(s => s, s => votes.Count(v => v.SubjectType == s.Type && v.SubjectId == s.Id));

        var (aggregation, normalisation) = (policy?.Aggregation ?? ScoreAggregation.WeightedMean, policy?.Normalisation ?? ScoreNormalisation.None);
        var effectiveScores = normalisation == ScoreNormalisation.PerJudgeZscore ? ZscorePerJudge(scores) : scores.ToDictionary(s => s.Id, s => (double)s.Score);

        var judgeRaw = JudgeRaw(subjects, scores, effectiveScores, aggregation);
        var judgeComp = MinMax(judgeRaw);
        var voteComp = MinMax(subjects.ToDictionary(s => s, s => (double)voteCount[s]));

        var (wJudge, wVote) = Weights(policy, scores.Count > 0, votes.Count > 0);

        var scored = subjects.Select(s =>
        {
            var final = wJudge * judgeComp[s] + wVote * voteComp[s];
            return (Subject: s, Final: Math.Round(final * 100.0, 4), JudgeRaw: judgeRaw[s], Votes: voteCount[s], Seed: seed.GetValueOrDefault(s));
        }).ToList();

        var tieBreak = ParseTieBreak(policy);
        scored.Sort((a, b) =>
        {
            var c = b.Final.CompareTo(a.Final);   // higher final first
            if (c != 0) return c;
            foreach (var key in tieBreak)
            {
                c = key switch
                {
                    "judge_score" => b.JudgeRaw.CompareTo(a.JudgeRaw),
                    "votes" => b.Votes.CompareTo(a.Votes),
                    "seed" => (a.Seed ?? int.MaxValue).CompareTo(b.Seed ?? int.MaxValue),   // lower seed first
                    _ => 0,
                };
                if (c != 0) return c;
            }
            return a.Subject.Id.CompareTo(b.Subject.Id);   // ultimate deterministic fallback — no unresolved tie
        });

        return scored.Select((x, i) => new Ranked(x.Subject.Type, x.Subject.Id, i + 1, (decimal)x.Final,
            JsonSerializer.Serialize(new
            {
                judgeComponent = Math.Round(judgeComp[x.Subject], 4), voteComponent = Math.Round(voteComp[x.Subject], 4),
                judgeRaw = Math.Round(x.JudgeRaw, 4), votes = x.Votes, seed = x.Seed,
                weightJudge = Math.Round(wJudge, 4), weightVote = Math.Round(wVote, 4), final = x.Final,
            }))).ToList();
    }

    /// <summary>Per-judge z-score: within each judge's own scores, (x − mean) / stddev. Removes a harsh/lenient
    /// judge's bias before aggregation. A judge with a single score or zero spread contributes 0 (neutral).</summary>
    private static Dictionary<Guid, double> ZscorePerJudge(List<JudgeScore> scores)
    {
        var result = new Dictionary<Guid, double>();
        foreach (var byJudge in scores.GroupBy(s => s.JudgeParticipantId))
        {
            var vals = byJudge.Select(s => (double)s.Score).ToList();
            var mean = vals.Average();
            var std = Math.Sqrt(vals.Average(v => (v - mean) * (v - mean)));
            foreach (var s in byJudge) result[s.Id] = std == 0 ? 0.0 : ((double)s.Score - mean) / std;
        }
        return result;
    }

    private static Dictionary<Subject, double> JudgeRaw(List<Subject> subjects, List<JudgeScore> scores, Dictionary<Guid, double> effective, ScoreAggregation aggregation)
    {
        if (aggregation == ScoreAggregation.RankAggregation) return RankAggregate(subjects, scores, effective);
        var raw = subjects.ToDictionary(s => s, _ => 0.0);
        foreach (var s in subjects)
        {
            var vals = scores.Where(js => js.SubjectType == s.Type && js.SubjectId == s.Id).Select(js => effective[js.Id]).OrderBy(v => v).ToList();
            raw[s] = vals.Count == 0 ? 0.0 : aggregation switch
            {
                ScoreAggregation.Sum => vals.Sum(),
                ScoreAggregation.Median => Median(vals),
                ScoreAggregation.TrimmedMean => vals.Count >= 3 ? vals.Skip(1).Take(vals.Count - 2).Average() : vals.Average(),
                _ => vals.Average(),   // WeightedMean (equal judges) is the default
            };
        }
        return raw;
    }

    /// <summary>Borda-style rank aggregation: each judge ranks the subjects they scored; a subject earns
    /// (n − position) points per judge, summed. Robust to differing judge scales without normalisation.</summary>
    private static Dictionary<Subject, double> RankAggregate(List<Subject> subjects, List<JudgeScore> scores, Dictionary<Guid, double> effective)
    {
        var points = subjects.ToDictionary(s => s, _ => 0.0);
        foreach (var byJudge in scores.GroupBy(s => s.JudgeParticipantId))
        {
            var ordered = byJudge.OrderByDescending(s => effective[s.Id]).ThenBy(s => s.SubjectId).ToList();
            for (var i = 0; i < ordered.Count; i++)
                points[new Subject(ordered[i].SubjectType, ordered[i].SubjectId)] += ordered.Count - i;
        }
        return points;
    }

    private static double Median(List<double> sorted)
    {
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    /// <summary>Min-max scale to [0,1]. All-equal (or single) subjects map to 1.0 — they are tied, and the tie-break
    /// chain, not an arbitrary 0, decides their order.</summary>
    private static Dictionary<Subject, double> MinMax(Dictionary<Subject, double> raw)
    {
        if (raw.Count == 0) return raw;
        var min = raw.Values.Min();
        var max = raw.Values.Max();
        var range = max - min;
        return raw.ToDictionary(kv => kv.Key, kv => range == 0 ? 1.0 : (kv.Value - min) / range);
    }

    private static (double Judge, double Vote) Weights(ScoringPolicy? policy, bool anyScores, bool anyVotes)
    {
        double wJudge = 0, wVote = 0;
        var sources = policy is null ? null : JsonSerializer.Deserialize<List<SourceRow>>(policy.SourcesJson);
        if (sources is { Count: > 0 })
            foreach (var s in sources)
            {
                if (string.Equals(s.type, "JUDGE", StringComparison.OrdinalIgnoreCase)) wJudge += s.weight;
                else if (string.Equals(s.type, "PUBLIC_VOTE", StringComparison.OrdinalIgnoreCase) || string.Equals(s.type, "PUBLICVOTE", StringComparison.OrdinalIgnoreCase)) wVote += s.weight;
                // AUTOMATED sources are deferred (§10.3) — ignored, not blended.
            }
        else { wJudge = anyScores ? 1 : 0; wVote = anyVotes && !anyScores ? 1 : 0; }   // no policy ⇒ judge-led, or vote-only if only votes exist

        // Cap the public-vote share (§10.3 PublicVoteRules) so ballot-stuffing can never dominate a judged result.
        // The cap bounds vote influence *against judge score* — with no judge component there is nothing to bound,
        // so a vote-only stage ranks entirely by votes regardless of the cap (never collapse wVote to 0 here).
        var cap = (policy?.VoteWeightCapPercent ?? 100) / 100.0;
        if (wVote > 0 && wJudge > 0)
        {
            var share = wVote / (wJudge + wVote);
            if (share > cap) wVote = cap <= 0 ? 0 : cap >= 1 ? wVote : wJudge * cap / (1 - cap);
        }
        var total = wJudge + wVote;
        return total == 0 ? (1, 0) : (wJudge / total, wVote / total);
    }

    private static List<string> ParseTieBreak(ScoringPolicy? policy)
        => policy?.TieBreakJson is null ? [] : JsonSerializer.Deserialize<List<string>>(policy.TieBreakJson) ?? [];

    private sealed record SourceRow(string type, double weight);
}
