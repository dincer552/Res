using System.Globalization;
using System.Xml.Linq;
using HattrickAI.HOEngine;

namespace HattrickAI.CHPP;

public sealed record ChppTeamSnapshot(
    int TeamId,
    string TeamName,
    List<PlayerData> Players,
    string TeamXml,
    string PlayersXml);

public sealed class ChppTeamDataService
{
    private readonly ChppOAuthClient _oauth;

    public ChppTeamDataService(ChppOAuthClient oauth)
    {
        _oauth = oauth;
    }

    public async Task<ChppTeamSnapshot> LoadOwnTeamAsync(
        CancellationToken cancellationToken = default)
    {
        var teamXml = await _oauth.GetXmlAsync(
            "teamdetails",
            new Dictionary<string, string?> { ["version"] = "1.7" },
            cancellationToken);

        var teamDoc = XDocument.Parse(teamXml);
        var team = teamDoc.Descendants("Team").FirstOrDefault();
        if (team == null)
            throw new InvalidDataException("CHPP teamdetails XML içinde Team bulunamadı.");

        var teamId = ReadInt(team, "TeamID");
        var teamName = ReadText(team, "TeamName") ?? "Hattrick Takımı";

        if (teamId <= 0)
            throw new InvalidDataException("CHPP takım ID'si okunamadı.");

        var playersXml = await _oauth.GetXmlAsync(
            "players",
            new Dictionary<string, string?>
            {
                ["version"] = "1.3",
                ["teamId"] = teamId.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        var players = ParsePlayers(playersXml);
        return new ChppTeamSnapshot(teamId, teamName, players, teamXml, playersXml);
    }

    public async Task<ChppTeamSnapshot> LoadTeamAsync(
        int teamId,
        string? fallbackTeamName = null,
        CancellationToken cancellationToken = default)
    {
        if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));

        var teamXml = await _oauth.GetXmlAsync(
            "teamdetails",
            new Dictionary<string, string?>
            { ["version"] = "1.7", ["teamId"] = teamId.ToString(CultureInfo.InvariantCulture) },
            cancellationToken);

        var teamDoc = XDocument.Parse(teamXml);
        var team = teamDoc.Descendants("Team").FirstOrDefault();
        var teamName = team == null ? (fallbackTeamName ?? "Rakip") : (ReadText(team, "TeamName") ?? fallbackTeamName ?? "Rakip");

        var playersXml = await _oauth.GetXmlAsync(
            "players",
            new Dictionary<string, string?>
            { ["version"] = "1.3", ["teamId"] = teamId.ToString(CultureInfo.InvariantCulture) },
            cancellationToken);

        return new ChppTeamSnapshot(teamId, teamName, ParsePlayers(playersXml), teamXml, playersXml);
    }

    private static List<PlayerData> ParsePlayers(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new List<PlayerData>();

        foreach (var node in doc.Descendants("Player"))
        {
            var injuryLevel = ReadInt(node, "InjuryLevel", -1);
            var cards = ReadInt(node, "Cards");
            var player = new PlayerData
            {
                PlayerId = ReadInt(node, "PlayerID"),
                Name = ReadText(node, "PlayerName") ?? "Bilinmeyen Oyuncu",
                Age = ReadInt(node, "Age"),
                Form = ReadInt(node, "PlayerForm"),
                Experience = ReadInt(node, "Experience"),
                Leadership = ReadInt(node, "Leadership"),
                Specialty = ReadInt(node, "Specialty").ToString(CultureInfo.InvariantCulture),
                Stamina = ReadInt(node, "StaminaSkill"),
                Keeper = ReadInt(node, "KeeperSkill"),
                Playmaking = ReadInt(node, "PlaymakerSkill"),
                Scoring = ReadInt(node, "ScorerSkill"),
                Passing = ReadInt(node, "PassingSkill"),
                Winger = ReadInt(node, "WingerSkill"),
                Defending = ReadInt(node, "DefenderSkill"),
                SetPieces = ReadInt(node, "SetPiecesSkill"),
                Injured = injuryLevel >= 0,
                Suspended = cards >= 3
            };

            if (player.PlayerId > 0)
                result.Add(player);
        }

        return result;
    }

    private static int ReadInt(XElement parent, string name, int fallback = 0)
    {
        var text = ReadText(parent, name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    private static string? ReadText(XElement parent, string name)
    {
        return parent.Element(name)?.Value?.Trim();
    }
}
