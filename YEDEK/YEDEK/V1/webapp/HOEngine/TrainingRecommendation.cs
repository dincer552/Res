namespace HattrickAI.HOEngine;

public sealed class TrainingRecommendationProfile
{
    public int TrainingType { get; init; }
    public string TrainingName { get; init; } = "Bilinmiyor";
    public int TrainingLevel { get; init; }
    public int StaminaTrainingPart { get; init; }
    public IReadOnlyDictionary<string, int> FormationExperience { get; init; } = new Dictionary<string, int>();

    public int Experience(string formation) => FormationExperience.TryGetValue(formation, out var value) ? value : 0;

    // 2 = maximum/primary training fit among the formations supported by this app.
    // 1 = useful but not maximum training fit.
    // 0 = poor training fit for the selected training type.
    public int TrainingFit(string formation) => TrainingType switch
    {
        0 => 1, // General
        1 => 1, // Stamina
        2 => 1, // Set pieces: formation-independent
        3 => formation is "5-4-1" or "5-3-2" ? 2 : 1,
        4 => formation is "3-4-3" or "4-3-3" ? 2 : 1,
        5 => 1, // Crossing: supported standard formations can field the winger/wingback slots.
        6 => 1, // Shooting: formation-independent
        7 => formation is "3-4-3" or "3-5-2" or "4-3-3" ? 2 : 1,
        8 => formation is "3-5-2" or "4-5-1" ? 2 : 1,
        9 => 1, // Goalkeeping: formation-independent
        10 => formation is "4-5-1" or "5-4-1" ? 2 : 1,
        11 => formation is "5-4-1" or "4-5-1" ? 2 : 1,
        _ => 1
    };

    public string PriorityText(string formation) => TrainingFit(formation) switch
    {
        2 => "Antrenmana tam uygun",
        1 => "Antrenman kaybı düşük/uygun",
        _ => "Antrenman için öncelikli değil"
    };

    public string PreferredFormationText()
    {
        var candidates = new[] { "4-3-3", "4-5-1", "3-5-2", "5-3-2", "3-4-3", "5-4-1" }
            .Where(f => TrainingFit(f) == 2)
            .OrderByDescending(Experience)
            .ToArray();
        return candidates.Length == 0 ? "4-4-2" : string.Join(" / ", candidates);
    }
}
