using HattrickAI.CHPP;
using System.Globalization;
using System.Xml.Linq;

namespace HattrickAI.V3.Core;

public sealed record V3Player(int Id, string Name, int Keeper, int Defending, int Playmaking, int Passing, int Winger, int Scoring, int Stamina, int Form, int Experience);
public sealed record V3Slot(string Code, string Position, string Name, int PlayerId, double X, double Y, string Reason);
public sealed record V3Pitch(string TeamName, string Formation, IReadOnlyList<V3Slot> Players, bool Opponent);
public sealed record V3AnalysisResult(V3Pitch OpponentPitch, V3Pitch OwnPitch, string MatchTitle, DateTimeOffset GeneratedAt);

public sealed class V3AnalysisService
{
    private readonly ChppOAuthClient _oauth;
    public V3AnalysisService(ChppOAuthClient oauth) => _oauth = oauth;

    public async Task<V3AnalysisResult> RunAsync(CancellationToken ct = default)
    {
        var ownTeam = await ReadTeamAsync(ct);
        var ownPlayers = await ReadPlayersAsync(ownTeam.TeamId, ct);
        var matchesXml = await _oauth.GetXmlAsync("matches", new Dictionary<string, string?>
        {
            ["version"] = "1.3",
            ["teamId"] = ownTeam.TeamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var matches = ParseMatches(matchesXml, ownTeam.TeamId);
        var next = matches.Where(x => x.Date > DateTimeOffset.UtcNow).OrderBy(x => x.Date).FirstOrDefault();
        if (next is null)
            throw new InvalidOperationException("Yaklaşan maç bulunamadı.");

        var opponentId = next.HomeTeamId == ownTeam.TeamId ? next.AwayTeamId : next.HomeTeamId;
        var opponentName = next.HomeTeamId == ownTeam.TeamId ? next.AwayName : next.HomeName;
        if (opponentId <= 0)
            throw new InvalidOperationException("Rakip takım ID'si bulunamadı.");

        var opponentMatchesXml = await _oauth.GetXmlAsync("matches", new Dictionary<string, string?>
        {
            ["version"] = "1.3",
            ["teamId"] = opponentId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var opponentMatches = ParseMatches(opponentMatchesXml, opponentId);
        var last = opponentMatches.Where(x => x.Date < DateTimeOffset.UtcNow).OrderByDescending(x => x.Date).FirstOrDefault();
        if (last is null)
            throw new InvalidOperationException("Rakibin son maçı bulunamadı.");

        var opponentLineupXml = await _oauth.GetXmlAsync("matchlineup", new Dictionary<string, string?>
        {
            ["version"] = "1.1",
            ["matchID"] = last.MatchId.ToString(CultureInfo.InvariantCulture),
            ["teamID"] = opponentId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var opponentLineup = ParseLineup(opponentLineupXml, opponentId, true);

        var detailXml = await _oauth.GetXmlAsync("matchdetails", new Dictionary<string, string?>
        {
            ["version"] = "1.0",
            ["matchID"] = last.MatchId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var weakness = ParseOpponentWeakness(detailXml, opponentId);
        var own = BuildOwnPitch(ownTeam.TeamName, ownPlayers, weakness);
        var opp = BuildOpponentPitch(opponentName, opponentLineup);
        var matchTitle = $"{next.Date:dd.MM.yyyy HH:mm} • {opponentName} • {(next.HomeTeamId == ownTeam.TeamId ? "Ev" : "Deplasman")}";
        return new V3AnalysisResult(opp, own, matchTitle, DateTimeOffset.UtcNow);
    }

    private async Task<(int TeamId, string TeamName)> ReadTeamAsync(CancellationToken ct)
    {
        var xml = await _oauth.GetXmlAsync("teamdetails", new Dictionary<string, string?> { ["version"] = "3.0" }, ct);
        var team = XDocument.Parse(xml).Descendants("Team").FirstOrDefault();
        var id = ReadInt(team, "TeamID");
        var name = ReadText(team, "TeamName") ?? "Takım";
        if (id <= 0) throw new InvalidOperationException("Takım bilgisi alınamadı.");
        return (id, name);
    }

    private async Task<IReadOnlyList<V3Player>> ReadPlayersAsync(int teamId, CancellationToken ct)
    {
        var xml = await _oauth.GetXmlAsync("players", new Dictionary<string, string?>
        {
            ["version"] = "1.3", ["teamId"] = teamId.ToString(CultureInfo.InvariantCulture)
        }, ct);
        var result = new List<V3Player>();
        foreach (var node in XDocument.Parse(xml).Descendants("Player"))
        {
            var id = ReadInt(node, "PlayerID");
            if (id <= 0) continue;
            result.Add(new V3Player(id,
                ReadText(node, "PlayerName") ?? "Oyuncu",
                ReadInt(node, "KeeperSkill"), ReadInt(node, "DefenderSkill"), ReadInt(node, "PlaymakerSkill"),
                ReadInt(node, "PassingSkill"), ReadInt(node, "WingerSkill"), ReadInt(node, "ScorerSkill"),
                ReadInt(node, "StaminaSkill"), ReadInt(node, "PlayerForm"), ReadInt(node, "Experience")));
        }
        return result;
    }

    private static List<(int MatchId, DateTimeOffset Date, int HomeTeamId, int AwayTeamId, string HomeName, string AwayName)> ParseMatches(string xml, int teamId)
    {
        var list = new List<(int, DateTimeOffset, int, int, string, string)>();
        foreach (var m in XDocument.Parse(xml).Descendants("Match"))
        {
            var id = ReadInt(m, "MatchID");
            var date = ReadDate(m, "MatchDate");
            if (id <= 0 || date == default) continue;
            var homeId = ReadInt(m, "HomeTeamID");
            var awayId = ReadInt(m, "AwayTeamID");
            var home = ReadText(m, "HomeTeamName") ?? "Ev sahibi";
            var away = ReadText(m, "AwayTeamName") ?? "Deplasman";
            if (homeId == teamId || awayId == teamId)
                list.Add((id, date, homeId, awayId, home, away));
        }
        return list;
    }

    private static List<V3Slot> ParseLineup(string xml, int teamId, bool opponent)
    {
        var result = new List<V3Slot>();
        var players = XDocument.Parse(xml).Descendants("Player").Take(11).ToList();
        foreach (var p in players)
        {
            var id = ReadInt(p, "PlayerID");
            var name = ReadText(p, "PlayerName") ?? "Oyuncu";
            var pos = ReadInt(p, "PositionCode");
            var role = pos switch
            {
                1 => ("GK", "Kaleci", 50d, opponent ? 12d : 88d),
                2 => ("DEF-R", "Sağ stoper", 76d, opponent ? 34d : 66d),
                3 or 4 => ("DEF-C", "Merkez stoper", 50d, opponent ? 30d : 70d),
                5 => ("DEF-L", "Sol stoper", 24d, opponent ? 34d : 66d),
                6 => ("W-R", "Sağ kanat", 83d, opponent ? 50d : 50d),
                7 or 8 => ("IM", "Merkez orta saha", 50d, opponent ? 50d : 50d),
                9 => ("W-L", "Sol kanat", 17d, opponent ? 50d : 50d),
                10 or 11 => ("FW", "Forvet", pos == 10 ? 40d : 60d, opponent ? 70d : 30d),
                _ => ("IM", "Merkez orta saha", 50d, opponent ? 50d : 50d)
            };
            result.Add(new V3Slot(role.Item1, role.Item2, name, id, role.Item3, role.Item4, "Rakibin son maçındaki gerçek yerleşim"));
        }
        return result;
    }

    private static (string Target, double LeftDef, double MidDef, double RightDef, double LeftAttack, double MidAttack, double RightAttack) ParseOpponentWeakness(string xml, int opponentId)
    {
        var doc = XDocument.Parse(xml);
        var team = doc.Descendants("Team").FirstOrDefault(t => ReadInt(t, "TeamID") == opponentId) ?? doc.Descendants("Team").FirstOrDefault();
        var ld = ReadDoubleCandidates(team, "LeftDefenceRating", "LeftDefendingRating", "LeftDefRating");
        var md = ReadDoubleCandidates(team, "MiddleDefenceRating", "CentralDefenceRating", "MiddleDefendingRating");
        var rd = ReadDoubleCandidates(team, "RightDefenceRating", "RightDefendingRating", "RightDefRating");
        var la = ReadDoubleCandidates(team, "LeftAttackRating", "LeftAttackingRating", "LeftAttack");
        var ma = ReadDoubleCandidates(team, "MiddleAttackRating", "CentralAttackRating", "MiddleAttackingRating");
        var ra = ReadDoubleCandidates(team, "RightAttackRating", "RightAttackingRating", "RightAttack");
        var min = Math.Min(ld, Math.Min(md, rd));
        var target = min == ld ? "SOL" : min == rd ? "SAĞ" : "MERKEZ";
        return (target, ld, md, rd, la, ma, ra);
    }

    private static V3Pitch BuildOwnPitch(string teamName, IReadOnlyList<V3Player> players, (string Target, double LeftDef, double MidDef, double RightDef, double LeftAttack, double MidAttack, double RightAttack) weakness)
    {
        var used = new HashSet<int>();
        V3Player Pick(Func<V3Player, double> score)
        {
            var p = players.Where(p => !used.Contains(p.Id)).OrderByDescending(score).FirstOrDefault();
            if (p == null) throw new InvalidOperationException("11 uygun oyuncu oluşturulamadı.");
            used.Add(p.Id); return p;
        }

        var slots = new List<V3Slot>();
        var gk = Pick(p => p.Keeper * 2 + p.Defending * .2 + p.Form * .1);
        slots.Add(new V3Slot("GK", "Kaleci", gk.Name, gk.Id, 50, 88, "En yüksek kalecilik"));
        var defs = new[] { Pick(p => p.Defending + p.Passing * .2), Pick(p => p.Defending + p.Passing * .15 + p.Experience * .05), Pick(p => p.Defending + p.Passing * .2) };
        slots.Add(new V3Slot("DEF-L", "Sol savunma", defs[0].Name, defs[0].Id, 24, 68, "Savunma dengesi"));
        slots.Add(new V3Slot("DEF-C", "Merkez savunma", defs[1].Name, defs[1].Id, 50, 72, "Merkez güvenliği"));
        slots.Add(new V3Slot("DEF-R", "Sağ savunma", defs[2].Name, defs[2].Id, 76, 68, "Savunma dengesi"));

        var ims = new[] { Pick(p => p.Playmaking + p.Passing * .25 + p.Stamina * .15), Pick(p => p.Playmaking + p.Passing * .2), Pick(p => p.Playmaking + p.Passing * .2 + p.Experience * .05) };
        slots.Add(new V3Slot("IM-L", "Sol iç", ims[0].Name, ims[0].Id, 34, 50, "Orta saha hakimiyeti"));
        slots.Add(new V3Slot("IM-C", "Merkez iç", ims[1].Name, ims[1].Id, 50, 46, "Merkez hakimiyeti"));
        slots.Add(new V3Slot("IM-R", "Sağ iç", ims[2].Name, ims[2].Id, 66, 50, "Orta saha hakimiyeti"));

        var rightWing = weakness.Target == "SOL";
        var wl = Pick(p => p.Winger + p.Passing * .25 + (rightWing ? p.Playmaking * .05 : 0));
        var wr = Pick(p => p.Winger + p.Passing * .25 + (rightWing ? p.Winger * .05 : 0));
        slots.Add(new V3Slot("W-L", "Sol kanat", wl.Name, wl.Id, 14, 49, rightWing ? "Sol savunma hedeflendi" : "Kanat dengesi"));
        slots.Add(new V3Slot("W-R", "Sağ kanat", wr.Name, wr.Id, 86, 49, rightWing ? "Zayıf sol savunmaya yüklen" : "Kanat dengesi"));

        var f1 = Pick(p => p.Scoring + p.Passing * .2 + (rightWing ? p.Winger * .08 : 0));
        var f2 = Pick(p => p.Scoring + p.Passing * .15 + p.Experience * .05);
        slots.Add(new V3Slot("FW-L", "Sol forvet", f1.Name, f1.Id, 40, 28, rightWing ? "Rakibin sol savunmasına yönel" : "Golcülük"));
        slots.Add(new V3Slot("FW-R", "Sağ forvet", f2.Name, f2.Id, 60, 28, "Golcülük ve bağlantı"));
        return new V3Pitch(teamName, "3-5-2", slots, false);
    }

    private static V3Pitch BuildOpponentPitch(string teamName, IReadOnlyList<V3Slot> slots) => new(teamName, "Son maç", slots, true);

    private static int ReadInt(XElement? node, string name) => int.TryParse(ReadText(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0;
    private static double ReadDoubleCandidates(XElement? node, params string[] names)
    {
        foreach (var name in names)
            if (double.TryParse(ReadText(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return x;
        return 0;
    }
    private static string? ReadText(XElement? node, string name) => node?.Element(name)?.Value?.Trim() ?? node?.Descendants(name).FirstOrDefault()?.Value?.Trim();
    private static DateTimeOffset ReadDate(XElement node, string name)
        => DateTimeOffset.TryParse(ReadText(node, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var x) ? x : default;
}
