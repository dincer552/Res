using System.Globalization;
using System.Xml.Linq;
using HattrickAI.HOEngine;

namespace HattrickAI.CHPP;

public sealed class ChppTrainingDataService
{
    private readonly ChppOAuthClient _oauth;

    public ChppTrainingDataService(ChppOAuthClient oauth) => _oauth = oauth;

    public async Task<TrainingRecommendationProfile> LoadOwnTrainingAsync(CancellationToken cancellationToken = default)
    {
        var xml = await ChppTraceHttp.GetXmlAsync(
            _oauth,
            "training",
            new Dictionary<string, string?> { ["version"] = "1.1" },
            "own-team training",
            cancellationToken);

        var doc = XDocument.Parse(xml);
        var team = doc.Descendants("Team").FirstOrDefault();
        if (team == null)
            throw new InvalidDataException("CHPP training XML içinde Team bulunamadı.");

        var experience = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["4-3-3"] = ReadInt(team, "Experience433"),
            ["4-5-1"] = ReadInt(team, "Experience451"),
            ["3-5-2"] = ReadInt(team, "Experience352"),
            ["5-3-2"] = ReadInt(team, "Experience532"),
            ["3-4-3"] = ReadInt(team, "Experience343"),
            ["5-4-1"] = ReadInt(team, "Experience541")
        };

        var trainingType = ReadInt(team, "TrainingType");
        return new TrainingRecommendationProfile
        {
            TrainingType = trainingType,
            TrainingName = TrainingName(trainingType),
            TrainingLevel = ReadInt(team, "TrainingLevel"),
            StaminaTrainingPart = ReadInt(team, "StaminaTrainingPart"),
            FormationExperience = experience
        };
    }

    private static string TrainingName(int type) => type switch
    {
        0 => "Genel",
        1 => "Dayanıklılık",
        2 => "Duran Toplar",
        3 => "Defans",
        4 => "Golcülük",
        5 => "Kanat (Crossing)",
        6 => "Şut",
        7 => "Kısa Paslar",
        8 => "Oyun Kurma",
        9 => "Kalecilik",
        10 => "Ara Paslar",
        11 => "Defansif Pozisyonlar",
        _ => $"Antrenman #{type}"
    };

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    {
        var text = parent.Element(name)?.Value?.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
