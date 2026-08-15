namespace HattrickAI.HOEngine;

public sealed class TrainingAnalyzer
{
    public IReadOnlyList<PlayerDevelopmentSnapshot> BuildSnapshots(
        IEnumerable<TrainingReport> reports,
        IEnumerable<PlayerData> players)
    {
        var playerMap = players.ToDictionary(p => p.PlayerId);
        var events = reports
            .SelectMany(r => r.Events)
            .GroupBy(e => e.PlayerId);

        var result = new List<PlayerDevelopmentSnapshot>();

        foreach (var group in events)
        {
            if (!playerMap.TryGetValue(group.Key, out var player))
                continue;

            var list = group.ToList();
            double skill = list.Where(e => e.Category == TrainingCategory.Skill).Sum(e => e.After - e.Before);
            double form = list.Where(e => e.Category == TrainingCategory.Form).Sum(e => e.After - e.Before);
            double stamina = list.Where(e => e.Category == TrainingCategory.Stamina).Sum(e => e.After - e.Before);
            double growth = skill + form * .35 + stamina * .25;

            result.Add(new PlayerDevelopmentSnapshot
            {
                PlayerId = player.PlayerId,
                PlayerName = player.Name,
                WeeksObserved = reports.Count(),
                SkillGrowth = skill,
                FormGrowth = form,
                StaminaGrowth = stamina,
                Trend = growth switch
                {
                    > 1.5 => "Hızlı gelişiyor",
                    > .25 => "Gelişiyor",
                    < -.5 => "Geriliyor",
                    _ => "Stabil"
                }
            });
        }

        return result
            .OrderByDescending(x => x.SkillGrowth)
            .ThenByDescending(x => x.FormGrowth)
            .ToList();
    }
}
