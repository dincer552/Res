using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.HOEngine;

public sealed class RecommendationEngine
{
    private readonly BestLineupEngine _lineupEngine = new();
    private readonly LineupRatingEngine _ratingEngine = new();
    private readonly MatchSimulator _simulator = new();

    private static readonly TacticCandidate[] Tactics =
    {
        new("Dengeli oyun", 0, 0),
        new("Pres", 1, 6),
        new("Kontra atak", 2, 6),
        new("Ortadan hücum", 3, 5),
        new("Kanatlardan hücum", 4, 5),
        new("Yaratıcı oyun", 7, 6)
    };

    public RecommendationResult? Recommend(
        List<PlayerData> players,
        TeamData opponent,
        int simulationCount = 1000,
        bool isHome = true,
        int trainingType = -1,
        IReadOnlyDictionary<string, int>? formationExperience = null)
    {
        if (players == null || players.Count < 11) return null;

        if (!string.IsNullOrWhiteSpace(opponent.PreferredFormation))
            return RecommendForFormation(players, opponent, opponent.PreferredFormation!, simulationCount, isHome, trainingType, formationExperience);

        var formations = BestLineupEngine.SupportedFormations
            .OrderByDescending(f => TrainingFormationFit(trainingType, f))
            .ThenByDescending(f => formationExperience?.TryGetValue(f, out var exp) == true ? exp : 0)
            .ThenBy(f => f == "3-4-3" ? 0 : 1)
            .ToArray();

        RecommendationResult? best = null;
        foreach (string formation in formations)
        {
            var result = RecommendForFormation(players, opponent, formation, simulationCount, isHome, trainingType, formationExperience);
            if (result == null) continue;

            // The order is deliberate: training tier first, then match quality.
            // This prevents a formation that wastes the week's training from
            // winning the recommendation merely because it produces a slightly
            // higher simulated win percentage.
            if (best == null || result.SelectionScore > best.SelectionScore)
                best = result;
        }
        return best;
    }

    public RecommendationResult? RecommendForFormation(
        List<PlayerData> players,
        TeamData opponent,
        string formation,
        int simulationCount = 10000,
        bool isHome = true,
        int trainingType = -1,
        IReadOnlyDictionary<string, int>? formationExperience = null)
    {
        if (players == null || players.Count < 11 || !BestLineupEngine.SupportedFormations.Contains(formation)) return null;
        var lineup = _lineupEngine.FindBestLineupForFormation(players, formation, opponent.Ratings);
        if (lineup.Count != 11) return null;
        var baseBehaviours = new Dictionary<int, PlayerBehaviour>(_lineupEngine.LastBehaviourProfile);
        RecommendationResult? best = null;

        foreach (var tactic in Tactics)
        {
            var behaviours = BuildTacticBehaviours(lineup, formation, baseBehaviours, tactic.Type);
            var context = new TeamMatchContext
            {
                TacticType = tactic.Type,
                TacticLevel = tactic.Level,
                IsHome = isHome,
                Location = isHome ? TeamLocation.Home : TeamLocation.Away,
                SlotBehaviours = behaviours,
                OpponentRatings = opponent.Ratings
            };
            var ratings = _ratingEngine.Calculate(lineup, formation, context);
            var ourTeam = new TeamData("Bizim Takım", ratings, tactic.Type, tactic.Level);
            var simulation = isHome ? _simulator.Run(ourTeam, opponent, simulationCount) : _simulator.Run(opponent, ourTeam, simulationCount);
            var score = SelectionScore(simulation, tactic.Type, isHome, trainingType, formation, formationExperience);
            var explanation = BuildExplanation(formation, tactic, ratings, opponent.Ratings, simulation, trainingType, formationExperience);

            if (best == null || score > best.SelectionScore)
            {
                var trainingFit = TrainingFormationFit(trainingType, formation);
                best = new RecommendationResult
                {
                    Formation = formation,
                    TacticName = tactic.Name,
                    TacticType = tactic.Type,
                    TacticLevel = tactic.Level,
                    Lineup = lineup.ToList(),
                    Ratings = ratings,
                    Simulation = simulation,
                    SelectionScore = score,
                    Explanation = explanation,
                    BehaviourProfile = behaviours,
                    TrainingFit = TrainingTier(trainingFit),
                    FormationExperience = formationExperience?.TryGetValue(formation, out var exp) == true ? exp : 0,
                    TrainingName = ChppTrainingName(trainingType),
                    TrainingPriority = TrainingPriorityText(trainingFit)
                };
            }
        }
        return best;
    }

