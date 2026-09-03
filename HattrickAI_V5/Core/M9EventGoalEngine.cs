using System;
using System.Collections.Generic;
using System.Linq;

namespace HattrickAI.V5.Core;

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

    public M9EventGoalBreakdown Calculate(Lineup ownLineup, IReadOnlyList<Player> ownPlayers, double ownMidfieldRating, double opponentMidfieldRating, AdvancedTactic tactic, double creativeMultiplier,
        double ownNormalChanceVolume = 10.0, double opponentNormalChanceVolume = 10.0, double ownNormalGoalProbability = 0.5, double opponentNormalGoalProbability = 0.5, int opponentCentralDefenders = 3)
    {
        ArgumentNullException.ThrowIfNull(ownLineup);
        ArgumentNullException.ThrowIfNull(ownPlayers);
        var byId = ownPlayers.ToDictionary(p => p.Id);
        var profiles = ownLineup.Slots.Where(s => s.PlayerId > 0 && byId.ContainsKey(s.PlayerId)).Select(s => new SlotProfile(s.Code, byId[s.PlayerId])).ToArray();
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
        var pnfCount = CountSpecial(profiles, PlayerSpecialty.Powerful, IsForward);
        var pdimCount = CountSpecial(profiles, PlayerSpecialty.Powerful, IsInnerMidfielder);

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
        var playerEventBudget = activeWeight > 0 ? PlayerEventTrials * PlayerEventProbability * playerMultiplier * PlayerTeamOwnershipFallback : 0.0;
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
            if (item.Name == "UnpredictableOwnGoal") ownGoalExpected += expectedGoals; else playerSpecialGoals += expectedGoals;
            contributions.Add(new M9EventContribution(item.Name, expectedEvents, item.GoalProbability, expectedGoals));
        }

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
            teamSpecialGoals += expectedEvents * item.GoalProbability;
            contributions.Add(new M9EventContribution(item.Name, expectedEvents, item.GoalProbability, expectedEvents * item.GoalProbability));
        }

        var technicalCaRate = technicalDefenders switch { >= 4 => 0.0311, 3 => 0.0100, 2 => 0.0084, _ => 0.0 };
        var pdimSuppression = PdimSuppressionRate(pdimCount);
        var missedOpponentNormals = Math.Max(0.0, opponentNormalChanceVolume * (1.0 - Math.Clamp(opponentNormalGoalProbability, 0.0, 1.0)));
        var pnfConversion = PnfConversionRate(pnfCount, opponentCentralDefenders);
        var pnfExtraAttacks = missedOpponentNormals * pnfConversion;
        var pnfGoalProbability = Math.Clamp(ownNormalGoalProbability, 0.0, 1.0);
        var pnfGoals = pnfExtraAttacks * pnfGoalProbability;
        if (pnfCount > 0) contributions.Add(new M9EventContribution("PowerfulNormalForward", pnfExtraAttacks, pnfGoalProbability, pnfGoals));
        if (pdimCount > 0) contributions.Add(new M9EventContribution("PowerfulDefensiveInnerMidfielder", opponentNormalChanceVolume * pdimSuppression, pdimSuppression, 0.0));

        return new M9EventGoalBreakdown(playerSpecialGoals, teamSpecialGoals, ownGoalExpected, 0.0, 0.0, pnfGoals, pdimSuppression, playerEventRate, teamEventRate, technicalCaRate, contributions,
            "PDF Tables 4-5 + Appendix C hooks; baseline preserved, PNF/PDIM enabled, LS scoring/hidden inputs pending.",
            $"PNF={pnfCount}; opponent CDs={Math.Clamp(opponentCentralDefenders, 0, 5)}; PNF conversion={pnfConversion:P2}. PDIM={pdimCount}; suppression={pdimSuppression:P2}. LS scoring remains a graph/calibration input.");
    }

    public static double SetPieceGoalProbability(double ownIspAttackRating, double opponentIspDefenceRating)
    {
        var d = ownIspAttackRating - opponentIspDefenceRating;
        return Math.Clamp(-0.0000380429 * d * d * d + 0.0000226846 * d * d + 0.0366246 * d + 0.45515, 0.0, 1.0);
    }

    public static double LongShotTacticConversionRate(double tacticRating)
        => Math.Clamp(0.00761935 * tacticRating + 0.07520052, 0.0, 1.0);

    public static double PnfConversionRate(int pnfCount, int centralDefenders)
    {
        var p = Math.Max(0, pnfCount); var cd = Math.Clamp(centralDefenders, 0, 3);
        return p switch { <= 0 => 0.0, 1 => cd switch { 0 => 0.096, 1 => 0.069, 2 => 0.033, _ => 0.020 }, 2 => cd switch { 0 => 0.117, 1 => 0.096, 2 => 0.052, _ => 0.031 }, _ => 0.066 };
    }

    public static double PdimSuppressionRate(int pdimCount) => Math.Clamp(Math.Max(0, pdimCount) * 0.065, 0.0, 1.0);

    private static double LinearPossession(double ownMidfieldRating, double opponentMidfieldRating)
    {
        var own = Math.Max(0.0, ownMidfieldRating) * 4.0 - 3.0;
        var opp = Math.Max(0.0, opponentMidfieldRating) * 4.0 - 3.0;
        own = Math.Max(0.0, own); opp = Math.Max(0.0, opp);
        var total = own + opp;
        return total <= 0 ? 0.5 : own / total;
    }

    private static double SpecialEventMultiplier(AdvancedTactic tactic, double creativeMultiplier) => tactic == AdvancedTactic.Creative ? Math.Max(1.0, creativeMultiplier) : 1.0;
    private static double CornerHeadShare(int offensiveHeaders) => offensiveHeaders switch { <= 0 => 0.0, 1 => 0.27, 2 => 0.42, 3 => 0.51, 4 => 0.59, _ => 0.65 };
    private static int CountSpecial(IEnumerable<SlotProfile> profiles, PlayerSpecialty specialty, Func<SlotProfile, bool> predicate) => profiles.Count(x => x.Player.Specialty == specialty && predicate(x));
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
    private static bool IsInnerMidfielder(SlotProfile p) => IsInnerMidfielder(p.Code);
    private static bool IsForward(SlotProfile p) => IsForward(p.Code);
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
public sealed record M9EventGoalBreakdown(double PlayerBasedSpecialEventGoals, double TeamBasedSpecialEventGoals, double ExpectedGoalsConcededFromOwnGoalEvents, double CounterAttackGoals, double LongShotGoals, double PowerfulNormalForwardGoals, double PressingSuppressionSignal, double ExpectedPlayerBasedEvents, double ExpectedTeamBasedEvents, double TechnicalCounterAttackOpportunityRate, IReadOnlyList<M9EventContribution> Contributions, string CalibrationStatus, string Notes)
{
    public static M9EventGoalBreakdown Empty => new(0,0,0,0,0,0,0,0,0,0,Array.Empty<M9EventContribution>(),"Not calculated","No player/event context supplied.");
    public double NetSpecialEventGoalContribution => PlayerBasedSpecialEventGoals + TeamBasedSpecialEventGoals + PowerfulNormalForwardGoals - ExpectedGoalsConcededFromOwnGoalEvents;
}
