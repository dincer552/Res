using HattrickAI.V5.Core;

var tests = new List<(string Name, Action Run)>
{
    ("Goalkeeper central + both side defences", TestGoalkeeper),
    ("Central defender on left", TestLeftCentralDefender),
    ("Central defender in centre splits side defence", TestCentralDefender),
    ("Offensive central defender adds midfield", TestOffensiveCentralDefender),
    ("Normal winger feeds side attack", TestWinger),
    ("Normal forward in centre splits side attack", TestCentralForward),
    ("Towards-wing forward sends extra winger contribution to opposite side", TestForwardTowardsWing),
    ("Display quarter buckets", TestDisplay),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL  {test.Name}: {ex.Message}");
        Console.WriteLine(failures[^1]);
    }
}

Console.WriteLine($"\nPhase 1: {tests.Count - failures.Count}/{tests.Count} tests passed.");
if (failures.Count > 0)
    Environment.Exit(1);

static RegionalRatingEngine Engine() => new();

static RegionalPlayer Player(
    RegionalPosition position,
    PlayerSide side = PlayerSide.Center,
    PlayerOrder order = PlayerOrder.Normal,
    double keeper = 0,
    double defending = 0,
    double playmaking = 0,
    double passing = 0,
    double winger = 0,
    double scoring = 0)
    => new(1, position, side, order, keeper, defending, playmaking, passing, winger, scoring, 8, 0, 0);

static RegionalRatingSnapshot Calc(RegionalPlayer player)
    => Engine().Calculate(new[] { player });

static void Near(double actual, double expected, string name)
{
    if (Math.Abs(actual - expected) > 0.000001)
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
}

static void TestGoalkeeper()
{
    var r = Calc(Player(RegionalPosition.Goalkeeper, keeper: 10));
    Near(r.RawCentralDefence, 1.650, "central defence");
    Near(r.RawLeftDefence, 0.915, "left defence");
    Near(r.RawRightDefence, 0.915, "right defence");
}

static void TestLeftCentralDefender()
{
    var r = Calc(Player(RegionalPosition.CentralDefender, PlayerSide.Left, defending: 10));
    Near(r.RawCentralDefence, 1.860, "central defence");
    Near(r.RawLeftDefence, 0.770, "left defence");
    Near(r.RawRightDefence, 0, "right defence");
}

static void TestCentralDefender()
{
    var r = Calc(Player(RegionalPosition.CentralDefender, PlayerSide.Center, defending: 10));
    Near(r.RawCentralDefence, 1.860, "central defence");
    Near(r.RawLeftDefence, 0.385, "left defence");
    Near(r.RawRightDefence, 0.385, "right defence");
}

static void TestOffensiveCentralDefender()
{
    var r = Calc(Player(RegionalPosition.CentralDefender, PlayerSide.Center, PlayerOrder.Offensive, defending: 10, playmaking: 10));
    Near(r.RawCentralDefence, 1.300, "central defence");
    Near(r.RawLeftDefence, 0.290, "left defence");
    Near(r.RawRightDefence, 0.290, "right defence");
    Near(r.RawMidfield, 0.470, "midfield");
}

static void TestWinger()
{
    var r = Calc(Player(RegionalPosition.Winger, PlayerSide.Left, winger: 10));
    Near(r.RawLeftAttack, 0.540, "left attack");
}

static void TestCentralForward()
{
    var r = Calc(Player(RegionalPosition.Forward, PlayerSide.Center, scoring: 10));
    Near(r.RawCentralAttack, 0.660, "central attack");
    Near(r.RawLeftAttack, 0.290, "left attack");
    Near(r.RawRightAttack, 0.290, "right attack");
}

static void TestForwardTowardsWing()
{
    var r = Calc(Player(RegionalPosition.Forward, PlayerSide.Left, PlayerOrder.TowardsWing, winger: 10));
    Near(r.RawLeftAttack, 0.440, "same-side wing contribution");
    Near(r.RawRightAttack, 0.170, "opposite-side wing contribution");
}

static void TestDisplay()
{
    Near(RegionalRatingEngine.Display(0.00), 0.75, "0.00");
    Near(RegionalRatingEngine.Display(0.24), 0.75, "0.24");
    Near(RegionalRatingEngine.Display(0.25), 1.00, "0.25");
    Near(RegionalRatingEngine.Display(0.50), 1.25, "0.50");
    Near(RegionalRatingEngine.Display(11.25), 12.00, "11.25");
}
