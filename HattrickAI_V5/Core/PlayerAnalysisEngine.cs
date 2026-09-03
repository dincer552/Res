namespace HattrickAI.V5.Core;

/// <summary>
/// Motor 3: Oyuncu Analiz Motoru.
/// Sadece oyuncu uygunluk profilini üretir. XI seçmez, diziliş seçmez,
/// rakip skoru kullanmaz ve takım ratingi üretmez.
/// Specialty bu aşamada rating bonusu olarak uygulanmaz; oyuncu profiline
/// taşınır ve sonraki motorların context'i için korunur.
/// </summary>
public sealed class PlayerAnalysisEngine : IPlayerAnalysisEngine
{
    private static readonly string[] PositionCodes =
    [
        "GK",
        "DEF-L", "DEF-CL", "DEF-C", "DEF-CR", "DEF-R",
        "W-L", "IM-L", "IM-C", "IM-R", "W-R",
        "FW-L", "FW-C", "FW-R"
    ];

    public PlayerAnalysisResult Analyze(IReadOnlyList<Player> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        return new PlayerAnalysisResult(players.Select(AnalyzePlayer).ToList());
    }

    public PlayerAnalysisProfile AnalyzePlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var eligible = IsEligible(player);
        var candidates = PositionCodes
            .Select(code => new PlayerPositionCandidate(code, eligible ? Score(player, code) : double.NegativeInfinity))
            .Where(x => !double.IsNegativeInfinity(x.Score))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => PositionOrder(x.PositionCode))
            .ToList();

        return new PlayerAnalysisProfile(
            player.Id,
            player.Name,
            eligible,
            player.InjuryLevel,
            player.Specialty,
            BuildSpecialtyProfile(player.Specialty),
            candidates,
            candidates.FirstOrDefault()?.PositionCode,
            candidates.Skip(1).FirstOrDefault()?.PositionCode);
    }

    public double Score(Player player, string positionCode)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!IsEligible(player)) return double.NegativeInfinity;

        return positionCode switch
        {
            "GK" => player.Keeper + player.Form * .15,
            "DEF-L" or "DEF-R" => player.Defending + player.Passing * .10 + player.Winger * .05,
            "DEF-C" or "DEF-CL" or "DEF-CR" => player.Defending * 1.05 + player.Passing * .15 + player.Playmaking * .04,
            "W-L" or "W-R" => player.Winger + player.Passing * .22 + player.Playmaking * .08,
            "IM-L" or "IM-R" => player.Playmaking + player.Passing * .25 + player.Stamina * .12,
            "IM-C" => player.Playmaking * 1.05 + player.Passing * .25 + player.Stamina * .12 + player.Experience * .04,
            "FW-L" or "FW-R" => player.Scoring + player.Passing * .18 + player.Winger * .08 + player.Experience * .02,
            "FW-C" => player.Scoring * 1.05 + player.Passing * .20 + player.Playmaking * .04,
            _ => double.NegativeInfinity
        };
    }

    private static PlayerSpecialtyProfile BuildSpecialtyProfile(PlayerSpecialty specialty)
        => specialty switch
        {
            PlayerSpecialty.Technical => new(
                specialty,
                HasSpecialEventContext: true,
                HasWeatherInteraction: true,
                HasCounterAttackInteraction: false,
                HasPressingInteraction: false,
                HasQuickEventInteraction: false,
                HasHeaderInteraction: true,
                HasPlayCreativelyInteraction: false,
                Notes: "Technical specialty; weather and Technical-vs-Head interactions are retained for later event resolution."),

            PlayerSpecialty.Quick => new(
                specialty,
                HasSpecialEventContext: true,
                HasWeatherInteraction: false,
                HasCounterAttackInteraction: true,
                HasPressingInteraction: false,
                HasQuickEventInteraction: true,
                HasHeaderInteraction: false,
                HasPlayCreativelyInteraction: false,
                Notes: "Quick specialty; quick events and counter-attack interaction are retained for later tactical/event resolution."),

            PlayerSpecialty.Powerful => new(
                specialty,
                HasSpecialEventContext: true,
                HasWeatherInteraction: true,
                HasCounterAttackInteraction: false,
                HasPressingInteraction: true,
                HasQuickEventInteraction: false,
                HasHeaderInteraction: false,
                HasPlayCreativelyInteraction: false,
                Notes: "Powerful specialty; weather and pressing interactions are retained for later tactical/event resolution."),

            PlayerSpecialty.Unpredictable => new(
                specialty,
                HasSpecialEventContext: true,
                HasWeatherInteraction: false,
                HasCounterAttackInteraction: false,
                HasPressingInteraction: false,
                HasQuickEventInteraction: false,
                HasHeaderInteraction: false,
                HasPlayCreativelyInteraction: true,
                Notes: "Unpredictable specialty; unexpected actions and Play Creatively interaction are retained for later event resolution."),

            PlayerSpecialty.Head => new(
                specialty,
                HasSpecialEventContext: true,
                HasWeatherInteraction: false,
                HasCounterAttackInteraction: false,
                HasPressingInteraction: false,
                HasQuickEventInteraction: false,
                HasHeaderInteraction: true,
                HasPlayCreativelyInteraction: false,
                Notes: "Head specialty; own header and opponent anti-header interaction are retained for later event resolution."),

            _ => new(
                PlayerSpecialty.None,
                HasSpecialEventContext: false,
                HasWeatherInteraction: false,
                HasCounterAttackInteraction: false,
                HasPressingInteraction: false,
                HasQuickEventInteraction: false,
                HasHeaderInteraction: false,
                HasPlayCreativelyInteraction: false,
                Notes: "No specialty; no specialty-specific event context."),
        };

    private static bool IsEligible(Player player)
        => player.Id > 0 && player.InjuryLevel != 999;

    private static int PositionOrder(string code) => code switch
    {
        "GK" => 0,
        "DEF-L" => 10,
        "DEF-CL" => 11,
        "DEF-C" => 12,
        "DEF-CR" => 13,
        "DEF-R" => 14,
        "W-L" => 20,
        "IM-L" => 21,
        "IM-C" => 22,
        "IM-R" => 23,
        "W-R" => 24,
        "FW-L" => 30,
        "FW-C" => 31,
        "FW-R" => 32,
        _ => 99
    };
}

public sealed record PlayerPositionCandidate(string PositionCode, double Score);

public sealed record PlayerSpecialtyProfile(
    PlayerSpecialty Specialty,
    bool HasSpecialEventContext,
    bool HasWeatherInteraction,
    bool HasCounterAttackInteraction,
    bool HasPressingInteraction,
    bool HasQuickEventInteraction,
    bool HasHeaderInteraction,
    bool HasPlayCreativelyInteraction,
    string Notes);

public sealed record PlayerAnalysisProfile(
    int PlayerId,
    string PlayerName,
    bool IsEligible,
    int InjuryLevel,
    PlayerSpecialty Specialty,
    PlayerSpecialtyProfile SpecialtyProfile,
    IReadOnlyList<PlayerPositionCandidate> Positions,
    string? PrimaryPosition,
    string? SecondaryPosition)
{
    public double PrimaryScore => Positions.Count == 0 ? double.NegativeInfinity : Positions[0].Score;
    public double SecondaryScore => Positions.Count < 2 ? double.NegativeInfinity : Positions[1].Score;
}
