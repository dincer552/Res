using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.CHPP;

public sealed record ChppLineupPlayer(
    int PlayerId,
    string Name,
    int RoleId,
    int PositionCode,
    int Behaviour,
    double RatingStars)
{
    // These are presentation fields for the web lineup. PositionCode is the
    // player's actual final position in the historical match; RoleId is only
    // the formal slot before repositioning.
    public string RoleKey => PositionCode switch
    {
        1 => "Goalkeeper",
        2 => "RightDefender",
        3 or 4 => "CentralDefender",
        5 => "LeftDefender",
        6 => "RightWinger",
        7 or 8 => "CentralMidfielder",
        9 => "LeftWinger",
        10 or 11 => RoleId switch
        {
            10 => "CentralForward",
            11 => "RightForward",
            _ => "CentralForward"
        },
        _ => "CentralMidfielder"
    };

    public string Role => RoleKey switch
    {
        "Goalkeeper" => "KL",
        "RightDefender" => "SGB",
        "CentralDefender" => "STP",
        "LeftDefender" => "SLB",
        "RightWinger" => "K",
        "CentralMidfielder" => "OM",
        "LeftWinger" => "K",
        "RightForward" or "LeftForward" or "CentralForward" => "SF",
        _ => "OM"
    };

    public double Rating => RatingStars;
    public string Form => "-";
    public string Stamina => "-";
}

public sealed class ChppMatchLineupService
{
    private readonly ChppOAuthClient _oauth;
    public ChppMatchLineupService(ChppOAuthClient oauth) => _oauth = oauth;

    public async Task<IReadOnlyList<ChppLineupPlayer>> LoadAsync(int matchId, int teamId, CancellationToken cancellationToken = default)
    {
        var xml = await ChppTraceHttp.GetXmlAsync(_oauth, "matchlineup",
            new Dictionary<string, string?>
            {
                ["version"] = "1.1",
                ["matchID"] = matchId.ToString(CultureInfo.InvariantCulture),
                ["teamID"] = teamId.ToString(CultureInfo.InvariantCulture)
            },
            $"historical opponent lineup matchId={matchId} teamId={teamId}", cancellationToken);

        var doc = XDocument.Parse(xml);
        var team = doc.Descendants("Team").FirstOrDefault();
        if (team == null) return Array.Empty<ChppLineupPlayer>();

        return team.Descendants("Player")
            .Select(p => new ChppLineupPlayer(
                ReadInt(p, "PlayerID"), ReadText(p, "PlayerName") ?? "Bilinmeyen Oyuncu", ReadInt(p, "RoleID"),
                ReadInt(p, "PositionCode"), ReadInt(p, "Behaviour"), ReadDouble(p, "RatingStars")))
            .Where(p => p.PlayerId > 0 && p.PositionCode > 0)
            .Take(11)
            .ToList();
    }

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    {
        var text = ReadText(parent, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
    private static double ReadDouble(XElement parent, string name, double fallback = 0)
    {
        var text = ReadText(parent, name);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
    private static string? ReadText(XElement parent, string name) => parent.Element(name)?.Value?.Trim();
}
