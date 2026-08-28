using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace HattrickAI.HOEngine;

public class PlayerHtmlLoader
{
    public List<PlayerData> Load(string html)
    {
        var players = new List<PlayerData>();

        if (string.IsNullOrWhiteSpace(html))
            return players;

        html = WebUtility.HtmlDecode(html);

        // ---------------------------------------------------------
        // 1. Oyuncu isimlerini sırayla al
        // ---------------------------------------------------------

        MatchCollection nameMatches =
            Regex.Matches(
                html,
                @"data-fullname\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);

        var names = new List<string>();

        foreach (Match match in nameMatches)
        {
            string name =
                WebUtility.HtmlDecode(
                    match.Groups[1].Value).Trim();

            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }

        // ---------------------------------------------------------
        // 2. Oyuncu ID'lerini sırayla al
        // ---------------------------------------------------------

        MatchCollection idMatches =
            Regex.Matches(
                html,
                @"<hattrick-player[^>]*player-id\s*=\s*[""'](\d+)[""']",
                RegexOptions.IgnoreCase);

        var ids = new List<int>();

        foreach (Match match in idMatches)
        {
            if (int.TryParse(
                    match.Groups[1].Value,
                    out int id))
            {
                ids.Add(id);
            }
        }

        // ---------------------------------------------------------
        // 3. Her oyuncunun yetenek tablosunu al
        // ---------------------------------------------------------

        MatchCollection skillBlocks =
            Regex.Matches(
                html,
                @"<div\s+class\s*=\s*[""']transferPlayerSkills[""'][\s\S]*?</table>",
                RegexOptions.IgnoreCase);

        int count =
            Math.Min(
                Math.Min(
                    names.Count,
                    ids.Count),
                skillBlocks.Count);

        // Güvenlik
        if (count == 0)
            return players;

        for (int i = 0; i < count; i++)
        {
            string block =
                skillBlocks[i].Value;

            var player =
                new PlayerData
                {
                    PlayerId = ids[i],
                    Name = names[i],

                    Keeper =
                        GetSkill(
                            block,
                            "trKeeper"),

                    Defending =
                        GetSkill(
                            block,
                            "trDefender"),

                    Playmaking =
                        GetSkill(
                            block,
                            "trPlaymaker"),

                    Winger =
                        GetSkill(
                            block,
                            "trWinger"),

                    Passing =
                        GetSkill(
                            block,
                            "trPasser"),

                    Scoring =
                        GetSkill(
                            block,
                            "trScorer"),

                    SetPieces =
                        GetSkill(
                            block,
                            "trKicker"),

                    // Temiz oyuncu HTML'inin bazı sürümlerinde bu
                    // satırlar aynı oyuncu tablosunda bulunur. Bulunmazsa
                    // 0 kalır; mevcut yetenek okuma akışı etkilenmez.
                    Form = GetSkill(block, "trForm"),
                    Stamina = GetSkill(block, "trStamina"),
                    Experience = GetSkill(block, "trExperience"),
                    Leadership = GetSkill(block, "trLeadership"),
                    Age = GetFirstNumberFromRow(block, "trAge"),
                    Loyalty = GetSkill(block, "trLoyalty") > 0 ? GetSkill(block, "trLoyalty") : 20,
                    Specialty = GetSpecialty(block),
                    Injured = HasExplicitStatus(block, "injured"),
                    Suspended = HasExplicitStatus(block, "suspended")
                };

            players.Add(player);
        }

        return players;
    }

    private static int GetFirstNumberFromRow(string block, string rowName)
    {
        string pattern =
            @"<tr[^>]*id\s*=\s*[""']?[^""']*" +
            Regex.Escape(rowName) +
            @"[^>]*>[\s\S]*?(\d+)";

        Match match = Regex.Match(block, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : 0;
    }

    private static string GetSpecialty(string block)
    {
        Match attribute = Regex.Match(
            block,
            @"(?:data-specialty|specialty)\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase);

        if (attribute.Success)
            return WebUtility.HtmlDecode(attribute.Groups[1].Value).Trim();

        Match row = Regex.Match(
            block,
            @"<tr[^>]*id\s*=\s*[""'][^""']*specialty[^""']*[""'][^>]*>[\s\S]*?<td[^>]*>([\s\S]*?)</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!row.Success)
            return "";

        string text = Regex.Replace(row.Groups[1].Value, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(text).Trim();
    }

    private static bool HasExplicitStatus(string block, string keyword)
    {
        return Regex.IsMatch(
            block,
            $@"(?:data-{Regex.Escape(keyword)}|is-{Regex.Escape(keyword)})\s*=\s*[""'](?:true|1)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static int GetSkill(
        string block,
        string rowName)
    {
        string pattern =
            @"<tr[^>]*id\s*=\s*[""'][^""']*" +
            Regex.Escape(rowName) +
            @"[""'][^>]*>" +
            @"[\s\S]*?" +
            @"<span[^>]*class\s*=\s*[""']?denominationNumber[""']?" +
            @"[^>]*title\s*=\s*[""']?(\d+)/\d+";

        Match match =
            Regex.Match(
                block,
                pattern,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (!match.Success)
            return 0;

        return int.TryParse(
            match.Groups[1].Value,
            out int value)
            ? value
            : 0;
    }
}
