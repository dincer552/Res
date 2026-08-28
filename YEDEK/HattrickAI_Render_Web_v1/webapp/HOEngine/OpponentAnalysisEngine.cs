namespace HattrickAI.HOEngine;

public class OpponentAnalysisEngine
{
    public string GetTacticRecommendation(TeamData opponent)
    {
        TeamRatings ratings = opponent.Ratings;

        double weakestDefence = Math.Min(
            ratings.LeftDefence,
            Math.Min(
                ratings.CentralDefence,
                ratings.RightDefence));

        string attackPlan = GetAttackPlan(
            ratings,
            weakestDefence);

        string midfieldPlan = ratings.Midfield < 5.0
            ? "Rakibin orta sahasi dusuk; topa sahipligi almak mumkun."
            : "Rakibin orta sahasi guclu; oyunkuruculugu onceliklendir.";

        double strongestAttack = Math.Max(
            ratings.LeftAttack,
            Math.Max(
                ratings.CentralAttack,
                ratings.RightAttack));

        string defencePlan = strongestAttack < 5.0
            ? "Rakibin hucumu sinirli; dengeli bir savunma yeterli."
            : "Rakibin hucumunu dikkate al; savunma gucunu koru.";

        return
            $"RAKIBE GORE TAKTIK\n\n" +
            $"Rakip: {opponent.TeamName}\n\n" +
            $"Ana plan: {attackPlan}\n" +
            $"Orta saha: {midfieldPlan}\n" +
            $"Savunma: {defencePlan}";
    }

    public string GetMatchPlan(
        TeamData opponent,
        string formation)
    {
        string formationPlan = opponent.Ratings.Midfield < 5.0
            ? $"Onerilen dizilis: {formation}. Rakibin orta sahasina karsi oyunu kontrol et."
            : $"Onerilen dizilis: {formation}. Orta sahada dengeyi koru.";

        return
            GetTacticRecommendation(opponent) +
            "\n\n" +
            "BIZIM MAC PLANI\n\n" +
            formationPlan;
    }

    public int GetRecommendedTacticType(TeamData opponent)
    {
        TeamRatings ratings = opponent.Ratings;

        if (ratings.CentralDefence + 0.50 <
            Math.Min(ratings.LeftDefence, ratings.RightDefence))
        {
            return 3;
        }

        if (ratings.LeftDefence == ratings.RightDefence &&
            ratings.CentralDefence >= ratings.LeftDefence + 0.50)
        {
            return 4;
        }

        return 0;
    }

    private static string GetAttackPlan(
        TeamRatings ratings,
        double weakestDefence)
    {
        bool centralIsWeakest =
            ratings.CentralDefence == weakestDefence;

        bool bothWingsAreWeak =
            ratings.LeftDefence == weakestDefence &&
            ratings.RightDefence == weakestDefence;

        if (centralIsWeakest &&
            ratings.CentralDefence + 0.50 <
            Math.Min(ratings.LeftDefence, ratings.RightDefence))
        {
            return "ORTADAN HUCUM kullan.";
        }

        if (bothWingsAreWeak &&
            ratings.CentralDefence >= weakestDefence + 0.50)
        {
            return "KANATLARDAN HUCUM kullan.";
        }

        if (ratings.LeftDefence == weakestDefence)
            return "Sol kanada daha fazla hucum katkisi ver.";

        if (ratings.RightDefence == weakestDefence)
            return "Sag kanada daha fazla hucum katkisi ver.";

        return "Dengeli hucum kullan.";
    }
}
