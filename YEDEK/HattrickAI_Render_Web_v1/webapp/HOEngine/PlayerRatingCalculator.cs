using System;

namespace HattrickAI.HOEngine;

/// <summary>
/// Direct C# port of the player-strength portions of HO!'s RatingPredictionModel.
/// Skill strength follows (SkillRating + Loyalty) * Form.
/// </summary>
public sealed class PlayerRatingCalculator
{
    public double SkillRating(double skill) => Math.Max(0, skill - 1);

    public double FormFactor(int form)
    {
        var value = Math.Min(7.0, SkillRating(form));
        return 0.378 * Math.Sqrt(value);
    }

    public double LoyaltyFactor(PlayerData player)
    {
        if (player.HomeGrown)
            return 1.5;

        return SkillRating(player.Loyalty) / 19.0;
    }

    public double ExperienceContribution(int experience, RatingSector sector)
    {
        var exp = SkillRating(experience);
        var k =
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
            _ => throw new ArgumentOutOfRangeException(nameof(sector))
        };
    }

    public double StaminaFactor(PlayerData player, int minute = 0, int startMinute = 0, int tacticType = 0)
    {
        var p = tacticType == 1 ? 1.1 : 1.0;
        var s = SkillRating(player.Stamina);
        double r0;
        double delta;

        if (s < 7)
        {
            r0 = 102.0 + 23.0 / 7.0 * s;
            delta = p * (27.0 / 70.0 * s - 5.95);
        }
        else
        {
            r0 = 102.0 + 23.0 + (s - 7.0) * 100.0 / 7.0;
            delta = -3.25 * p;
        }

        var r = r0;
        var to = Math.Min(45, minute);
        if (startMinute < to)
            r += (to - startMinute) * delta / 5.0;

        var from = Math.Max(45, startMinute);
        if (minute >= 45)
        {
            if (startMinute < 45)
                r = Math.Min(r0, r + 120.75 - 102.0);

            to = Math.Min(90, minute);
            if (from < to)
                r += (to - from) * delta / 5.0;
        }

        if (minute >= 90)
        {
            from = Math.Max(90, startMinute);
            if (startMinute < 90)
                r = Math.Min(r0, r + 127.0 - 120.75);

            if (from < minute)
                r += (minute - from) * delta / 5.0;
        }

        return Math.Min(1.0, r / 100.0);
    }

    public double WeatherFactor(PlayerData player, MatchWeather weather)
    {
        var specialty = NormalizeSpecialty(player.Specialty);
        if (specialty == "Technical")
        {
            if (weather == MatchWeather.Rainy) return 0.95;
            if (weather == MatchWeather.Sunny) return 1.05;
        }
        else if (specialty == "Powerful")
        {
            if (weather == MatchWeather.Rainy) return 1.05;
            if (weather == MatchWeather.Sunny) return 0.95;
        }
        else if (specialty == "Quick" && weather != MatchWeather.Normal)
        {
            return 0.95;
        }

        return 1.0;
    }

    public double SkillStrength(PlayerData player, string skill)
    {
        if (player.Injured || player.Suspended)
            return 0;

        var value = skill.ToLowerInvariant() switch
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

        var skillRating = SkillRating(value);
        var loyalty = LoyaltyFactor(player);
        var form = FormFactor(player.Form);
        return (skillRating + loyalty) * form;
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
