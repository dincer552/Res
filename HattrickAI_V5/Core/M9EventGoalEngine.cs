using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

/// <summary>
/// M9 event -> goal layer based on the explicit event frequencies and average
/// scoring rates reported in Tables 4 and 5 of the 2026 Hattrick paper.
/// Unknown hidden inputs are never fabricated; unsupported mechanisms stay
/// pending and are reported in CalibrationStatus.
/// </summary>
public sealed class M9EventGoalEngine
{
    // Table 4: player-based event rate per match and average probability of a goal.
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

    // Table 5: team-based event rate per match and average scoring probability.
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
        double ownPossession,
        double opponentPossession,
        AdvancedTactic tactic,
        double creativeMultiplier,
        double opponentLinearPossession)
    {
        ArgumentNullException.ThrowIfNull(ownLineup);
        ArgumentNullException.ThrowIfNull(ownPlayers);

        var byId = ownPlayers.ToDictionary(p => p.Id);
        var slots = ownLineup.Slots.Where(s => s.PlayerId > 0 && byId.ContainsKey(s.PlayerId)).ToArray();
        var profiles = slots.Select(s => new SlotProfile(s.Code, s.Order, byId[s.PlayerId])).ToArray();

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
        var defenders = profiles.Count(x => IsDefender(x));
        var centralDefenders = profiles.Count(x => IsCentralDefender(x.Code));

        // §4.4: event generation is represented by a capped binomial process.
        // For a single-match expectation we use the reported mean event rates,
        // then scale by eligibility/support. Player-based events are mutually
        // exclusive, so absent categories are redistributed across remaining
        // categories rather than silently creating impossible events.
        var eligiblePlayerWeights = new List<EventWeight>
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

        var activeWeight = eligiblePlayerWeights.Sum(x => x.Weight);
        var playerSpecialGoals = 0.0;
        var ownGoalsFromOwnGoalEvents = 0.0;
        var playerEventRate = 0.0;
        var eventDetails = new List<M9EventContribution>();
        foreach (var item in eligiblePlayerWeights)
        {
            if (item.Weight <= 0 || activeWeight <= 0) continue;
            var expectedEvents = 0.8410 * (item.Weight / activeWeight); // Table 4 total player-SE mean.
            playerEventRate += expectedEvents;
            var expectedGoals = expectedEvents * item.GoalProbability;
            if (item.Name == "UnpredictableOwnGoal") ownGoalsFromOwnGoalEvents += expectedGoals;
            else playerSpecialGoals += expectedGoals;
            eventDetails.Add(new M9EventContribution(item.Name, expectedEvents, item.GoalProbability, expectedGoals));
        }

        // Team-based events are allocated using the paper's linear-possession Eq.5.
        var linearPossession = LinearPossession(ownPossession, opponentPossession);
        var ownTeamAllocation = Clamp01((linearPossession + 0.5 * Math.Clamp(opponentLinearPossession - 0.5, -0.5, 0.5)) * 0.5 + 0.25);

        // Corner-to-head probability from §4.4. For one offensive header this is
        // 27%, rising to 65% for >4. We interpolate between reported breakpoints.
        var cornerHeadProbability = CornerHeadShare(headOffensive);
        var cornerGoalProbability = (cornerHeadProbability * CornerHeadGoalRate) + ((1.0 - cornerHeadProbability) * CornerAnyoneGoalRate);
        var teamEvents = new[]
        {
            new TeamEventWeight("Corner", CornerRate, cornerGoalProbability, Math.Max(0.15, ownTeamAllocation)),
            new TeamEventWeight("ExperiencedForward", ExperiencedForwardRate, ExperiencedForwardGoalRate, forwards > 0 ? 1.0 : 0.0),
            new TeamEventWeight("InexperiencedDefender", InexperiencedDefenderRate, InexperiencedDefenderGoalRate, defenders > 0 ? 1.0 : 0.0),
            new TeamEventWeight("TiredDefender", TiredDefenderRate, TiredDefenderGoalRate, defenders > 0 ? 1.0 : 0.0)
        };

        var teamSpecialGoals = 0.0;
        var teamEventRate = 0.0;
        foreach (var item in teamEvents)
        {
            if (item.Eligibility <= 0) continue;
            var expectedEvents = item.Rate * item.Eligibility * (tactic == AdvancedTactic.Creative ? Math.Max(1.0, creativeMultiplier) : 1.0);
            teamEventRate += expectedEvents;
            var expectedGoals = expectedEvents * item.GoalProbability;
            teamSpecialGoals += expectedGoals;
            eventDetails.Add(new M9EventContribution(item.Name, expectedEvents, item.GoalProbability, expectedGoals));
        }

        // Tactic-specific opportunity generation is kept separate here. LS goal
        // conversion is intentionally not guessed because the paper provides a
        // plotted relationship rather than a published explicit scoring equation.
        var longShotExpected = tactic == AdvancedTactic.LongShots ? 0.0 : 0.0;
        var counterAttackExpected = 0.0;
        var pnfExpected = 0.0;
        var pdimSuppression = 0.0;
        var status = "PDF Tables 4-5 event rates applied; LS scoring, PNF/PDIM exact conversion, opponent specialty allocation and weather remain calibration-limited.";

        // Extra structural signal: Technical defenders/WBs may generate a technical
        // counterattack at 0.84% (2 defenders), 1% (3), 3.11% (>3) per missed normal attack.
        // This is an opportunity component; scoring is deliberately deferred.
        var technicalCaRate = technicalDefenders switch
        {
            >= 4 => 0.0311,
            3 => 0.0100,
            2 => 0.0084,
            _ => 0.0
        };

        var notes = new List<string>
        {
            "Player-based SE mean 0.841 and team-based SE mean 0.372 follow the paper baseline.",
            "Player-SE feasibility is driven by current lineup specialty/position counts.",
            "Opponent specialty counts are unavailable in the current pipeline, so opponent-specific PSE allocation is not fabricated.",
            $"Technical CA opportunity rate={technicalCaRate:P2}; goal conversion pending.",
            "Set-piece taker skill remains hidden from CHPP and is therefore handled conservatively in M9."
        };

        if (tactic == AdvancedTactic.Creative)
            notes.Add($"Play Creatively multiplier={creativeMultiplier:0.00}x from the paper's 2.37x-3.80x range; exact level mapping remains a V5 proxy.");

        return new M9EventGoalBreakdown(
            playerSpecialGoals,
            teamSpecialGoals,
            ownGoalsFromOwnGoalEvents,
            counterAttackExpected,
            longShotExpected,
            pnfExpected,
            pdimSuppression,
            playerEventRate,
            teamEventRate,
            technicalCaRate,
            eventDetails,
            status,
            string.Join(" ", notes));
    }

    private static double LinearPossession(double ownPossession, double opponentPossession)
    {
        var own = Math.Max(0.0, ownPossession) * 4.0 - 3.0;
        var opp = Math.Max(0.0, opponentPossession) * 4.0 - 3.0;
        own = Math.Max(0.0, own); opp = Math.Max(0.0, opp);
        var total = own + opp;
        return total <= 0 ? 0.5 : own / total;
    }

    private static double CornerHeadShare(int offensiveHeaders)
        => offensiveHeaders switch { <= 0 => 0.0, 1 => 0.27, 2 => 0.42, 3 => 0.51, 4 => 0.59, _ => 0.65 };

    private static int CountSpecial(IEnumerable<SlotProfile> profiles, PlayerSpecialty specialty, Func<SlotProfile, bool> predicate)
        => profiles.Count(x => x.Player.Specialty == specialty && predicate(x));

    private static bool IsOffensive(SlotProfile p) => IsWinger(p.Code) || IsInnerMidfielder(p.Code) || IsForward(p.Code);
    private static bool IsDefender(SlotProfile p) => IsCentralDefender(p.Code) || IsWingBack(p.Code) || p.Code == "GK";
    private static bool IsHeadOffensive(SlotProfile p) => IsForward(p.Code) || IsInnerMidfielder(p.Code);
    private static bool IsHeadDefensive(SlotProfile p) => IsDefender(p) || IsInnerMidfielder(p);
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

    private sealed record SlotProfile(string Code, PlayerOrder Order, Player Player);
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
