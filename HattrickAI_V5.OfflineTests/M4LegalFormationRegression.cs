using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C2 acceptance: M4 must emit only the supported legal, 11-slot formations
/// and must drop formations that cannot be filled by the eligible player pool.
/// </summary>
public static class M4LegalFormationRegression
{
    private static readonly IReadOnlyDictionary<string, string[]> Expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["3-5-2"] = ["GK", "DEF-CL", "DEF-C", "DEF-CR", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-R"],
        ["3-4-3"] = ["GK", "DEF-CL", "DEF-C", "DEF-CR", "W-L", "IM-L", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"],
        ["4-4-2"] = ["GK", "DEF-L", "DEF-CL", "DEF-CR", "DEF-R", "W-L", "IM-L", "IM-R", "W-R", "FW-L", "FW-R"],
        ["4-5-1"] = ["GK", "DEF-L", "DEF-CL", "DEF-CR", "DEF-R", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-C"],
        ["2-5-3"] = ["GK", "DEF-CL", "DEF-CR", "W-L", "IM-L", "IM-C", "IM-R", "W-R", "FW-L", "FW-C", "FW-R"],
        ["5-3-2"] = ["GK", "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R", "IM-L", "IM-C", "IM-R", "FW-L", "FW-R"]
    };

    private static readonly string[] AllSlots =
    [
        "GK",
        "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R",
        "W-L", "IM-L", "IM-C", "IM-R", "W-R",
        "FW-L", "FW-C", "FW-R"
    ];

    public static int Run()
    {
        try
        {
            Console.WriteLine("=== C2 M4 LEGAL FORMATION REGRESSION ===");

            var engine = new PlayerAnalysisEngine();
            var formationEngine = new FormationCandidateEngine();
            var players = Enumerable.Range(1, 11)
                .Select(id => new PlayerAnalysisProfile(
                    id,
                    $"Fixture Player {id}",
                    true,
                    0,
                    PlayerSpecialty.None,
                    new PlayerSpecialtyProfile(PlayerSpecialty.None, false, false, false, false, false, false, false, ""),
                    AllSlots.Select(code => new PlayerPositionCandidate(code, 1.0)).ToList(),
                    "GK",
                    "DEF-C"))
                .ToList();

            var m3 = new PlayerAnalysisResult(players);
            var result = formationEngine.Generate(m3);

            Check(result.Candidates.Count == Expected.Count, $"M4 emitted {result.Candidates.Count} formations; expected {Expected.Count}");

            var actualNames = result.Candidates.Select(x => x.Formation).Distinct(StringComparer.Ordinal).ToList();
            Check(actualNames.Count == result.Candidates.Count, "M4 formation identities are unique");
            Check(actualNames.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Expected.Keys.OrderBy(x => x, StringComparer.Ordinal)), "M4 emitted an unexpected formation set");

            foreach (var candidate in result.Candidates)
            {
                Check(Expected.TryGetValue(candidate.Formation, out var expectedSlots), $"M4 emitted non-legal formation {candidate.Formation}");
                Check(candidate.SlotCodes.Count == 11, $"{candidate.Formation} has {candidate.SlotCodes.Count} slots");
                Check(candidate.SlotCodes.Distinct(StringComparer.Ordinal).Count() == 11, $"{candidate.Formation} contains duplicate slot codes");
                Check(candidate.SlotCodes.SequenceEqual(expectedSlots), $"{candidate.Formation} slot contract changed");
                Check(candidate.SlotCodes.Count(x => x == "GK") == 1, $"{candidate.Formation} must contain exactly one GK");
                Check(double.IsFinite(candidate.StructuralScore) && candidate.StructuralScore > 0, $"{candidate.Formation} structural score is invalid");
            }

            var underfilledPlayers = players.Take(10).ToList();
            var underfilled = formationEngine.Generate(new PlayerAnalysisResult(underfilledPlayers));
            Check(underfilled.Candidates.Count == 0, "M4 must reject every formation when only 10 eligible players are available");

            var ineligible = players.Take(10)
                .Append(players[0] with { InjuryLevel = 999 })
                .ToList();
            var ineligibleResult = formationEngine.Generate(new PlayerAnalysisResult(ineligible));
            Check(ineligibleResult.Candidates.Count == 0, "M4 must exclude injured/ineligible players from feasibility");

            Console.WriteLine("PASS: C2 M4 legal formations");
            Console.WriteLine("  Legal set=6 | slot contract=11 each | feasibility guard=10-player rejection");
            Console.WriteLine("NEXT: C3 M5 XI candidates");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL: C2 " + ex.Message);
            return 1;
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
