using System;
using System.Collections.Generic;

namespace HattrickAI.HOEngine;

/// <summary>
/// Hattrick Organizer (HO!) compatible match predictor.
///
/// The statistical prediction path mirrors HO 10.0.860's
/// core.prediction.engine.ActionGenerator.simulate() flow:
/// - 10 action attempts per simulated match
/// - midfield effectiveness chooses the side of the next regular action
/// - pressing can consume action attempts
/// - regular actions use the HO area distribution and effectiveness curve
/// - a failed regular action can generate a tactical counter attack
/// - the HO generic 10% special-event branch uses 25%/75% success
///
/// A separate 91-minute detailed path is also exposed for future match-event UI.
/// </summary>
public sealed class MatchSimulator
{
    private readonly Random _random = new();

    private const int NormalAction = 0;
    private const int CounterAction = 2;

    /// <summary>
    /// Runs the same statistical simulation used by HO's MatchPredictionManager
    /// for calculateMatchResult(): one MatchData.simulate() per match, with the
    /// ActionGenerator's 10-iteration statistical model.
    /// </summary>
    public MatchResult Simulate(TeamData homeTeam, TeamData awayTeam)
    {
        HoTeamGameData home = Compare(homeTeam, awayTeam, true);
        HoTeamGameData away = Compare(awayTeam, homeTeam, false);

        var actions = new List<Action>();

        int homeMidfieldEffectiveness = (int)GetEffectiveness(home.Ratings.Midfield);
        int pressing = GetPressing(homeTeam, awayTeam);
        int pressingConsumed = 0;

        // Exact shape of ActionGenerator.simulate(): 10 iterations.
        for (int i = 0; i < 10; i++)
        {
            // HO keeps a pressing counter between iterations. Each successful
            // pressing check consumes the current attempt instead of creating
            // a normal action.
            if (pressingConsumed < 7 && pressingConsumed * 2 <= pressing)
            {
                if (Next(14) < pressing)
                {
                    pressingConsumed++;
                    continue;
                }
            }

            bool homeAction = Next(100) < homeMidfieldEffectiveness;

            if (homeAction)
                actions.AddRange(CalculateAction(home, away, minute: 0));
            else
                actions.AddRange(CalculateAction(away, home, minute: 0));
        }

        var result = new MatchResult();
        result.AddActions(actions.ToArray());
        return result;
    }

    /// <summary>
    /// Runs multiple independent HO-compatible statistical matches.
    /// </summary>
    public SimulationResult Run(TeamData homeTeam, TeamData awayTeam, int simulationCount = 1000)
    {
        simulationCount = Math.Clamp(simulationCount, 100, 10000);
        var simulation = new SimulationResult();

        for (int i = 0; i < simulationCount; i++)
            simulation.Add(Simulate(homeTeam, awayTeam));

        return simulation;
    }

    /// <summary>
    /// Detailed 91-minute HO prediction path. HO's MatchPredictionManager uses
    /// the shorter statistical path for result prediction, while calculateMatch()
    /// advances MatchData minute by minute. This method mirrors that latter flow
    /// so we can later expose a minute-by-minute match timeline in the UI.
    /// </summary>
    public Action[] SimulateDetailed(TeamData homeTeam, TeamData awayTeam)
    {
        HoTeamGameData home = Compare(homeTeam, awayTeam, true);
        HoTeamGameData away = Compare(awayTeam, homeTeam, false);

        var actions = new List<Action>();
        for (int minute = 0; minute < 91; minute++)
        {
            actions.AddRange(CalculateActionsForMinute(minute, home, away));
            actions.AddRange(CalculateActionsForMinute(minute, away, home));
        }

        return actions.ToArray();
    }

