namespace HattrickAI.HOEngine;

public class Action
{
    public bool HomeTeam { get; set; }

    public bool Score { get; set; }

    // -1 = sol, 0 = orta, 1 = sağ
    public int Area { get; set; }

    public int Minute { get; set; }

    // 0 = normal atak
    // 2 = kontra (HO!)
    public int Type { get; set; }

    public bool IsHomeTeam()
    {
        return HomeTeam;
    }

    public bool IsScore()
    {
        return Score;
    }

    public int GetArea()
    {
        return Area;
    }

    public int GetMinute()
    {
        return Minute;
    }

    public new int GetType()
    {
        return Type;
    }

    public override string ToString()
    {
        string team = HomeTeam ? "Ev sahibi" : "Deplasman";

        string area = Area switch
        {
            -1 => "Sol",
            0 => "Orta",
            1 => "Sağ",
            _ => "Bilinmiyor"
        };

        string result = Score ? "GOL!" : "kaçan şans";

        return $"{Minute}. dk - {team} - {area} - {result}";
    }
}