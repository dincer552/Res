namespace HattrickAI.HOEngine;

public class SimulationResult
{
    public int Simulations { get; private set; }

    public int HomeWins { get; private set; }

    public int Draws { get; private set; }

    public int AwayWins { get; private set; }

    // HO! MatchResult uses a 5x5 score table. Scores above 4 are grouped
    // into the 4 bucket, exactly like HO!'s result table.
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

    public double HomeWinPercentage =>
        Simulations == 0 ? 0 : HomeWins * 100.0 / Simulations;

    public double DrawPercentage =>
        Simulations == 0 ? 0 : Draws * 100.0 / Simulations;

    public double AwayWinPercentage =>
        Simulations == 0 ? 0 : AwayWins * 100.0 / Simulations;

    public double AverageHomeGoals =>
        Simulations == 0 ? 0 : HomeGoals * 1.0 / Simulations;

    public double AverageAwayGoals =>
        Simulations == 0 ? 0 : AwayGoals * 1.0 / Simulations;

    /// <summary>
    /// Returns the most frequent score from HO!'s 5x5 result table.
    /// 4 means 4 or more goals, matching HO!'s result bucketing.
    /// </summary>
    public string GetMostLikelyScore()
    {
        if (Simulations == 0)
            return "-";

        int bestIndex = 0;

        for (int i = 1; i < ScoreDistribution.Length; i++)
        {
            if (ScoreDistribution[i] > ScoreDistribution[bestIndex])
                bestIndex = i;
        }

        int home = bestIndex / 5;
        int away = bestIndex % 5;

        return $"{home}-{away}";
    }
}
