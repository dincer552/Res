using System.Text.Json;

namespace HattrickAI.HOEngine;

public class MatchJsonLoader
{
    public MatchData? FindLatestMatch(
        string json,
        string teamName)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON boş.");

        using JsonDocument document =
            JsonDocument.Parse(json);

        return FindLatestMatch(
            document.RootElement,
            teamName);
    }

    private static MatchData? FindLatestMatch(
        JsonElement element,
        string teamName)
    {
        MatchData? latest = null;

        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            if (IsMatchObject(element, teamName))
            {
                latest = CreateMatchData(element);
            }

            foreach (JsonProperty property
                in element.EnumerateObject())
            {
                MatchData? found =
                    FindLatestMatch(
                        property.Value,
                        teamName);

                if (found != null)
                {
                    latest = ChooseNewer(
                        latest,
                        found);
                }
            }
        }
        else if (element.ValueKind ==
                 JsonValueKind.Array)
        {
            foreach (JsonElement item
                in element.EnumerateArray())
            {
                MatchData? found =
                    FindLatestMatch(
                        item,
                        teamName);

                if (found != null)
                {
                    latest = ChooseNewer(
                        latest,
                        found);
                }
            }
        }

        return latest;
    }

    private static bool IsMatchObject(
        JsonElement element,
        string teamName)
    {
        string home =
            GetString(element, "HEIMNAME");

        string away =
            GetString(element, "GASTNAME");

        if (string.IsNullOrWhiteSpace(home) ||
            string.IsNullOrWhiteSpace(away))
        {
            return false;
        }

        return
            string.Equals(
                home,
                teamName,
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                away,
                teamName,
                StringComparison.OrdinalIgnoreCase);
    }

    private static MatchData CreateMatchData(
        JsonElement element)
    {
        return new MatchData
        {
            HomeTeamName =
                GetString(
                    element,
                    "HEIMNAME"),

            AwayTeamName =
                GetString(
                    element,
                    "GASTNAME"),

            HomeRatings =
                CreateRatings(
                    element,
                    "HEIM"),

            AwayRatings =
                CreateRatings(
                    element,
                    "GAST"),

            HomeGoals =
                GetInt(
                    element,
                    "HEIMTORE"),

            AwayGoals =
                GetInt(
                    element,
                    "GASTTORE"),

            MatchDate =
                GetDate(
                    element,
                    "SPIELDATUM")
        };
    }

    private static TeamRatings CreateRatings(
        JsonElement element,
        string prefix)
    {
        return new TeamRatings
        {
            Midfield =
                GetDouble(
                    element,
                    prefix + "MIDFIELD"),

            LeftDefence =
                GetDouble(
                    element,
                    prefix + "LEFTDEF"),

            CentralDefence =
                GetDouble(
                    element,
                    prefix + "MIDDEF"),

            RightDefence =
                GetDouble(
                    element,
                    prefix + "RIGHTDEF"),

            LeftAttack =
                GetDouble(
                    element,
                    prefix + "LEFTATT"),

            CentralAttack =
                GetDouble(
                    element,
                    prefix + "MIDATT"),

            RightAttack =
                GetDouble(
                    element,
                    prefix + "RIGHTATT")
        };
    }

    private static MatchData ChooseNewer(
        MatchData? current,
        MatchData candidate)
    {
        if (current == null)
            return candidate;

        return candidate.MatchDate >
               current.MatchDate
            ? candidate
            : current;
    }

    private static string GetString(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return "";
        }

        return value.ValueKind ==
               JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int GetInt(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return 0;
        }

        if (value.ValueKind ==
            JsonValueKind.Number)
        {
            return value.GetInt32();
        }

        if (value.ValueKind ==
            JsonValueKind.String &&
            int.TryParse(
                value.GetString(),
                out int number))
        {
            return number;
        }

        return 0;
    }

    private static double GetDouble(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return 0;
        }

        if (value.ValueKind ==
            JsonValueKind.Number)
        {
            return value.GetDouble();
        }

        if (value.ValueKind ==
            JsonValueKind.String &&
            double.TryParse(
                value.GetString(),
                out double number))
        {
            return number;
        }

        return 0;
    }

    private static DateTime GetDate(
        JsonElement element,
        string property)
    {
        string value =
            GetString(
                element,
                property);

        if (DateTime.TryParse(
                value,
                out DateTime date))
        {
            return date;
        }

        return DateTime.MinValue;
    }
}