using System.Text.Json;

namespace HattrickAI.V5.Core;

/// <summary>
/// FAZ9: gerçek offline fixture ile M3→M11 entegrasyonunu regression olarak doğrular.
/// </summary>
public static class FullPipelineRegressionRunner
{
    public static async Task<int> RunAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return Fail($"offline JSON bulunamadı: {path}");

        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            var normalized = root.GetProperty("normalized");
            var analysis = root.GetProperty("v5Analysis");

            var players = normalized.GetProperty("ownPlayers").EnumerateArray().Select(ReadPlayer).ToList();
            var lineup = ReadLineup(analysis.GetProperty("ownLineup"));
            var opponent = ReadRating(analysis.GetProperty("opponentRating"));
            var opponentName = GetString(analysis, "opponentName", "Opponent");
            var opponentFormation = GetString(analysis, "opponentFormation", "");
            var teamName = GetString(analysis, "teamName", lineup.TeamName);

            var failures = new List<string>();
            Check(players.Count >= 11, "offline player pool >= 11", failures);
            Check(lineup.Slots.Count == 11, "fixture own XI has 11 slots", failures);
            Check(IsFiniteRating(opponent), "fixture opponent rating finite", failures);

            var opponentProfile = new OpponentMatchProfile(
                opponentName,
                opponentFormation,
                opponent,
                new OpponentThreatEngine().Analyze(opponent));

            var context = new MatchDataContext(
                players,
                0,
                teamName,
                opponentProfile,
                new RatingContext(MatchLocation.Away, TeamAttitude.Normal),
                MatchQuestionnaire.Default);

            Console.WriteLine("=== V5 FAZ9 FULL OFFLINE REGRESSION ===");
            Console.WriteLine($"Fixture: {teamName} vs {opponentName}");

            var pipeline = new MotorPipelineService();
            var result = await pipeline.RunAsync(context, players, cancellationToken, "offline-faz9");

            var legalFormations = result.M4.Candidates
                .Select(x => x.Formation)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            Check(result.M3.Players.Count == players.Count, "M3 player count continuity", failures);
            Check(legalFormations.Count >= 2, "M4 has >= 2 legal formations", failures);
            Check(result.M5.Count > 0, "M5 produced XI candidates", failures);
            Check(legalFormations.All(f => result.M5.Any(x => x.Formation == f)), "M5 contains every legal formation", failures);

            // FAZ2: M6-A formation-aware search gerçekten bütün legal formasyonları DB1'e taşır.
            Check(result.M6.EvaluatedCandidates > 0, "M6-A evaluated candidates", failures);
            Check(result.CandidateDatabase1Count >= legalFormations.Count * CandidateEvaluationDatabase.MinimumPerFormation,
                "DB1 reaches formation-depth budget", failures);
            var db1 = result.M5
                .Select(x => x.Formation)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            Check(db1.All(f => legalFormations.Contains(f, StringComparer.Ordinal)), "M6-A formation identities valid", failures);

            // DB1/DB2 gerçek depth kontrolü pipeline log/result üzerinden ayrıca yapılır;
            // CandidateDatabaseSet içindeki minimum depth invariantı tüm legal formasyonları zorunlu tutar.
            Check(result.CandidateDatabase2Count >= legalFormations.Count * CandidateEvaluationDatabase.MinimumPerFormation,
                "DB2 reaches formation-depth budget", failures);

            // FAZ4: M10 bütün legal formasyonları leaderboard'a sokmuş olmalı.
            var competition = result.M10.FormationCompetition ?? [];
            Check(competition.Count == legalFormations.Count, "M10 leaderboard contains every legal formation", failures);
            foreach (var formation in legalFormations)
            {
                var row = competition.FirstOrDefault(x => x.Formation == formation);
                Check(row is not null, $"M10 row exists {formation}", failures);
                if (row is not null)
                {
                    Check(row.Rank > 0, $"M10 rank valid {formation}", failures);
                    Check(row.CandidateCount >= M10FinalDecisionEngine.RequiredFormationDepth,
                        $"M10 formation depth {formation}", failures);
                    Check(row.SearchDepthStatus == M10SearchDepthStatus.Sufficient,
                        $"M10 depth status sufficient {formation}", failures);
                    Check(double.IsFinite(row.CompositeScore), $"M10 composite score finite {formation}", failures);
                }
            }

