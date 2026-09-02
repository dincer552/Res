namespace HattrickAI.V5.Core;

public static class MotorRunLogContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? CurrentRunId => Current.Value;

    public static IDisposable Push(string runId)
    {
        var previous = Current.Value;
        Current.Value = runId;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