    private static Dictionary<int, PlayerBehaviour> BuildTacticBehaviours(List<PlayerData> lineup, string formation, Dictionary<int, PlayerBehaviour> baseBehaviours, int tacticType)
    {
        var roles = LineupRatingEngine.GetRoles(formation);
        var result = new Dictionary<int, PlayerBehaviour>();
        for (int i = 0; i < roles.Length; i++)
        {
            var baseBehaviour = baseBehaviours.TryGetValue(i, out var b) ? b : PlayerBehaviour.Normal;
            var behaviour = tacticType switch
            {
                5 when roles[i] is PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender => PlayerBehaviour.Defensive,
                5 when roles[i] is PlayerRole.CentralMidfielder or PlayerRole.LeftWinger or PlayerRole.RightWinger => PlayerBehaviour.Defensive,
                3 when roles[i] is PlayerRole.LeftWinger or PlayerRole.RightWinger => PlayerBehaviour.TowardsMiddle,
                4 when roles[i] is PlayerRole.LeftWinger or PlayerRole.RightWinger or PlayerRole.LeftForward or PlayerRole.RightForward => PlayerBehaviour.TowardsWing,
                2 when roles[i] is PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender => PlayerBehaviour.Defensive,
                _ => baseBehaviour
            };
            result[i] = behaviour;
        }
        return result;
    }

    private static double SelectionScore(
        SimulationResult simulation,
        int tacticType,
        bool isHome,
        int trainingType,
        string formation,
        IReadOnlyDictionary<string, int>? formationExperience)
    {
        double ourWin = isHome ? simulation.HomeWinPercentage : simulation.AwayWinPercentage;
        double ourLoss = isHome ? simulation.AwayWinPercentage : simulation.HomeWinPercentage;
        double draw = simulation.DrawPercentage;
        double ourGoals = isHome ? simulation.AverageHomeGoals : simulation.AverageAwayGoals;
        double opponentGoals = isHome ? simulation.AverageAwayGoals : simulation.AverageHomeGoals;

        double matchQuality = ourWin * 0.65 + draw * 0.20 - ourLoss * 0.35
            + Math.Clamp((ourGoals - opponentGoals + 2.0) * 10.0, 0.0, 40.0);
        matchQuality = Math.Clamp(matchQuality, 0.0, 100.0);

        double trainingFit = TrainingFormationFit(trainingType, formation);
        int trainingTier = TrainingTier(trainingFit);
        double routineBonus = formationExperience?.TryGetValue(formation, out var exp) == true
            ? Math.Clamp(exp, 1, 8) * 0.75
            : 0.0;

        // HARD PRIORITY:
        //   1) Full training fit beats partial fit.
        //   2) Partial fit beats a poor fit.
        //   3) Inside the same tier, match-winning quality decides.
        //   4) Formation experience is only a small tie-breaker.
        // This is intentionally not a 60/40 blend: training is a real priority.
        double score = trainingTier * 1000.0 + trainingFit * 10.0 + matchQuality + routineBonus;
        if (tacticType == 1) score += ourWin * 0.01;
        return score;
    }