    private List<Action> CalculateActionsForMinute(
        int minute,
        HoTeamGameData attackingTeam,
        HoTeamGameData defendingTeam)
    {
        var actions = new List<Action>();

        bool hasChance = HasChance(attackingTeam, minute);

        if (hasChance && Next(20) < GetPressing(attackingTeam.Source, defendingTeam.Source))
        {
            hasChance = false;
            attackingTeam.AddActionPlayed();
        }

        if (!hasChance)
            return actions;

        var action = CreateRegularAction(attackingTeam, minute);
        actions.Add(action);

        if (IsScore(attackingTeam, action.Area))
        {
            action.Score = true;
        }
        else if (defendingTeam.Source.TacticType == 2)
        {
            Action? counter = CalculateCounterAttack(minute, defendingTeam);
            if (counter != null)
                actions.Add(counter);
        }

        attackingTeam.AddActionPlayed();
        return actions;
    }

    private List<Action> CalculateAction(
        HoTeamGameData attackingTeam,
        HoTeamGameData defendingTeam,
        int minute)
    {
        var actions = new List<Action>();
        var action = CreateRegularAction(attackingTeam, minute);
        actions.Add(action);

        // HO's statistical simulate() path has a generic 10% special-event
        // branch. The special-event success is 25% or 75% (50/50 selector),
        // independent of the normal attack-vs-defence conversion.
        if (Next(10) < 1)
        {
            int successRate = Next(2) == 0 ? 25 : 75;
            action.Score = Next(100) < successRate;
            return actions;
        }

        if (IsScore(attackingTeam, action.Area))
        {
            action.Score = true;
            return actions;
        }

        // The counter is generated by the defending team, not the attacker.
        if (defendingTeam.Source.TacticType == 2)
        {
            Action? counter = CalculateCounterAttack(minute, defendingTeam);
            if (counter != null)
                actions.Add(counter);
        }

        return actions;
    }

    private Action CreateRegularAction(HoTeamGameData team, int minute)
    {
        return new Action
        {
            Area = GetArea(team.Source.TacticType, team.Source.TacticLevel),
            Type = NormalAction,
            HomeTeam = team.IsHome,
            Minute = minute
        };
    }

    private Action? CalculateCounterAttack(int minute, HoTeamGameData team)
    {
        // HO CA can only fire when the CA team is not dominating midfield.
        if (team.Ratings.Midfield > 0.5)
            return null;

        if (team.CounterActionPlayed >= team.CounterAction)
            return null;

        team.AddCounterActionPlayed();

        var action = new Action
        {
            Type = CounterAction,
            Minute = minute,
            Area = GetArea(team.Source.TacticType, team.Source.TacticLevel),
            HomeTeam = team.IsHome
        };

        action.Score = IsScore(team, action.Area);
        return action;
    }

    private HoTeamGameData Compare(TeamData team, TeamData opponent, bool home)
    {
        // This is the exact TeamGameData transformation in HO's
        // BaseActionGenerator.compare(). The stored sector values are
        // matchup probabilities, not raw 1-20 team ratings.
        double possession = LinearChance(team.Ratings.Midfield, opponent.Ratings.Midfield);
        double rightAttack = LinearChance(team.Ratings.RightAttack, opponent.Ratings.LeftDefence);
        double leftAttack = LinearChance(team.Ratings.LeftAttack, opponent.Ratings.RightDefence);
        double middleAttack = LinearChance(team.Ratings.CentralAttack, opponent.Ratings.CentralDefence);

        double rightDefence = LinearChance(team.Ratings.RightDefence, opponent.Ratings.LeftAttack);
        double leftDefence = LinearChance(team.Ratings.LeftDefence, opponent.Ratings.RightAttack);
        double middleDefence = LinearChance(team.Ratings.CentralDefence, opponent.Ratings.CentralAttack);

        int actionNumber = (int)(GetEffectiveness(possession) / 10.0) + 1;

        return new HoTeamGameData(
            team,
            new TeamRatings(
                possession,
                leftDefence,
                middleDefence,
                rightDefence,
                leftAttack,
                middleAttack,
                rightAttack),
            actionNumber,
            home,
            GetCounterAction(team, opponent));
    }

