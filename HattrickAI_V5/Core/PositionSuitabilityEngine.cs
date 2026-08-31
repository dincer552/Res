namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 1: oyuncunun seçilen pozisyon için temel uygunluğunu hesaplar.
/// Mevcut V5 seçim ağırlıklarını tek bir yerde toplar. XI optimizasyonu ve
/// oyuncu davranışı seçimi sonraki motorlarda bu katmanı kullanacaktır.
/// </summary>
public sealed class PositionSuitabilityEngine
{
    public double Score(Player p, string code)
    {
        ArgumentNullException.ThrowIfNull(p);

        return code switch
        {
            "GK" => p.Keeper + p.Form * .15,
            "DEF-L" or "DEF-R" => p.Defending + p.Passing * .10 + p.Winger * .05,
            "DEF-C" or "DEF-CL" or "DEF-CR" => p.Defending * 1.05 + p.Passing * .15 + p.Playmaking * .04,
            "W-L" or "W-R" => p.Winger + p.Passing * .22 + p.Playmaking * .08,
            "IM-L" or "IM-R" => p.Playmaking + p.Passing * .25 + p.Stamina * .12,
            "IM-C" => p.Playmaking * 1.05 + p.Passing * .25 + p.Stamina * .12 + p.Experience * .04,
            "FW-L" => p.Scoring + p.Passing * .18 + p.Winger * .08,
            "FW-R" => p.Scoring * 1.05 + p.Passing * .18 + p.Winger * .08 + p.Experience * .04,
            "FW-C" => p.Scoring * 1.05 + p.Passing * .20 + p.Playmaking * .04,
            _ => double.NegativeInfinity
        };
    }

    public IReadOnlyDictionary<string, double> ScoreAll(Player p)
    {
        var codes = new[]
        {
            "GK", "DEF-L", "DEF-C", "DEF-R",
            "W-L", "IM-L", "IM-C", "IM-R", "W-R",
            "FW-L", "FW-R"
        };

        return codes.ToDictionary(code => code, code => Score(p, code));
    }
}
