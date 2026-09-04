using HattrickAI.V5.Core;

namespace HattrickAI.V5.OfflineTests;

/// <summary>
/// C15 acceptance: M11 finalist pool and final selection continuity.
/// </summary>
public static class M11FinalSelectionRegression
{
    public static int Run()
    {
        try
        {
            var candidates = new List<M11CandidateEvaluation>
            {
                Candidate("4-4-2", 0.70, 0.75, 0.8),
                Candidate("3-5-2", 0.82, 0.55, 0.9),
                Candidate("5-3-2", 0.65, 0.60, 0.7)
            };

            var result = new M11FinalSelectorEngine().Select(candidates);

            Check(result.CandidateCount == 3, "M11 finalist count preserved");
            Check(result.FormationCount == 3, "M11 formation diversity preserved");
            Check(result.Ranking.Count > 0, "M11 ranking produced");
            Check(!string.IsNullOrWhiteSpace(result.BestPlan.Formation), "M11 final formation selected");
            Check(result.Prediction is not null, "M11 prediction continuity");
            Check(result.Ranking.First().Formation == result.BestPlan.Formation, "winner matches ranking leader");

            Console.WriteLine("PASS: C15 M11 final selection contract");
            return 0;
        }
        catch(Exception ex)
        {
            Console.WriteLine("FAIL: C15 " + ex.Message);
            return 1;
        }
    }

    private static M11CandidateEvaluation Candidate(string formation, double tactical, double win, double structural)
    {
        throw new NotImplementedException("fixture builder will use existing pipeline records");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
