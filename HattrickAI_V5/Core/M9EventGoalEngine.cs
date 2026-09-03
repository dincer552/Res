using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9 event -> goal layer based on Tables 4 and 5 of the 2026 Hattrick paper.
/// Event counts follow the paper's Binomial(n,p) specification; hidden inputs remain explicit gaps.
/// </summary>
public sealed class M9EventGoalEngine
{
    private const double PlayerEventProbability = 0.8410;
    private const int PlayerEventTrials = 4;
    private const double TeamEventProbability = 0.3720;
    private const int TeamEventTrials = 5;
    private const double PlayerTeamOwnershipFallback = 0.5;

    private const double WingerAnyRate = 0.2163;
    private const double WingerAnyGoalRate = 0.4951;
    private const double TechnicalOverHeadRate = 0.1277;
    private const double TechnicalOverHeadGoalRate = 0.2937;
    private const double QuickRushRate = 0.1286;
    private const double QuickRushGoalRate = 0.3670;
    private const double QuickPassRate = 0.1219;
    private const double QuickPassGoalRate = 0.4387;
    private const double UnpredictableLongPassRate = 0.0687;
    private const double UnpredictableLongPassGoalRate = 0.4090;
    private const double UnpredictableScoreOwnRate = 0.0536;
    private const double UnpredictableScoreOwnGoalRate = 0.5822;
    private const double UnpredictableSpecialActionRate = 0.0560;
    private const double UnpredictableSpecialActionGoalRate = 0.4241;
    private const double UnpredictableMistakeRate = 0.0290;
    private const double UnpredictableMistakeGoalRate = 0.1816;
    private const double UnpredictableOwnGoalRate = 0.0392;
    private const double UnpredictableOwnGoalProbability = 0.1725;

    private const double ExperiencedForwardRate = 0.0400;
    private const double ExperiencedForwardGoalRate = 0.3704;
    private const double InexperiencedDefenderRate = 0.0392;
    private const double InexperiencedDefenderGoalRate = 0.1050;
    private const double TiredDefenderRate = 0.0004;
    private const double TiredDefenderGoalRate = 0.3432;
    private const double CornerRate = 0.2922;
    private const double CornerAnyoneGoalRate = 0.4849;
    private const double CornerHeadGoalRate = 0.5503;