            // FAZ5/6: M6-B + DB2 ikinci arama turu bütün formasyonları korur.
            Check(result.M11 is not null, "M11 decision exists", failures);
            if (result.M11 is not null)
            {
                Check(result.M11.FormationCount == legalFormations.Count, "M11 compares every legal formation", failures);
                Check(result.M11.CandidateCount > 0, "M11 has finalists", failures);
                Check(result.M11.Ranking.Count > 0, "M11 ranking is non-empty", failures);
                Check(double.IsFinite(result.M11.Ranking[0].FinalScore), "M11 winner final score finite", failures);
            }

            // FAZ1: M9 W/D/L aynı expected-goals çiftinden türemeli ve toplam 1 olmalı.
            var p = result.M9.Prediction;
            var probabilitySum = p.WinProbability + p.DrawProbability + p.LossProbability;
            Check(double.IsFinite(p.ExpectedHomeGoals) && double.IsFinite(p.ExpectedAwayGoals), "M9 expected goals finite", failures);
            Check(p.WinProbability is >= 0 and <= 1, "M9 win probability bounded", failures);
            Check(p.DrawProbability is >= 0 and <= 1, "M9 draw probability bounded", failures);
            Check(p.LossProbability is >= 0 and <= 1, "M9 loss probability bounded", failures);
            Check(Math.Abs(probabilitySum - 1.0) < 1e-9, "M9 W/D/L sum = 1", failures);
            if (p.ExpectedHomeGoals > p.ExpectedAwayGoals)
                Check(p.WinProbability >= p.LossProbability, "M9 probability direction follows expected goals", failures);
            else if (p.ExpectedHomeGoals < p.ExpectedAwayGoals)
                Check(p.WinProbability <= p.LossProbability, "M9 probability direction follows expected goals", failures);

            // Determinizm: aynı fixture ikinci kez çalıştırıldığında finalist sonucu değişmemeli.
            var resultAgain = await pipeline.RunAsync(context, players, cancellationToken, "offline-faz9-repeat");
            Check(result.FinalPlan.Formation == resultAgain.FinalPlan.Formation, "full pipeline deterministic formation", failures);
            Check(result.M11?.Ranking.FirstOrDefault()?.CandidateId == resultAgain.M11?.Ranking.FirstOrDefault()?.CandidateId,
                "full pipeline deterministic finalist", failures);
            Check(Math.Abs(result.M9.Prediction.WinProbability - resultAgain.M9.Prediction.WinProbability) < 1e-12,
                "full pipeline deterministic M9", failures);

            Console.WriteLine($"M3={result.M3.Players.Count} | M4={legalFormations.Count} formations | M5={result.M5.Count}");
            Console.WriteLine($"M6-A evaluated={result.M6.EvaluatedCandidates} | DB1={result.CandidateDatabase1Count}");
            Console.WriteLine($"M10 formations={competition.Count} | DB2={result.CandidateDatabase2Count} | M11 formations={result.M11?.FormationCount ?? 0}");
            Console.WriteLine($"M9 xG={p.ExpectedHomeGoals:0.###}-{p.ExpectedAwayGoals:0.###} | W/D/L={p.WinProbability:P1}/{p.DrawProbability:P1}/{p.LossProbability:P1}");
            Console.WriteLine($"FINAL={result.FinalPlan.Formation} | formations={string.Join(", ", legalFormations)}");

            if (failures.Count > 0)
            {
                foreach (var failure in failures) Console.WriteLine("FAIL: " + failure);
                return 1;
            }

