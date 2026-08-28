using System.Text.Json;

namespace HattrickAI.HOEngine;

public class TeamJsonLoader
{
    public TeamInput Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON boş.");

        using JsonDocument document =
            JsonDocument.Parse(json);

        JsonElement match =
            FindLatestMatchForTeam(
                document.RootElement,
                "S4MSUNFC");

        if (match.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidDataException(
                "JSON içinde S4MSUNFC takımı bulunamadı.");
        }

        string homeName =
            GetString(match, "HEIMNAME");

        string awayName =
            GetString(match, "GASTNAME");

        bool isHome =
            string.Equals(
                homeName,
                "S4MSUNFC",
                StringComparison.OrdinalIgnoreCase);

        if (isHome)
        {
            return new TeamInput(
                homeName,
                CreateRatings(
                    match,
                    "HEIM"));
        }

        return new TeamInput(
            awayName,
            CreateRatings(
                match,
                "GAST"));
    }

    private static TeamRatings CreateRatings(
        JsonElement match,
        string prefix)
    {
        return new TeamRatings
        {
            Midfield =
                GetDouble(
                    match,
                    prefix + "MIDFIELD"),

            LeftDefence =
                GetDouble(
                    match,
                    prefix + "LEFTDEF"),

            CentralDefence =
                GetDouble(
                    match,
                    prefix + "MIDDEF"),

            RightDefence =
                GetDouble(
                    match,
                    prefix + "RIGHTDEF"),

            LeftAttack =
                GetDouble(
                    match,
                    prefix + "LEFTATT"),

            CentralAttack =
                GetDouble(
                    match,
                    prefix + "MIDATT"),

            RightAttack =
                GetDouble(
                    match,
                    prefix + "RIGHTATT")
        };
    }

    private static JsonElement FindLatestMatchForTeam(
        JsonElement element,
        string teamName)
    {
        JsonElement latest = default;

        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            if (IsMatchForTeam(
                    element,
                    teamName))
            {
                latest = element;
            }

            foreach (JsonProperty property
                in element.EnumerateObject())
            {
                JsonElement found =
                    FindLatestMatchForTeam(
                        property.Value,
                        teamName);

                if (found.ValueKind !=
                    JsonValueKind.Undefined)
                {
                    latest = ChooseNewerMatch(
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
                JsonElement found =
                    FindLatestMatchForTeam(
                        item,
                        teamName);

                if (found.ValueKind !=
                    JsonValueKind.Undefined)
                {
                    latest = ChooseNewerMatch(
                        latest,
                        found);
                }
            }
        }

        return latest;
    }

    private static bool IsMatchForTeam(
        JsonElement element,
        string teamName)
    {
        if (!element.TryGetProperty(
                "HEIMNAME",
                out JsonElement home))
        {
            return false;
        }

        if (!element.TryGetProperty(
                "GASTNAME",
                out JsonElement away))
        {
            return false;
        }

        string homeName =
            home.GetString() ?? "";

        string awayName =
            away.GetString() ?? "";

        return
            string.Equals(
                homeName,
                teamName,
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                awayName,
                teamName,
                StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement ChooseNewerMatch(
        JsonElement current,
        JsonElement candidate)
    {
        if (current.ValueKind ==
            JsonValueKind.Undefined)
        {
            return candidate;
        }

        DateTime currentDate =
            GetDate(current);

        DateTime candidateDate =
            GetDate(candidate);

        return candidateDate > currentDate
            ? candidate
            : current;
    }

    private static DateTime GetDate(
        JsonElement element)
    {
        if (element.TryGetProperty(
                "SPIELDATUM",
                out JsonElement date))
        {
            if (DateTime.TryParse(
                    date.GetString(),
                    out DateTime result))
            {
                return result;
            }
        }

        return DateTime.MinValue;
    }

    private static string GetString(
        JsonElement element,
        string property)
    {
        if (element.TryGetProperty(
                property,
                out JsonElement value))
        {
            return value.GetString() ?? "";
        }

        return "";
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
}