    public static double TrainingFormationFit(int trainingType, string formation) => trainingType switch
    {
        // Defending: five defenders are the full-training target.
        3 => formation switch { "5-4-1" or "5-3-2" => 1.00, "4-4-2" or "4-5-1" => 0.70, _ => 0.35 },
        // Scoring: three forwards are the full-training target.
        4 => formation switch { "3-4-3" or "4-3-3" => 1.00, "4-4-2" => 0.70, "3-5-2" or "4-5-1" => 0.45, _ => 0.30 },
        // Crossing/Winger: prioritize standard shapes with two wingers and wing-backs.
        5 => formation switch { "4-4-2" or "4-3-3" => 1.00, "3-5-2" or "4-5-1" or "3-4-3" => 0.90, "5-3-2" or "5-4-1" => 0.75, _ => 0.40 },
        // Shooting trains outfielders broadly; attacking shapes are the practical priority.
        6 => formation switch { "3-4-3" or "4-3-3" => 1.00, "3-5-2" or "4-4-2" => 0.90, "4-5-1" => 0.80, _ => 0.65 },
        // Short passes: IMs, wingers and forwards. 2-5-3 is omitted from this app.
        7 => formation switch { "3-4-3" or "3-5-2" => 1.00, "4-4-2" or "4-3-3" => 0.90, "4-5-1" => 0.80, _ => 0.60 },
        // Playmaking: 3-5-2 and 4-5-1 are the standard full-training choices.
        8 => formation switch { "3-5-2" or "4-5-1" => 1.00, "3-4-3" or "4-4-2" => 0.70, "4-3-3" => 0.60, _ => 0.40 },
        // Through passes: 4-5-1 and 5-4-1 are the strongest supported choices.
        10 => formation switch { "5-4-1" or "4-5-1" => 1.00, "5-3-2" or "3-5-2" or "4-4-2" => 0.85, _ => 0.65 },
        // Defensive positions: 5-5-0 would be the maximum, but is not supported here.
        11 => formation switch { "5-4-1" => 1.00, "5-3-2" or "4-5-1" => 0.90, "3-5-2" or "4-4-2" => 0.75, _ => 0.55 },
        // Goalkeeping, set pieces, general and stamina are formation-flexible.
        0 or 1 or 2 or 9 => 0.80,
        _ => 0.55
    };

    private static int TrainingTier(double fit) => fit >= 0.99 ? 2 : fit >= 0.75 ? 1 : 0;

    private static string TrainingPriorityText(double fit) => TrainingTier(fit) switch
    {
        2 => "Tam antrenman uyumu",
        1 => "Kısmi antrenman uyumu",
        _ => "Antrenman için düşük öncelik"
    };

    private static string BuildExplanation(
        string formation,
        TacticCandidate tactic,
        TeamRatings ours,
        TeamRatings opponent,
        SimulationResult simulation,
        int trainingType,
        IReadOnlyDictionary<string, int>? formationExperience)
    {
        var points = new List<string>();
        double midfieldDelta = ours.Midfield - opponent.Midfield;
        double centralDefenceDelta = ours.CentralDefence - opponent.CentralAttack;
        double centralAttackDelta = ours.CentralAttack - opponent.CentralDefence;
        double wingAttackDelta = (ours.LeftAttack + ours.RightAttack) / 2.0 - (opponent.LeftDefence + opponent.RightDefence) / 2.0;

        if (trainingType >= 0)
        {
            var fit = TrainingFormationFit(trainingType, formation);
            var exp = formationExperience?.TryGetValue(formation, out var value) == true ? value : 0;
            points.Add($"Öncelik: {ChppTrainingName(trainingType)} antrenmanı • {formation} {TrainingPriorityText(fit)} • deneyim {exp}.");
        }
        if (midfieldDelta > .20) points.Add("Orta saha avantajımız var.");
        else if (midfieldDelta < -.20) points.Add("Rakibin orta saha üstünlüğü var; topa sahip olmayı artırmak önemli.");
        if (centralAttackDelta > .15) points.Add("Merkez hücumumuz rakibin merkez savunmasına karşı güçlü.");
        if (wingAttackDelta > .15) points.Add("Kanat hücumlarımız rakibin kanat savunmasına göre avantajlı.");
        if (centralDefenceDelta < -.15) points.Add("Rakibin merkez hücumuna karşı merkez savunmayı korumak gerekiyor.");
        points.Add($"{formation} dizilişi {tactic.Name.ToLowerInvariant()} ile simüle edildi.");
        points.Add($"{simulation.Simulations} simülasyonda beklenen skor {simulation.AverageHomeGoals:F2}-{simulation.AverageAwayGoals:F2}.");
        return string.Join(" ", points);
    }

    private static string ChppTrainingName(int type) => type switch
    {
        3 => "Defans", 4 => "Golcülük", 5 => "Kanat (Crossing)", 6 => "Şut", 7 => "Kısa Paslar",
        8 => "Oyun Kurma", 9 => "Kalecilik", 10 => "Ara Paslar", 11 => "Defansif Pozisyonlar",
        2 => "Duran Toplar", 1 => "Dayanıklılık", 0 => "Genel", _ => "Antrenman verisi yok"
    };

    private sealed record TacticCandidate(string Name, int Type, int Level);
}
