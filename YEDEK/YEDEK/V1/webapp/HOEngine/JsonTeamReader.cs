using System.Text.Json;

namespace HattrickAI.HOEngine;

public class JsonTeamReader
{
    public TeamInput Read(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;

        string teamName =
            root.TryGetProperty("teamName", out JsonElement name)
                ? name.GetString() ?? "Bilinmeyen Takım"
                : "Bilinmeyen Takım";

        JsonElement ratingsElement =
            root.GetProperty("ratings");

        var ratings = new TeamRatings
        {
            Midfield = GetDouble(ratingsElement, "midfield"),

            LeftDefence =
                GetDouble(ratingsElement, "leftDefence"),

            CentralDefence =
                GetDouble(ratingsElement, "centralDefence"),

            RightDefence =
                GetDouble(ratingsElement, "rightDefence"),

            LeftAttack =
                GetDouble(ratingsElement, "leftAttack"),

            CentralAttack =
                GetDouble(ratingsElement, "centralAttack"),

            RightAttack =
                GetDouble(ratingsElement, "rightAttack")
        };

        int tacticType =
            GetInt(root, "tacticType");

        int tacticLevel =
            GetInt(root, "tacticLevel");

        return new TeamInput(
            teamName,
            ratings)
        {
            TacticType = tacticType,
            TacticLevel = tacticLevel
        };
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

        return 0;
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

        return 0;
    }
}