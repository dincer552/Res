namespace HattrickAI.HOEngine;

public class MatchResult
{
    // 0-0 ... 4-4 skorları
    private readonly int[] resultDetail = new int[25];

    private int matchNumber;
    private int homeWin;
    private int awayWin;
    private int draw;

    private int homeGoals;
    private int homeChances;
    private int guestGoals;
    private int guestChances;

    // 0 = sol, 1 = orta, 2 = sağ
    private readonly int[] homeSuccess = { 0, 0, 0 };
    private readonly int[] homeFailed = { 0, 0, 0 };

    private readonly int[] guestSuccess = { 0, 0, 0 };
    private readonly int[] guestFailed = { 0, 0, 0 };


    public void AddActions(Action[] actions)
    {
        matchNumber++;

        int matchHomeGoals = 0;
        int matchHomeChances = 0;

        int matchGuestGoals = 0;
        int matchGuestChances = 0;

        int[] matchHomeSuccess = { 0, 0, 0 };
        int[] matchHomeFailed = { 0, 0, 0 };

        int[] matchGuestSuccess = { 0, 0, 0 };
        int[] matchGuestFailed = { 0, 0, 0 };


        foreach (Action action in actions)
        {
            if (action.IsHomeTeam())
            {
                matchHomeChances++;

                if (action.IsScore())
                {
                    matchHomeGoals++;

                    if (action.GetArea() == -1)
                        matchHomeSuccess[0]++;
                    else if (action.GetArea() == 0)
                        matchHomeSuccess[1]++;
                    else
                        matchHomeSuccess[2]++;
                }
                else
                {
                    if (action.GetArea() == -1)
                        matchHomeFailed[0]++;
                    else if (action.GetArea() == 0)
                        matchHomeFailed[1]++;
                    else
                        matchHomeFailed[2]++;
                }
            }
            else
            {
                matchGuestChances++;

                if (action.IsScore())
                {
                    matchGuestGoals++;

                    if (action.GetArea() == -1)
                        matchGuestSuccess[0]++;
                    else if (action.GetArea() == 0)
                        matchGuestSuccess[1]++;
                    else
                        matchGuestSuccess[2]++;
                }
                else
                {
                    if (action.GetArea() == -1)
                        matchGuestFailed[0]++;
                    else if (action.GetArea() == 0)
                        matchGuestFailed[1]++;
                    else
                        matchGuestFailed[2]++;
                }
            }
        }

        // HO! sonuç tablosunda 4'ten sonraki skorları 4'e topluyor.
        int away = Math.Min(matchGuestGoals, 4);
        int home = Math.Min(matchHomeGoals, 4);

        resultDetail[(home * 5) + away]++;


        homeGoals += matchHomeGoals;
        homeChances += matchHomeChances;

        guestGoals += matchGuestGoals;
        guestChances += matchGuestChances;


        for (int i = 0; i < 3; i++)
        {
            homeSuccess[i] += matchHomeSuccess[i];
            homeFailed[i] += matchHomeFailed[i];

            guestSuccess[i] += matchGuestSuccess[i];
            guestFailed[i] += matchGuestFailed[i];
        }


        if (matchHomeGoals > matchGuestGoals)
        {
            homeWin++;
        }
        else if (matchHomeGoals < matchGuestGoals)
        {
            awayWin++;
        }
        else
        {
            draw++;
        }
    }


    public int GetGuestChances()
    {
        return guestChances;
    }

    public int[] GetGuestFailed()
    {
        return guestFailed;
    }

    public int GetGuestGoals()
    {
        return guestGoals;
    }

    public int[] GetGuestSuccess()
    {
        return guestSuccess;
    }

    public int GetHomeChances()
    {
        return homeChances;
    }

    public int[] GetHomeFailed()
    {
        return homeFailed;
    }

    public int GetHomeGoals()
    {
        return homeGoals;
    }

    public int[] GetHomeSuccess()
    {
        return homeSuccess;
    }

    public int GetHomeWin()
    {
        return homeWin;
    }

    public int GetAwayWin()
    {
        return awayWin;
    }

    public int GetDraw()
    {
        return draw;
    }

    public int GetMatchNumber()
    {
        return matchNumber;
    }

    public int[] GetResultDetail()
    {
        return resultDetail;
    }
}