using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C2 acceptance: M4 must emit only the supported legal, 11-slot formations
/// and must drop formations that cannot be filled by the eligible player pool.
/// The legal formation/slot contract is owned by the production registry.
/// </summary>
public static class M4LegalFormationRegression
{
    private static readonly string[] ExpectedFormationNames =
    ["2-5-3", "3-4-3", "3-5-2", "4-4-2", "4-5-1", "5-3-2"];

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

            var registry = FormationCandidateEngine.LegalFormations;
            Check(registry.Count == ExpectedFormationNames.Length, $"M4 legal registry has {registry.Count} formations; expected {ExpectedFormationNames.Length}");
            Check(registry.Select(x => x.Formation).Distinct(StringComparer.Ordinal).Count() == registry.Count, "M4 legal registry formation identities are unique");
            Check(registry.Select(x => x.Formation).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(ExpectedFormationNames.OrderBy(x => x, StringComparer.Ordinal)), "M4 legal registry formation set changed");

            foreach (var definition in registry)
            {
                Check(definition.SlotCodes.Count == 11, $"registry {definition.Formation} has {definition.SlotCodes.Count} slots");
                Check(definition.SlotCodes.Distinct(StringComparer.Ordinal).Count() == 11, $"registry {definition.Formation} contains duplicate slot codes");
                Check(definition.SlotCodes.All(code => AllSlots.Contains(code, StringComparer.Ordinal)), $"registry {definition.Formation} contains an unknown slot code");
                Check(definition.SlotCodes.Count(x => x == "GK") == 1, $"registry {definition.Formation} must contain exactly one GK");
            }

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
            var formationEngine = new FormationCandidateEngine();
            var result = formationEngine.Generate(m3);

            Check(result.Candidates.Count == registry.Count, $"M4 emitted {result.Candidates.Count} formations; registry has {registry.Count}");
            Check(result.Candidates.Select(x => x.Formation).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(registry.Select(x => x.Formation).OrderBy(x => x, StringComparer.Ordinal)), "M4 emitted an unexpected formation set");

            foreach (var candidate in result.Candidates)
            {
                var definition = registry.Single(x => x.Formation == candidate.Formation);
                Check(candidate.SlotCodes.SequenceEqual(definition.SlotCodes), $"{candidate.Formation} slot contract differs from authoritative registry");
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

            Console.WriteLine("PASS: C2 M4 legal formations + authoritative registry");
            Console.WriteLine($"Legal set={registry.Count} | slot contract=11 each | registry source=production | feasibility guard=10-player rejection");
            Console.WriteLine("NEXT: C3 M5 XI candidates");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL: C2 " + ex.Message);
            return 1;
        }
    }
}
