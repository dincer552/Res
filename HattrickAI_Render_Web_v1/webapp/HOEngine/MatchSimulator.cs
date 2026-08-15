using System;
using System.Collections.Generic;

namespace HattrickAI.HOEngine;

/// <summary>
/// C# port of HO!'s core prediction engine ActionGenerator/BaseActionGenerator.
/// The important point is that scoring uses HO!'s effectiveness curve directly;
/// there is no extra invented goal-conversion layer.
/// </summary>
public sealed class MatchSimulator
{
    private readonly Random _random = new();

    public MatchResult Simulate(TeamData homeTeam, TeamData awayTeam)
    {
        var home = Compare(homeTeam, awayTeam, true);
        var away = Compare(awayTeam, homeTeam, false);
        var actions = new List<Action>();

        int midfieldPossession = (int)GetEffectiveness(home.Ratings.Midfield);
        int pressing = GetPressing(homeTeam, awayTeam);
        int successfulPressing = 0;

        // HO ActionGenerator.simulate(): exactly 10 action attempts.
        for (int i = 0; i < 10; i++)
        {
            if (successfulPressing < 7 && successfulPressing * 2 <= pressing)
            {
                if (Next(14) < pressing)
                {
                    successfulPressing++;
                    continue;
                }
            }

            bool homeAction = Next(100) < midfieldPossession;
            actions.AddRange(CalculateAction(homeAction ? home : away, homeAction ? away : home));
        }

        var result = new MatchResult();
        result.AddActions(actions.ToArray());
        return result;
    }

    public SimulationResult Run(TeamData homeTeam, TeamData awayTeam, int simulationCount = 1000)
    {
        simulationCount = Math.Clamp(simulationCount, 100, 10000);
        var result = new SimulationResult();
        for (int i = 0; i < simulationCount; i++)
            result.Add(Simulate(homeTeam, awayTeam));
        return result;
    }

    public Action[] SimulateDetailed(TeamData homeTeam, TeamData awayTeam)
    {
        var home = Compare(homeTeam, awayTeam, true);
        var away = Compare(awayTeam, homeTeam, false);
        var actions = new List<Action>();

        for (int minute = 0; minute < 91; minute++)
        {
            actions.AddRange(CalculateActions(minute, home, away));
            actions.AddRange(CalculateActions(minute, away, home));
        }

        return actions.ToArray();
    }

    private List<Action> CalculateActions(int minute, HoTeamGameData team, HoTeamGameData opponent)
    {
        var actions = new List<Action>();
        bool hasChance = HasChance(team, minute);

        if (hasChance)
        {
            if (Next(20) < GetPressing(team.Source, opponent.Source))
            {
                hasChance = false;
                team.AddActionPlayed();
            }
        }

        if (!hasChance)
            return actions;

        var action = new Action
        {
            Area = GetArea(team.Source.TacticType, team.Source.TacticLevel),
            Minute = minute,
            Type = 0,
            HomeTeam = team.IsHome
        };
        actions.Add(action);

        if (IsScore(team, action.Area))
        {
            action.Score = true;
        }
        else if (opponent.Source.TacticType == 2)
        {
            var counter = CalculateCounterAttack(minute, opponent);
            if (counter != null)
                actions.Add(counter);
        }

        team.AddActionPlayed();
        return actions;
    }

    private List<Action> CalculateAction(HoTeamGameData team, HoTeamGameData opponent)
    {
        var actions = new List<Action>();
        var action = new Action
        {
            Area = GetArea(team.Source.TacticType, team.Source.TacticLevel),
            Type = 0,
            HomeTeam = team.IsHome
        };
        actions.Add(action);

        // HO generic special-event branch: 10%, then 25% or 75% success.
        if (Next(10) < 1)
        {
            int successRate = Next(2) == 0 ? 25 : 75;
            action.Score = Next(100) < successRate;
            return actions;
        }

        if (IsScore(team, action.Area))
        {
            action.Score = true;
        }
        else if (opponent.Source.TacticType == 2)
        {
            var counter = CalculateCounterAttack(0, opponent);
            if (counter != null)
                actions.Add(counter);
        }

        return actions;
    }

    private Action? CalculateCounterAttack(int minute, HoTeamGameData team)
    {
        // HO CounterAttackGenerator: no CA when the CA team wins midfield.
        if (team.Ratings.Midfield > 0.5)
            return null;

        if (team.CounterActionPlayed >= team.CounterAction)
            return null;

        team.AddCounterActionPlayed();

        var action = new Action
        {
            Type = 2,
            Minute = minute,
            Area = GetArea(team.Source.TacticType, team.Source.TacticLevel),
            HomeTeam = team.IsHome
        };
        action.Score = IsScore(team, action.Area);
        return action;
    }

