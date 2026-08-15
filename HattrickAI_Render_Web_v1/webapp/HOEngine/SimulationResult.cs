namespace HattrickAI.HOEngine;

public class SimulationResult
{
    public int Simulations { get; private set; }
    public int HomeWins { get; private set; }
    public int Draws { get; private set; }
    public int AwayWins { get; private set; }
    public int[] ScoreDistribution { get; } = new int[25];
    public int HomeGoals { get; private set; }
    public int AwayGoals { get; private set; }

    public void Add(MatchResult result)
    {
        Simulations++;
        HomeWins += result.GetHomeWin();
        Draws += result.GetDraw();
        AwayWins += result.GetAwayWin();
        HomeGoals += result.GetHomeGoals();
        AwayGoals += result.GetGuestGoals();

        int[] details = result.GetResultDetail();
        for (int i = 0; i < details.Length && i < ScoreDistribution.Length; i++)
            ScoreDistribution[i] += details[i];
    }

    public double HomeWinPercentage => Simulations == 0 ? 0 : HomeWins * 100.0 / Simulations;
    public double DrawPercentage => Simulations == 0 ? 0 : Draws * 100.0 / Simulations;
    public double AwayWinPercentage => Simulations == 0 ? 0 : AwayWins * 100.0 / Simulations;
    public double AverageHomeGoals => Simulations == 0 ? 0 : HomeGoals * 1.0 / Simulations;
    public double AverageAwayGoals => Simulations == 0 ? 0 : AwayGoals * 1.0 / Simulations;

    public string GetMostLikelyScore()
    {
        if (Simulations == 0) return "-";
        int bestIndex = 0;
        for (int i = 1; i < ScoreDistribution.Length; i++)
            if (ScoreDistribution[i] > ScoreDistribution[bestIndex]) bestIndex = i;
        int home = bestIndex / 5;
        int away = bestIndex % 5;
        return $"{home}-{away}";
    }

    // HO!'nun skor tablosundaki 0,1,2,3,4+ kovalarını birebir korur.
    public IReadOnlyList<ScoreBucket> GetScoreDistribution()
    {
        var result = new List<ScoreBucket>(25);
        for (int home = 0; home < 5; home++)
        {
            for (int away = 0; away < 5; away++)
            {
                int count = ScoreDistribution[(home * 5) + away];
                result.Add(new ScoreBucket(
                    home == 4 ? "4+" : home.ToString(),
                    away == 4 ? "4+" : away.ToString(),
                    count,
                    Simulations == 0 ? 0 : count * 100.0 / Simulations));
            }
        }
        return result;
    }

    // 4+ kovalarını dışarıda bırakır; gerçek tekil skorlar içinden en olası olanı verir.
    public string GetMostLikelyNormalScore()
    {
        if (Simulations == 0) return "-";

        int bestHome = -1;
        int bestAway = -1;
        int bestCount = -1;

        for (int home = 0; home < 4; home++)
        {
            for (int away = 0; away < 4; away++)
            {
                int count = ScoreDistribution[(home * 5) + away];
                if (count > bestCount)
                {
                    bestCount = count;
                    bestHome = home;
                    bestAway = away;
                }
            }
        }

        return bestHome < 0 ? "-" : $"{bestHome}-{bestAway}";
    }
}

public sealed record ScoreBucket(string Home, string Away, int Count, double Percentage);