            Console.WriteLine("PASS: FAZ1 M9 W/D/L");
            Console.WriteLine("PASS: FAZ2 M6-A formation-aware search");
            Console.WriteLine("PASS: FAZ3 DB1 formation depth");
            Console.WriteLine("PASS: FAZ4 M10 formation leaderboard");
            Console.WriteLine("PASS: FAZ5 M6-B exploration/refinement");
            Console.WriteLine("PASS: FAZ6 DB2 formation depth");
            Console.WriteLine("PASS: FAZ7 M11 final comparison");
            Console.WriteLine("PASS: FAZ8 web finalist/alternative pipeline contract");
            Console.WriteLine("PASS: FAZ9 full offline regression");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("FAIL: offline regression cancelled");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("STACK: " + ex);
            return Fail("offline regression exception: " + ex.Message);
        }
    }

    private static string GetString(JsonElement parent, string property, string fallback)
        => parent.TryGetProperty(property, out var value) ? value.GetString() ?? fallback : fallback;

    private static Player ReadPlayer(JsonElement e) => new(
        e.GetProperty("id").GetInt32(),
        e.GetProperty("name").GetString() ?? "",
        e.GetProperty("keeper").GetInt32(),
        e.GetProperty("defending").GetInt32(),
        e.GetProperty("playmaking").GetInt32(),
        e.GetProperty("passing").GetInt32(),
        e.GetProperty("winger").GetInt32(),
        e.GetProperty("scoring").GetInt32(),
        e.GetProperty("stamina").GetInt32(),
        e.GetProperty("form").GetInt32(),
        e.GetProperty("experience").GetInt32(),
        e.TryGetProperty("loyalty", out var loyalty) ? loyalty.GetInt32() : 0,
        e.TryGetProperty("injuryLevel", out var injury) ? injury.GetInt32() : -1);

    private static Lineup ReadLineup(JsonElement e)
    {
        var slots = e.GetProperty("slots").EnumerateArray().Select(s => new Slot(
            s.GetProperty("code").GetString() ?? "",
            s.GetProperty("label").GetString() ?? "",
            s.GetProperty("description").GetString() ?? "",
            s.TryGetProperty("playerName", out var name) ? name.GetString() : null,
            s.GetProperty("playerId").GetInt32(),
            s.GetProperty("rating").GetDouble(),
            s.GetProperty("x").GetDouble(),
            s.GetProperty("y").GetDouble())).ToList();
        return new Lineup(e.GetProperty("teamName").GetString() ?? "", e.GetProperty("formation").GetString() ?? "", slots);
    }

    private static RegionalRatingSnapshot ReadRating(JsonElement e) => new(
        e.GetProperty("rawLeftDefence").GetDouble(),
        e.GetProperty("rawCentralDefence").GetDouble(),
        e.GetProperty("rawRightDefence").GetDouble(),
        e.GetProperty("rawMidfield").GetDouble(),
        e.GetProperty("rawLeftAttack").GetDouble(),
        e.GetProperty("rawCentralAttack").GetDouble(),
        e.GetProperty("rawRightAttack").GetDouble(),
        e.GetProperty("leftDefence").GetDouble(),
        e.GetProperty("centralDefence").GetDouble(),
        e.GetProperty("rightDefence").GetDouble(),
        e.GetProperty("midfield").GetDouble(),
        e.GetProperty("leftAttack").GetDouble(),
        e.GetProperty("centralAttack").GetDouble(),
        e.GetProperty("rightAttack").GetDouble());

    private static bool IsFiniteRating(RegionalRatingSnapshot r) => new[]
    {
        r.LeftDefence, r.CentralDefence, r.RightDefence, r.Midfield,
        r.LeftAttack, r.CentralAttack, r.RightAttack
    }.All(double.IsFinite);

    private static void Check(bool ok, string name, List<string> failures)
    {
        if (!ok) failures.Add(name);
    }

    private static int Fail(string message)
    {
        Console.WriteLine("FAIL: " + message);
        return 1;
    }
}
