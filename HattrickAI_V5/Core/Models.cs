using System.Text.Json.Serialization;

namespace HattrickAI.V5.Core;

public enum PlayerSpecialty
{
    None = 0,
    Technical = 1,
    Quick = 2,
    Powerful = 3,
    Unpredictable = 4,
    Head = 5
}

public sealed record Player(
    int Id,
    string Name,
    int Keeper,
    int Defending,
    int Playmaking,
    int Passing,
    int Winger,
    int Scoring,
    int Stamina,
    int Form,
    int Experience,
    int Loyalty = 0,
    int InjuryLevel = -1,
    PlayerSpecialty Specialty = PlayerSpecialty.None,
    int SetPiecesSkill = 0);

public sealed record Slot(string Code,string Label,string Description,string? PlayerName,int PlayerId,double Rating,double X,double Y,PlayerOrder Order=PlayerOrder.Normal,double? HistoricalStars=null);
public sealed record Lineup(string TeamName,string Formation,IReadOnlyList<Slot> Slots)
{
    [JsonIgnore] public IReadOnlyList<Slot> Slots { get; init; } = Slots;
    [JsonPropertyName("slots")] public IReadOnlyList<Slot> DisplaySlots => NormalizeDisplaySlots(Slots);
    private static IReadOnlyList<Slot> NormalizeDisplaySlots(IReadOnlyList<Slot> source)
    {
        var used=new HashSet<string>(StringComparer.Ordinal); var result=new List<Slot>(source.Count);
        foreach(var slot in source){var code=slot.Code;if(!used.Add(code))foreach(var alternative in Alternatives(code))if(used.Add(alternative)){code=alternative;break;}result.Add(code==slot.Code?slot:slot with{Code=code});}return result;
    }
    private static IEnumerable<string> Alternatives(string code)=>code switch
    {
        "DEF-C" => ["DEF-CL","DEF-CR","DEF-L","DEF-R"], "DEF-CL" => ["DEF-C","DEF-CR","DEF-L","DEF-R"], "DEF-CR" => ["DEF-C","DEF-CL","DEF-R","DEF-L"], "DEF-L" => ["DEF-CL","DEF-C","DEF-CR","DEF-R"], "DEF-R" => ["DEF-CR","DEF-C","DEF-CL","DEF-L"],
        "IM-C" => ["IM-L","IM-R","W-L","W-R"], "IM-L" => ["IM-C","IM-R","W-L","W-R"], "IM-R" => ["IM-C","IM-L","W-R","W-L"], "W-L" => ["IM-L","IM-C","W-R","IM-R"], "W-R" => ["IM-R","IM-C","W-L","IM-L"],
        "FW-C" => ["FW-L","FW-R"], "FW-L" => ["FW-C","FW-R"], "FW-R" => ["FW-C","FW-L"], _ => []
    };
}

public sealed record Analysis(string Build,string TeamName,string OpponentName,string MatchTitle,Lineup Own,Lineup Opponent,RegionalRatingSnapshot OwnRating,RegionalRatingSnapshot OpponentRating,MatchQuestionnaire AppliedQuestionnaire)
{
    public Lineup OwnLineup=>Own; public Lineup OpponentLineup=>Opponent; public string OwnFormation=>Own.Formation; public string OpponentFormation=>Opponent.Formation;
    public RegionalRatingPair RegionalRatings=>new(OwnRating,OpponentRating); public OpponentThreatMap OpponentThreat=>new OpponentThreatEngine().Analyze(OpponentRating);
    public RatingScenarioResult? M7Scenario { get; init; }
    public AdvancedTacticalScenarioResult? M72Scenario { get; init; }
    public M8ChanceResult? M8Chance { get; init; }
    public M9PredictionResult? M9Prediction { get; init; }
    public M10DecisionResult? M10Decision { get; init; }
    [JsonIgnore] public MotorPipelineResult? MotorPipeline { get; init; }
}
