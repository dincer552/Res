using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

public static class M11FinalSelectionRegression
{
    public static int Run()
    {
        try
        {
            var candidates = new List<M11CandidateEvaluation>
            {
                Fixture("4-4-2", 0.70, 0.80, 0.80),
                Fixture("3-5-2", 0.82, 0.60, 0.90),
                Fixture("5-3-2", 0.65, 0.50, 0.70)
            };

            var result = new M11FinalSelectorEngine().Select(candidates);

            Check(result.CandidateCount == 3, "candidate count preserved");
            Check(result.FormationCount == 3, "formation diversity preserved");
            Check(result.Ranking.Count == 3, "ranking generated");
            Check(result.BestPlan is not null, "final plan exists");
            Check(result.Prediction is not null, "prediction continuity");
            Check(result.Ranking[0].Formation == result.BestPlan.Formation, "winner matches rank #1");

            var again = new M11FinalSelectorEngine().Select(candidates);
            Check(again.BestPlan.Formation == result.BestPlan.Formation, "deterministic final selection");

            Console.WriteLine("PASS: C15 M11 final selection contract");
            return 0;
        }
        catch(Exception ex)
        {
            Console.WriteLine("FAIL: C15 " + ex.Message);
            return 1;
        }
    }

    private static M11CandidateEvaluation Fixture(string formation, double tactical, double win, double structural)
    {
        var lineup = new Lineup(formation, new List<LineupSlot>());
        var prediction = new MatchPrediction(
            new SimulationResult(new OutcomeDistribution(win, 0.2, 1.0 - win - 0.2)),
            win);
        return new M11CandidateEvaluation(
            new TacticalCandidate(lineup, tactical, null!, null!),
            prediction,
            structural);
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