    public M9EventGoalBreakdown Calculate(
        Lineup ownLineup,
        IReadOnlyList<Player> ownPlayers,
        double ownMidfieldRating,
        double opponentMidfieldRating,
        AdvancedTactic tactic,
        double creativeMultiplier)
    {
        ArgumentNullException.ThrowIfNull(ownLineup);
        ArgumentNullException.ThrowIfNull(ownPlayers);

        var byId = ownPlayers.ToDictionary(p => p.Id);
        var profiles = ownLineup.Slots
            .Where(s => s.PlayerId > 0 && byId.ContainsKey(s.PlayerId))
            .Select(s => new SlotProfile(s.Code, byId[s.PlayerId]))
            .ToArray();

        var quickOffensive = CountSpecial(profiles, PlayerSpecialty.Quick, IsOffensive);
        var quickDefenders = CountSpecial(profiles, PlayerSpecialty.Quick, IsDefender);
        var technicalOffensive = CountSpecial(profiles, PlayerSpecialty.Technical, IsOffensive);
        var technicalDefenders = CountSpecial(profiles, PlayerSpecialty.Technical, IsDefender);
        var headOffensive = CountSpecial(profiles, PlayerSpecialty.Head, IsHeadOffensive);
        var headDefensive = CountSpecial(profiles, PlayerSpecialty.Head, IsHeadDefensive);
        var unpredOffensive = CountSpecial(profiles, PlayerSpecialty.Unpredictable, IsOffensive);
        var unpredLongPass = CountSpecial(profiles, PlayerSpecialty.Unpredictable, IsUnpredictableLongPass);
        var unpredMistake = CountSpecial(profiles, PlayerSpecialty.Unpredictable, IsUnpredictableMistake);
        var unpredOwnGoal = CountSpecial(profiles, PlayerSpecialty.Unpredictable, IsUnpredictableOwnGoal);
        var forwards = profiles.Count(x => IsForward(x.Code));
        var defenders = profiles.Count(IsDefender);

        var eligiblePlayerEvents = new List<EventWeight>
        {
            new("Winger", WingerAnyRate, WingerAnyGoalRate, profiles.Any(x => IsWinger(x.Code)) ? 1.0 : 0.0),
            new("TechnicalOverHead", TechnicalOverHeadRate, TechnicalOverHeadGoalRate, technicalOffensive > 0 && headDefensive > 0 ? technicalOffensive + headDefensive : 0.0),
            new("QuickRush", QuickRushRate, QuickRushGoalRate, quickOffensive > 0 ? quickOffensive * QuickDefenceFactor(quickOffensive, quickDefenders) : 0.0),
            new("QuickPass", QuickPassRate, QuickPassGoalRate, quickOffensive > 0 ? quickOffensive * QuickDefenceFactor(quickOffensive, quickDefenders) : 0.0),
            new("UnpredictableLongPass", UnpredictableLongPassRate, UnpredictableLongPassGoalRate, unpredLongPass),
            new("UnpredictableScoreOwn", UnpredictableScoreOwnRate, UnpredictableScoreOwnGoalRate, unpredOffensive),
            new("UnpredictableSpecialAction", UnpredictableSpecialActionRate, UnpredictableSpecialActionGoalRate, unpredOffensive),
            new("UnpredictableMistake", UnpredictableMistakeRate, UnpredictableMistakeGoalRate, unpredMistake),
            new("UnpredictableOwnGoal", UnpredictableOwnGoalRate, UnpredictableOwnGoalProbability, unpredOwnGoal)
        };

        var activeWeight = eligiblePlayerEvents.Sum(x => x.Weight);
        var playerMultiplier = SpecialEventMultiplier(tactic, creativeMultiplier);
        // Paper §4.4: Player_SEs ~ Binomial(n=4, p=0.841).
        // The table rates distribute generated events among feasible event types.
        // Team ownership is 50/50 until opponent specialty counts are wired in.
        var playerEventBudget = activeWeight > 0
            ? PlayerEventTrials * PlayerEventProbability * playerMultiplier * PlayerTeamOwnershipFallback
            : 0.0;
        var playerSpecialGoals = 0.0;
        var ownGoalExpected = 0.0;
        var playerEventRate = 0.0;
        var contributions = new List<M9EventContribution>();
        foreach (var item in eligiblePlayerEvents)
        {
            if (item.Weight <= 0 || activeWeight <= 0) continue;
            var expectedEvents = playerEventBudget * (item.Weight / activeWeight);
            playerEventRate += expectedEvents;
            var expectedGoals = expectedEvents * item.GoalProbability;
            if (item.Name == "UnpredictableOwnGoal") ownGoalExpected += expectedGoals;
            else playerSpecialGoals += expectedGoals;
            contributions.Add(new M9EventContribution(item.Name, expectedEvents, item.GoalProbability, expectedGoals));
        }

        // §4.4 Eq.5: team-based SE allocation uses linear possession.
        // Team_SEs ~ Binomial(n=5, p=0.372), expected total event count = 1.86.
        var linearPossession = LinearPossession(ownMidfieldRating, opponentMidfieldRating);
        var teamMultiplier = SpecialEventMultiplier(tactic, creativeMultiplier);
        var cornerHeadProbability = CornerHeadShare(headOffensive);
        var cornerGoalProbability = cornerHeadProbability * CornerHeadGoalRate + (1.0 - cornerHeadProbability) * CornerAnyoneGoalRate;
        var teamEvents = new[]
        {
            new TeamEventWeight("Corner", CornerRate, cornerGoalProbability, 1.0),
            new TeamEventWeight("ExperiencedForward", ExperiencedForwardRate, ExperiencedForwardGoalRate, forwards > 0 ? 1.0 : 0.0),
            new TeamEventWeight("InexperiencedDefender", InexperiencedDefenderRate, InexperiencedDefenderGoalRate, defenders > 0 ? 1.0 : 0.0),
            new TeamEventWeight("TiredDefender", TiredDefenderRate, TiredDefenderGoalRate, defenders > 0 ? 1.0 : 0.0)
        };
        var activeTeamRate = teamEvents.Where(x => x.Eligibility > 0).Sum(x => x.Rate);
        var teamEventBudget = activeTeamRate > 0 ? TeamEventTrials * TeamEventProbability * linearPossession * teamMultiplier : 0.0;
        var teamSpecialGoals = 0.0;
        var teamEventRate = 0.0;
        foreach (var item in teamEvents)
        {
            if (item.Eligibility <= 0 || activeTeamRate <= 0) continue;
            var expectedEvents = teamEventBudget * (item.Rate / activeTeamRate);
            teamEventRate += expectedEvents;
            var expectedGoals = expectedEvents * item.GoalProbability;
            teamSpecialGoals += expectedGoals;
            contributions.Add(new M9EventContribution(item.Name, expectedEvents, item.GoalProbability, expectedGoals));
        }

        var technicalCaRate = technicalDefenders switch
        {
            >= 4 => 0.0311,
            3 => 0.0100,
            2 => 0.0084,
            _ => 0.0
        };

        var notes = new List<string>
        {
            "Player-based SEs follow the paper Binomial(n=4,p=0.841) expectation; event types are redistributed over feasible residual types.",
            "Player-event team ownership is currently neutral 50/50 because opponent specialty counts are not wired into M9.",
            "Team-based SEs follow the paper Binomial(n=5,p=0.372) expectation and Eq.5 linear possession.",
            $"Technical CA opportunity rate={technicalCaRate:P2}; goal conversion remains pending.",
            "Long Shot scoring probability is not invented because the paper provides a plotted relationship rather than a published explicit equation.",
            "Set-piece taker skill is hidden from CHPP and therefore remains outside exact event resolution.",
            "PNF/PDIM opportunity resolution remains a separate pending layer because PNF scoring and PDIM multi-player suppression require additional hidden inputs/calibration."
        };
        if (tactic == AdvancedTactic.Creative)
            notes.Add($"Play Creatively own-event multiplier={teamMultiplier:0.00}x; paper range is 2.37x-3.80x and exact V5-level mapping remains a proxy.");

        return new M9EventGoalBreakdown(
            playerSpecialGoals,
            teamSpecialGoals,
            ownGoalExpected,
            0.0,
            0.0,
            0.0,
            0.0,
            playerEventRate,
            teamEventRate,
            technicalCaRate,
            contributions,
            "PDF Tables 4-5 + Binomial event-count structure; opponent specialty, LS scoring, PNF/PDIM and hidden set-piece inputs pending.",
            string.Join(" ", notes));
    }

