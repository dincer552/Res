namespace HattrickAI.HOEngine;

public enum TrainingCategory
{
    Skill,
    Stamina,
    Form,
    Unknown
}

public sealed record TrainingEvent(
    int PlayerId,
    string PlayerName,
    TrainingCategory Category,
    string Skill,
    int Before,
    int After,
    DateTime Date);

public sealed class TrainingReport
{
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public string TrainingType { get; init; } = "";
    public int TrainingIntensity { get; init; }
    public int StaminaShare { get; init; }
    public List<TrainingEvent> Events { get; } = new();

    public IEnumerable<TrainingEvent> SkillChanges =>
        Events.Where(x => x.Category == TrainingCategory.Skill);

    public IEnumerable<TrainingEvent> FormChanges =>
        Events.Where(x => x.Category == TrainingCategory.Form);

    public IEnumerable<TrainingEvent> StaminaChanges =>
        Events.Where(x => x.Category == TrainingCategory.Stamina);
}

public sealed class PlayerDevelopmentSnapshot
{
    public int PlayerId { get; init; }
    public string PlayerName { get; init; } = "";
    public int WeeksObserved { get; init; }
    public double SkillGrowth { get; init; }
    public double FormGrowth { get; init; }
    public double StaminaGrowth { get; init; }
    public string Trend { get; init; } = "Stabil";
}
