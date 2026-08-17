using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.CHPP;

public sealed record ChppTrainingSnapshot(
    int TeamId,
    int TrainingType,
    string TrainingName,
    int TrainingLevel,
    int StaminaTrainingPart,
    IReadOnlyDictionary<string, int> FormationExperience,
    string Xml);

public sealed class ChppTrainingService
{
    private readonly ChppOAuthClient _oauth;

    public ChppTrainingService(ChppOAuthClient oauth) => _oauth = oauth;

    public async Task<ChppTrainingSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var xml = await ChppTraceHttp.GetXmlAsync(
            _oauth,
            "training",
            new Dictionary<string, string?> { ["version"] = "1.2", ["actionType"] = "view" },
            "own-team training",
            cancellationToken);

        var doc = XDocument.Parse(xml);
        var team = doc.Descendants("Team").FirstOrDefault();
        if (team == null)
            throw new InvalidDataException("CHPP training XML içinde Team bulunamadı.");

        var type = ReadInt(team, "TrainingType", -1);
        if (type < 0)
            throw new InvalidDataException("CHPP aktif antrenman türü okunamadı.");

        var experience = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["4-3-3"] = ReadInt(team, "Experience433"),
            ["4-5-1"] = ReadInt(team, "Experience451"),
            ["3-5-2"] = ReadInt(team, "Experience352"),
            ["5-3-2"] = ReadInt(team, "Experience532"),
            ["3-4-3"] = ReadInt(team, "Experience343"),
            ["5-4-1"] = ReadInt(team, "Experience541")
        };

        return new ChppTrainingSnapshot(
            ReadInt(team, "TeamID"),
            type,
            TrainingName(type),
            ReadInt(team, "TrainingLevel"),
            ReadInt(team, "StaminaTrainingPart"),
            experience,
            xml);
    }

    public static string TrainingName(int type) => type switch
    {
        0 => "Genel",
        1 => "Dayanıklılık",
        2 => "Duran Toplar",
        3 => "Defans",
        4 => "Golcülük",
        5 => "Kanat Hücumu",
        6 => "Şut",
        7 => "Kısa Paslar",
        8 => "Oyun Kurma",
        9 => "Kalecilik",
        10 => "Ara Paslar",
        11 => "Defansif Pozisyon",
        _ => "Bilinmiyor"
    };

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    {
        var text = parent.Element(name)?.Value?.Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
