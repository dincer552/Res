using HattrickAI.HOEngine;

namespace HattrickAI.FormationTests;

public static class IndividualOrderOptimizationTests
{
    public static int RunAll()
    {
        try
        {
            var players = BuildPlayers();
            var optimizer = new IndividualOrderOptimizer();
            var opponent = new TeamRatings(6.0, 8.0, 8.0, 5.0, 7.0, 7.0, 6.0);

            var behaviours = optimizer.Optimize(players, "3-4-3", new TeamMatchContext(), opponent);

            if (behaviours.Count != 11)
                throw new Exception($"Expected 11 behavior slots, got {behaviours.Count}.");

            var roles = LineupRatingEngine.GetRoles("3-4-3");
            for (int i = 0; i < roles.Length; i++)
            {
                if (roles[i] == PlayerRole.CentralDefender &&
                    behaviours[i] == PlayerBehaviour.Defensive)
                {
                    throw new Exception("Central defender received an invalid Defensive individual order.");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"IndividualOrderOptimizationTests FAILED: {ex.Message}");
            return 1;
        }
    }

    private static List<PlayerData> BuildPlayers()
    {
        var players = new List<PlayerData>();

        players.Add(Create("GK", keeper: 12));
        players.Add(Create("CB1", defending: 12, playmaking: 6));
        players.Add(Create("CB2", defending: 11, playmaking: 7));
        players.Add(Create("CB3", defending: 10, playmaking: 8));
        players.Add(Create("LW", playmaking: 10, winger: 14, passing: 10));
        players.Add(Create("RW", playmaking: 11, winger: 13, passing: 10));
        players.Add(Create("IM1", playmaking: 13, winger: 9, passing: 11, defending: 8));
        players.Add(Create("IM2", playmaking: 12, winger: 8, passing: 12, defending: 9));
        players.Add(Create("F1", scoring: 13, passing: 9, winger: 7));
        players.Add(Create("F2", scoring: 12, passing: 10, winger: 7));
        players.Add(Create("F3", scoring: 11, passing: 9, winger: 8));

        return players;
    }

    private static PlayerData Create(
        string name,
        int keeper = 1,
        int defending = 5,
        int playmaking = 5,
        int winger = 5,
        int passing = 5,
        int scoring = 5)
    {
        return new PlayerData
        {
            Name = name,
            Keeper = keeper,
            Defending = defending,
            Playmaking = playmaking,
            Winger = winger,
            Passing = passing,
            Scoring = scoring,
            Stamina = 7,
            Form = 7,
            Experience = 7,
            Loyalty = 20
        };
    }
}