    private HoTeamGameData Compare(TeamData team, TeamData opponent, bool home)
    {
        double possession = LinearChance(team.Ratings.Midfield, opponent.Ratings.Midfield);
        double rightAttack = LinearChance(team.Ratings.RightAttack, opponent.Ratings.LeftDefence);
        double leftAttack = LinearChance(team.Ratings.LeftAttack, opponent.Ratings.RightDefence);
        double middleAttack = LinearChance(team.Ratings.CentralAttack, opponent.Ratings.CentralDefence);
        double rightDefence = LinearChance(team.Ratings.RightDefence, opponent.Ratings.LeftAttack);
        double leftDefence = LinearChance(team.Ratings.LeftDefence, opponent.Ratings.RightAttack);
        double middleDefence = LinearChance(team.Ratings.CentralDefence, opponent.Ratings.CentralAttack);

        int actionNumber = (int)(GetEffectiveness(possession) / 10.0) + 1;
        int counterAction = GetCounterAction(team, opponent);

        return new HoTeamGameData(
            team,
            new TeamRatings(possession, leftDefence, middleDefence, rightDefence,
                leftAttack, middleAttack, rightAttack),
            actionNumber,
            home,
            counterAction);
    }

    private int GetCounterAction(TeamData team, TeamData opponent)
    {
        if (team.TacticType != 2)
            return 0;

        int ability = team.TacticLevel;
        double def = opponent.Ratings.LeftDefence + opponent.Ratings.CentralDefence + opponent.Ratings.RightDefence;
        double counterIndex = ability / (def / 6.0 + ability) * 100.0;
        double ca = 4.00008896306671 /
                    (1.0 + 58995.2231780103 * Math.Exp(-0.21970325236894 * counterIndex));

        return Math.Clamp(GetRandomInt(ca), 0, 3);
    }

    private bool HasChance(HoTeamGameData team, int minute)
    {
        if (team.ActionAlreadyPlayed >= team.ActionNumber)
            return false;

        double factor =
            ((team.ActionNumber - team.ActionAlreadyPlayed + 1d) /
             (team.ActionNumber + 1d) * (91 - minute)) / 90d * 6d;
        factor = Math.Min(1d, factor);

        return Next((int)(90d * factor)) < team.ActionNumber;
    }

    private static int GetPressing(TeamData first, TeamData second)
    {
        int level = 0;
        if (first.TacticType == 1 && first.TacticLevel > 4)
            level += first.TacticLevel - 4;
        if (second.TacticType == 1 && second.TacticLevel > 4)
            level += second.TacticLevel - 4;
        return level;
    }

    private int GetArea(int tactic, int level)
    {
        int attackMiddle = 40;
        if (tactic == 3)
            attackMiddle += level * 3;
        if (tactic == 4)
            attackMiddle = (int)(attackMiddle - level * 1.5);

        int area = Next(100);
        if (area < attackMiddle)
            return 0;
        if (area < ((100 - attackMiddle) / 2 + attackMiddle))
            return -1;
        return 1;
    }

    private bool IsScore(HoTeamGameData team, int area)
    {
        double chance = area switch
        {
            -1 => team.Ratings.LeftAttack,
            0 => team.Ratings.CentralAttack,
            1 => team.Ratings.RightAttack,
            _ => 0
        };

        // IMPORTANT: this is HO's exact BaseActionGenerator.isScore().
        // Do not replace it with a custom baseline/floor/ceiling conversion.
        int effectiveness = (int)GetEffectiveness(chance);
        return Next(100) < effectiveness;
    }

    private static double LinearChance(double first, double second)
        => first / (first + second);

    /// <summary>Exact BaseActionGenerator.getEffectiveness().</summary>
    private static double GetEffectiveness(double value)
    {
        double x = value * 100d;
        bool low = false;
        if (x < 50d)
        {
            low = true;
            x = 100d - x;
        }

        double v = -500000d / Math.Pow(x, 2d) + 10000d / x + 50d;
        return low ? 100d - v : v;
    }

    private int GetRandomInt(double number)
    {
        int intPart = (int)number;
        double decimalPart = number % 1.0;
        if (Next(10) < decimalPart * 10)
            intPart++;
        return intPart;
    }

    private int Next(int max) => max <= 0 ? 0 : _random.Next(max);

    private sealed class HoTeamGameData
    {
        public TeamData Source { get; }
        public TeamRatings Ratings { get; }
        public int ActionNumber { get; }
        public bool IsHome { get; }
        public int CounterAction { get; }
        public int ActionAlreadyPlayed { get; private set; }
        public int CounterActionPlayed { get; private set; }

        public HoTeamGameData(TeamData source, TeamRatings ratings, int actionNumber, bool isHome, int counterAction)
        {
            Source = source;
            Ratings = ratings;
            ActionNumber = actionNumber;
            IsHome = isHome;
            CounterAction = counterAction;
        }

        public void AddActionPlayed() => ActionAlreadyPlayed++;
        public void AddCounterActionPlayed() => CounterActionPlayed++;
    }
}