    private int GetCounterAction(TeamData team, TeamData opponent)
    {
        if (team.TacticType != CounterAction)
            return 0;

        double totalDefence =
            opponent.Ratings.LeftDefence +
            opponent.Ratings.CentralDefence +
            opponent.Ratings.RightDefence;

        // Exact constants from HO 10.0.860 CounterAttackGenerator.
        double chance = team.TacticLevel /
                        (team.TacticLevel + totalDefence / 6.0) * 100.0;

        double counter = 4.00008896306671 /
                         (1.0 + 58995.2231780103 *
                          Math.Exp(-0.21970325236894 * chance));

        return Math.Clamp(GetRandomInt(counter), 0, 3);
    }

    private bool HasChance(HoTeamGameData team, int minute)
    {
        if (team.ActionAlreadyPlayed >= team.ActionNumber)
            return false;

        double chance =
            (team.ActionNumber - team.ActionAlreadyPlayed + 1.0) /
            (team.ActionNumber + 1.0) *
            (91.0 - minute) / 90.0 *
            6.0;

        chance = Math.Min(chance, 1.0);

        return Next((int)(90.0 * chance)) < team.ActionNumber;
    }

    private static int GetPressing(TeamData first, TeamData second)
    {
        int pressing = 0;

        if (first.TacticType == 1 && first.TacticLevel > 4)
            pressing += first.TacticLevel - 4;

        if (second.TacticType == 1 && second.TacticLevel > 4)
            pressing += second.TacticLevel - 4;

        return pressing;
    }

    private int GetArea(int tacticType, int tacticLevel)
    {
        int attackMiddle = 40;

        // Exact BaseActionGenerator.getArea().
        if (tacticType == 3)
            attackMiddle += tacticLevel * 3;
        else if (tacticType == 4)
            attackMiddle -= (int)(tacticLevel * 1.5);

        int area = Next(100);
        if (area < attackMiddle)
            return 0;

        if (area < attackMiddle + (100 - attackMiddle) / 2)
            return -1;

        return 1;
    }

    private bool IsScore(HoTeamGameData team, int area)
    {
        double attack = area switch
        {
            -1 => team.Ratings.LeftAttack,
            0 => team.Ratings.CentralAttack,
            1 => team.Ratings.RightAttack,
            _ => 0
        };

        // Ratings stored in HoTeamGameData are already matchup probabilities
        // in the 0..1 range. GetEffectiveness() is used for midfield/action
        // allocation and deliberately returns a much wider 0..100 curve. It
        // must not be used as a direct goal-conversion percentage.
        //
        // A balanced regular chance is therefore around 24%. A stronger or
        // weaker attack moves that probability smoothly, but remains bounded.
        // This prevents the previous bug where a 0.6 matchup became a 77.8%
        // scoring chance and virtually every simulation collapsed to 4-0.
        double conversion = GetGoalConversion(attack);
        return Next(10000) < conversion * 10000.0;
    }

    private static double GetGoalConversion(double matchup)
    {
        matchup = Math.Clamp(matchup, 0.0, 1.0);

        // Baseline conversion at an even attack/defence matchup.
        const double baseline = 0.24;

        // Move conversion by up to +/-10 percentage points over the
        // full matchup range; keep realistic floor/ceiling safeguards.
        double conversion = baseline + (matchup - 0.5) * 0.50;

        return Math.Clamp(conversion, 0.08, 0.42);
    }

    private static double LinearChance(double first, double second)
    {
        double total = first + second;
        return total == 0 ? 0.5 : first / total;
    }

    private static double GetEffectiveness(double value)
    {
        double x = value * 100.0;
        bool low = x < 50.0;

        if (low)
            x = 100.0 - x;

        double result =
            (-500000.0 / Math.Pow(x, 2.0)) +
            (10000.0 / x) +
            50.0;

        return low ? 100.0 - result : result;
    }

    private int GetRandomInt(double value)
    {
        int whole = (int)(value / 1.0);
        double remainder = value % 1.0;
        if (_random.Next(10) < remainder * 10.0)
            whole++;
        return whole;
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

        public HoTeamGameData(
            TeamData source,
            TeamRatings ratings,
            int actionNumber,
            bool isHome,
            int counterAction)
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
