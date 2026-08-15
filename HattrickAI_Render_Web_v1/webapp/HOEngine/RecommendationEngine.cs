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
        bool isHome = true)
    {
        if (players == null || players.Count < 11)
            return null;

        RecommendationResult? best = null;

        foreach (string formation in BestLineupEngine.SupportedFormations)
        {
            var lineup = _lineupEngine.FindBestLineupForFormation(players, formation);
            if (lineup.Count != 11)
                continue;

            var baseBehaviours = new Dictionary<int, PlayerBehaviour>(_lineupEngine.LastBehaviourProfile);

            foreach (var tactic in Tactics)
            {
                var behaviours = BuildTacticBehaviours(lineup, formation, baseBehaviours, tactic.Type);
                var context = new TeamMatchContext
                {
                    TacticType = tactic.Type,
                    TacticLevel = tactic.Level,
                    IsHome = isHome,
                    Location = isHome ? TeamLocation.Home : TeamLocation.Away,
                    SlotBehaviours = behaviours
                };

                TeamRatings ratings = _ratingEngine.Calculate(lineup, formation, context);
                var ourTeam = new TeamData("Bizim Takım", ratings, tactic.Type, tactic.Level);
                var simulation = isHome
                    ? _simulator.Run(ourTeam, opponent, simulationCount)
                    : _simulator.Run(opponent, ourTeam, simulationCount);
                double score = SelectionScore(simulation, tactic.Type, isHome);
                string explanation = BuildExplanation(formation, tactic, ratings, opponent.Ratings, simulation);

                if (best == null || score > best.SelectionScore)
                {
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
                        BehaviourProfile = behaviours
                    };
                }
            }
        }

        return best;
    }

    private static Dictionary<int, PlayerBehaviour> BuildTacticBehaviours(
        List<PlayerData> lineup,
        string formation,
        Dictionary<int, PlayerBehaviour> baseBehaviours,
        int tacticType)
    {
        var roles = LineupRatingEngine.GetRoles(formation);
        var result = new Dictionary<int, PlayerBehaviour>();

        for (int i = 0; i < roles.Length; i++)
        {
            PlayerBehaviour baseBehaviour = baseBehaviours.TryGetValue(i, out var b) ? b : PlayerBehaviour.Normal;

            PlayerBehaviour behaviour = tacticType switch
            {
                5 when roles[i] is PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender
                    => PlayerBehaviour.Defensive,
                5 when roles[i] is PlayerRole.CentralMidfielder or PlayerRole.LeftWinger or PlayerRole.RightWinger
                    => PlayerBehaviour.Defensive,
                3 when roles[i] is PlayerRole.LeftWinger or PlayerRole.RightWinger
                    => PlayerBehaviour.TowardsMiddle,
                4 when roles[i] is PlayerRole.LeftWinger or PlayerRole.RightWinger or PlayerRole.LeftForward or PlayerRole.RightForward
                    => PlayerBehaviour.TowardsWing,
                2 when roles[i] is PlayerRole.LeftDefender or PlayerRole.CentralDefender or PlayerRole.RightDefender
                    => PlayerBehaviour.Defensive,
                _ => baseBehaviour
            };

            result[i] = behaviour;
        }

        return result;
    }

    private static double SelectionScore(SimulationResult simulation, int tacticType, bool isHome)
    {
        double ourWin = isHome ? simulation.HomeWinPercentage : simulation.AwayWinPercentage;
        double ourLoss = isHome ? simulation.AwayWinPercentage : simulation.HomeWinPercentage;
        double ourGoals = isHome ? simulation.AverageHomeGoals : simulation.AverageAwayGoals;
        double opponentGoals = isHome ? simulation.AverageAwayGoals : simulation.AverageHomeGoals;

        double score = ourWin * 1.20 + simulation.DrawPercentage * 0.30 - ourLoss * 0.75 + ourGoals * 7.0 - opponentGoals * 5.0;
        if (tacticType == 1)
            score += simulation.HomeWinPercentage * 0.01;
        return score;
    }

    private static string BuildExplanation(string formation, TacticCandidate tactic, TeamRatings ours, TeamRatings opponent, SimulationResult simulation)
    {
        var points = new List<string>();
        double midfieldDelta = ours.Midfield - opponent.Midfield;
        double centralDefenceDelta = ours.CentralDefence - opponent.CentralAttack;
        double centralAttackDelta = ours.CentralAttack - opponent.CentralDefence;
        double wingAttackDelta = (ours.LeftAttack + ours.RightAttack) / 2.0 - (opponent.LeftDefence + opponent.RightDefence) / 2.0;

        if (midfieldDelta > .20) points.Add("Orta saha avantajımız var.");
        else if (midfieldDelta < -.20) points.Add("Rakibin orta saha üstünlüğü var; topa sahip olmayı artırmak önemli.");
        if (centralAttackDelta > .15) points.Add("Merkez hücumumuz rakibin merkez savunmasına karşı güçlü.");
        if (wingAttackDelta > .15) points.Add("Kanat hücumlarımız rakibin kanat savunmasına göre avantajlı.");
        if (centralDefenceDelta < -.15) points.Add("Rakibin merkez hücumuna karşı merkez savunmayı korumak gerekiyor.");
        points.Add($"{formation} dizilişi {tactic.Name.ToLowerInvariant()} ile simüle edildi.");
        points.Add($"{simulation.Simulations} simülasyonda beklenen skor {simulation.AverageHomeGoals:F2}-{simulation.AverageAwayGoals:F2}.");
        return string.Join(" ", points);
    }

    private sealed record TacticCandidate(string Name, int Type, int Level);
}