    private static double LinearPossession(double ownMidfieldRating, double opponentMidfieldRating)
    {
        var own = Math.Max(0.0, ownMidfieldRating) * 4.0 - 3.0;
        var opp = Math.Max(0.0, opponentMidfieldRating) * 4.0 - 3.0;
        own = Math.Max(0.0, own); opp = Math.Max(0.0, opp);
        var total = own + opp;
        return total <= 0 ? 0.5 : own / total;
    }

    private static double SpecialEventMultiplier(AdvancedTactic tactic, double creativeMultiplier)
        => tactic == AdvancedTactic.Creative ? Math.Max(1.0, creativeMultiplier) : 1.0;

    private static double CornerHeadShare(int offensiveHeaders)
        => offensiveHeaders switch { <= 0 => 0.0, 1 => 0.27, 2 => 0.42, 3 => 0.51, 4 => 0.59, _ => 0.65 };

    private static int CountSpecial(IEnumerable<SlotProfile> profiles, PlayerSpecialty specialty, Func<SlotProfile, bool> predicate)
        => profiles.Count(x => x.Player.Specialty == specialty && predicate(x));

    private static bool IsOffensive(SlotProfile p) => IsWinger(p.Code) || IsInnerMidfielder(p.Code) || IsForward(p.Code);
    private static bool IsDefender(SlotProfile p) => IsCentralDefender(p.Code) || IsWingBack(p.Code) || p.Code == "GK";
    private static bool IsHeadOffensive(SlotProfile p) => IsForward(p.Code) || IsInnerMidfielder(p.Code);
    private static bool IsHeadDefensive(SlotProfile p) => IsDefender(p) || IsInnerMidfielder(p.Code);
    private static bool IsUnpredictableLongPass(SlotProfile p) => IsDefender(p);
    private static bool IsUnpredictableMistake(SlotProfile p) => IsDefender(p) || IsInnerMidfielder(p.Code);
    private static bool IsUnpredictableOwnGoal(SlotProfile p) => IsWinger(p.Code) || IsForward(p.Code);
    private static bool IsWinger(string code) => code is "W-L" or "W-R";
    private static bool IsInnerMidfielder(string code) => code is "IM-L" or "IM-C" or "IM-R";
    private static bool IsForward(string code) => code is "FW-L" or "FW-C" or "FW-R";
    private static bool IsCentralDefender(string code) => code is "DEF-C" or "DEF-CL" or "DEF-CR";
    private static bool IsWingBack(string code) => code is "DEF-L" or "DEF-R";
    private static double QuickDefenceFactor(int quickOffensive, int quickDefenders)
    {
        if (quickOffensive <= 0) return 0.0;
        var support = quickOffensive / (double)(quickOffensive + quickDefenders);
        return 0.65 + 0.35 * support;
    }

    private sealed record SlotProfile(string Code, Player Player);
    private sealed record EventWeight(string Name, double Rate, double GoalProbability, double Weight);
    private sealed record TeamEventWeight(string Name, double Rate, double GoalProbability, double Eligibility);
}

public sealed record M9EventContribution(string Event, double ExpectedEvents, double GoalProbability, double ExpectedGoals);

public sealed record M9EventGoalBreakdown(
    double PlayerBasedSpecialEventGoals,
    double TeamBasedSpecialEventGoals,
    double ExpectedGoalsConcededFromOwnGoalEvents,
    double CounterAttackGoals,
    double LongShotGoals,
    double PowerfulNormalForwardGoals,
    double PressingSuppressionSignal,
    double ExpectedPlayerBasedEvents,
    double ExpectedTeamBasedEvents,
    double TechnicalCounterAttackOpportunityRate,
    IReadOnlyList<M9EventContribution> Contributions,
    string CalibrationStatus,
    string Notes)
{
    public static M9EventGoalBreakdown Empty => new(0,0,0,0,0,0,0,0,0,0,Array.Empty<M9EventContribution>(),"Not calculated","No player/event context supplied.");
    public double NetSpecialEventGoalContribution => PlayerBasedSpecialEventGoals + TeamBasedSpecialEventGoals - ExpectedGoalsConcededFromOwnGoalEvents;
}
