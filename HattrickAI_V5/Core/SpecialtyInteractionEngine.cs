namespace HattrickAI.V5.Core;

public enum MatchWeather
{
    Normal = 0,
    Sun = 1,
    Rain = 2
}

public sealed record SpecialtyWeatherEffect(
    double ScoringMultiplier = 1.0,
    double DefendingMultiplier = 1.0,
    double PlaymakingMultiplier = 1.0,
    double StaminaMultiplier = 1.0)
{
    public static SpecialtyWeatherEffect Neutral => new();
}

public sealed record SpecialtyTacticEffect(
    double CounterAttackBoostPercent,
    double TechnicalDefensiveWingPassingMultiplier,
    double PressingDefenceWeightMultiplier,
    bool IsSpecialEventAmplified,
    bool IsTechnicalTargetForManMarking,
    bool CanCreateNonTacticalCounterAttack,
    bool CanCreateQuickEvent);

public static class SpecialtyInteractionEngine
{
    public const double WeatherSkillDelta = 0.05;
    public const double TechnicalDefensiveForwardPassingMultiplier = 1.33;
    public const double QuickCounterAttackSinglePlayerBoost = 0.05;
    public const double QuickCounterAttackEightPlayerBoost = 0.14;

    public static SpecialtyWeatherEffect GetWeatherEffect(PlayerSpecialty specialty, MatchWeather weather)
        => (specialty, weather) switch
        {
            (PlayerSpecialty.Technical, MatchWeather.Sun) => new(1.05, 1.0, 1.05),
            (PlayerSpecialty.Technical, MatchWeather.Rain) => new(0.95, 1.0, 0.95),
            (PlayerSpecialty.Powerful, MatchWeather.Sun) => new(0.95, 1.0, 1.0),
            (PlayerSpecialty.Powerful, MatchWeather.Rain) => new(1.05, 1.05, 1.05),
            (PlayerSpecialty.Quick, MatchWeather.Sun) => new(0.95, 0.95, 1.0),
            (PlayerSpecialty.Quick, MatchWeather.Rain) => new(0.95, 0.95, 1.0),
            _ => SpecialtyWeatherEffect.Neutral
        };

    public static int ApplyWeatherToSkill(int skill, PlayerSpecialty specialty, MatchWeather weather, Func<SpecialtyWeatherEffect, double> selector)
    {
        if (skill <= 0) return skill;
        var multiplier = selector(GetWeatherEffect(specialty, weather));
        return Math.Max(0, (int)Math.Round(skill * multiplier, MidpointRounding.AwayFromZero));
    }

    public static SpecialtyTacticEffect GetTacticEffect(
        PlayerSpecialty specialty,
        string slotCode,
        PlayerOrder order,
        AdvancedTactic tactic,
        int ownQuickRelevantPlayers = 0,
        int opponentQuickDefensivePlayers = 0)
    {
        var isDefender = slotCode.StartsWith("DEF", StringComparison.Ordinal) || slotCode == "GK";
        var isForward = slotCode.StartsWith("FW", StringComparison.Ordinal);
        var canQuickEvent = specialty == PlayerSpecialty.Quick && !isDefender;
        var technicalCa = specialty == PlayerSpecialty.Technical && isDefender;
        var technicalTdf = specialty == PlayerSpecialty.Technical && isForward && order == PlayerOrder.Defensive;
        var pressingPowerful = specialty == PlayerSpecialty.Powerful && isDefender;
        var caBoost = tactic == AdvancedTactic.CounterAttack && specialty == PlayerSpecialty.Quick && !isDefender
            ? CounterAttackSpecialtyBoostPercent(ownQuickRelevantPlayers, opponentQuickDefensivePlayers)
            : 0.0;

        return new SpecialtyTacticEffect(
            caBoost,
            technicalTdf ? TechnicalDefensiveForwardPassingMultiplier : 1.0,
            pressingPowerful ? 2.0 : 1.0,
            tactic == AdvancedTactic.Creative,
            specialty == PlayerSpecialty.Technical,
            technicalCa,
            canQuickEvent);
    }

    public static double CounterAttackSpecialtyBoostPercent(int ownQuickRelevantPlayers, int opponentQuickDefensivePlayers)
    {
        var own = Math.Clamp(ownQuickRelevantPlayers, 0, 8);
        var opponent = Math.Clamp(opponentQuickDefensivePlayers, 0, 8);
        if (own == 0) return 0.0;

        // Published anchors: one extra Quick player gives +5%, eight give +14%.
        // The source explicitly says the relation is non-linear, so V5 does not
        // invent intermediate points. Linear interpolation is used only as a
        // bounded diagnostic estimate; production calibration remains pending.
        var estimate = own == 1
            ? QuickCounterAttackSinglePlayerBoost
            : QuickCounterAttackSinglePlayerBoost +
              (QuickCounterAttackEightPlayerBoost - QuickCounterAttackSinglePlayerBoost) * (own - 1) / 7.0;

        // Opposing Quick Wing Backs / IMs / Defenders reduce the extra boost.
        var reduction = 1.0 - Math.Min(1.0, opponent / 8.0);
        return Math.Clamp(estimate * reduction, 0.0, QuickCounterAttackEightPlayerBoost);
    }

    public static double HeadSetPieceScoringOpportunityBonus(int offensiveHeadSpecialists)
        => Math.Clamp(Math.Max(0, offensiveHeadSpecialists) * 0.07, 0.0, 0.70);

    public static double OpponentHeadSetPieceSuppression(int defensiveHeadSpecialists)
        => Math.Clamp(Math.Max(0, defensiveHeadSpecialists) * 0.05, 0.0, 0.50);
}
