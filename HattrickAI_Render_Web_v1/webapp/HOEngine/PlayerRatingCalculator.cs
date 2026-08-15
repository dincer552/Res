using System;

namespace HattrickAI.HOEngine;

/// <summary>
/// HO!-style player strength layer.
/// The implementation follows the public RatingPredictionModel concepts:
/// skill rating = max(0, skill - 1), then loyalty + form are applied to skill strength.
/// Missing optional values are treated as neutral rather than destroying the rating.
/// </summary>
public sealed class PlayerRatingCalculator
{
    public double SkillRating(int skill) => Math.Max(0, skill - 1);

    public double FormFactor(int form)
    {
        // Hattrick form is normally 1..8. When the HTML parser cannot find it,
        // 5 is used as a neutral fallback.
        int normalized = form <= 0 ? 5 : Math.Clamp(form, 1, 8);
        double formRating = Math.Min(7.0, SkillRating(normalized));
        return 0.378 * Math.Sqrt(formRating);
    }

    public double LoyaltyFactor(PlayerData player)
    {
        int loyalty = player.Loyalty <= 0 ? 20 : Math.Clamp(player.Loyalty, 0, 20);
        return SkillRating(loyalty) / 19.0;
    }

    public double ExperienceContribution(int experience, RatingSector sector)
    {
        int normalized = experience <= 0 ? 1 : Math.Clamp(experience, 1, 20);
        double exp = SkillRating(normalized);
        double k =
            -0.00000725 * Math.Pow(exp, 4) +
             0.0005 * Math.Pow(exp, 3) -
             0.01336 * Math.Pow(exp, 2) +
             0.176 * exp;

        return sector switch
        {
            RatingSector.LeftDefence or RatingSector.RightDefence => k * 0.345,
            RatingSector.CentralDefence => k * 0.48,
            RatingSector.Midfield => k * 0.73,
            RatingSector.LeftAttack or RatingSector.RightAttack => k * 0.375,
            RatingSector.CentralAttack => k * 0.450,
            _ => 0
        };
    }

    public double StaminaFactor(PlayerData player, int minute = 0, int startMinute = 0, int tacticType = 0)
    {
        int stamina = player.Stamina <= 0 ? 7 : Math.Clamp(player.Stamina, 1, 9);
        double s = SkillRating(stamina);
        double pressingFactor = tacticType == 1 ? 1.1 : 1.0;

        double r0;
        double delta;

        if (s < 7)
        {
            r0 = 102.0 + 23.0 / 7.0 * s;
            delta = pressingFactor * (27.0 / 70.0 * s - 5.95);
        }
        else
        {
            r0 = 125.0 + (s - 7.0) * 100.0 / 7.0;
            delta = -3.25 * pressingFactor;
        }

        double r = r0;
        int to = Math.Min(45, minute);
        if (startMinute < to)
            r += (to - startMinute) * delta / 5.0;

        int from = Math.Max(45, startMinute);
        if (minute >= 45)
        {
            if (startMinute < 45)
                r = Math.Min(r0, r + 18.75);

            to = Math.Min(90, minute);
            if (from < to)
                r += (to - from) * delta / 5.0;
        }

        if (minute >= 90)
        {
            from = Math.Max(90, startMinute);
            if (startMinute < 90)
                r = Math.Min(r0, r + 6.25);

            if (from < minute)
                r += (minute - from) * delta / 5.0;
        }

        return Math.Clamp(r / 100.0, 0, 1);
    }

    public double WeatherFactor(PlayerData player, MatchWeather weather)
    {
        string specialty = NormalizeSpecialty(player.Specialty);

        if (specialty == "Technical" && weather == MatchWeather.Rainy)
            return 0.95;
        if (specialty == "Technical" && weather == MatchWeather.Sunny)
            return 1.05;
        if (specialty == "Powerful" && weather == MatchWeather.Rainy)
            return 1.05;
        if (specialty == "Powerful" && weather == MatchWeather.Sunny)
            return 0.95;
        if (specialty == "Quick" && weather != MatchWeather.Normal)
            return 0.95;

        return 1.0;
    }

    public double SkillStrength(PlayerData player, string skill)
    {
        if (player.Injured || player.Suspended)
            return 0;

        int value = skill.ToLowerInvariant() switch
        {
            "keeper" => player.Keeper,
            "defending" => player.Defending,
            "playmaking" => player.Playmaking,
            "winger" => player.Winger,
            "passing" => player.Passing,
            "scoring" => player.Scoring,
            "setpieces" => player.SetPieces,
            _ => 0
        };

        if (value <= 0)
            return 0;

        return (SkillRating(value) + LoyaltyFactor(player)) * FormFactor(player.Form);
    }

    public double GetSkill(PlayerData player, string skill) => SkillStrength(player, skill);

    private static string NormalizeSpecialty(string specialty)
    {
        if (string.IsNullOrWhiteSpace(specialty))
            return "";

        return specialty.Trim().ToLowerInvariant() switch
        {
            "technical" or "teknik" or "teknikçi" => "Technical",
            "powerful" or "güçlü" => "Powerful",
            "quick" or "hızlı" => "Quick",
            "head" or "kafa" => "Head",
            "unpredictable" or "öngörülemez" => "Unpredictable",
            "regainer" or "rejeneratif" => "Regainer",
            "support" or "destek" => "Support",
            _ => specialty.Trim()
        };
    }
}
