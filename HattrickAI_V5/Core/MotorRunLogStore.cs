using System.Collections.Concurrent;

namespace HattrickAI.V5.Core;

public sealed record MotorLogStage(
    string Motor,
    string Status,
    string Message,
    long DurationMs,
    int? CurrentIteration = null,
    int? MaxIterations = null,
    int? CandidateCount = null,
    DateTimeOffset? UpdatedAt = null);

public sealed record MotorRunLog(
    string RunId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string FinalMessage,
    IReadOnlyList<MotorLogStage> Stages);

public static class MotorRunLogStore
{
    private static readonly ConcurrentDictionary<string, RunState> Runs = new(StringComparer.Ordinal);
    private const int MaxRuns = 24;

    public static string Start()
    {
        var runId = $"mrun-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var stages = new[] { "M3", "M4", "M5", "M6", "M7", "M7.2", "M8", "M9", "M10" }
            .Select(m => new MotorLogStage(m, "pending", "Bekliyor", 0, UpdatedAt: now)).ToList();
        Runs[runId] = new RunState(runId, now, now, "running", "", stages);
        Trim();
        return runId;
    }

    public static MotorRunLog? Get(string runId) => Runs.TryGetValue(runId, out var run) ? run.Snapshot() : null;

    public static void StartMotor(string runId, string motor, string message = "Çalışıyor")
        => Update(runId, motor, "running", message, 0);

    public static void CompleteMotor(string runId, string motor, string message, long durationMs = 0, int? candidateCount = null)
        => Update(runId, motor, "completed", message, durationMs, null, null, candidateCount);

    public static void FailMotor(string runId, string motor, string message, long durationMs = 0)
        => Update(runId, motor, "failed", message, durationMs);

    public static void Progress(string runId, string motor, string message, int current, int maximum)
        => Update(runId, motor, "running", message, 0, current, maximum);

    public static void Finish(string runId, bool success, string message)
    {
        if (!Runs.TryGetValue(runId, out var run)) return;
        lock (run.Sync)
        {
            run.Status = success ? "completed" : "failed";
            run.UpdatedAt = DateTimeOffset.UtcNow;
            run.FinalMessage = message;
        }
    }

    private static void Update(string runId, string motor, string status, string message, long durationMs, int? current = null, int? maximum = null, int? candidateCount = null)
    {
        if (!Runs.TryGetValue(runId, out var run)) return;
        lock (run.Sync)
        {
            var index = run.Stages.FindIndex(x => string.Equals(x.Motor, motor, StringComparison.Ordinal));
            if (index < 0) return;
            var old = run.Stages[index];
            run.Stages[index] = old with
            {
                Status = status,
                Message = message,
                DurationMs = durationMs > 0 ? durationMs : old.DurationMs,
                CurrentIteration = current ?? old.CurrentIteration,
                MaxIterations = maximum ?? old.MaxIterations,
                CandidateCount = candidateCount ?? old.CandidateCount,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            run.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void Trim()
    {
        foreach (var key in Runs.OrderByDescending(x => x.Value.StartedAt).Skip(MaxRuns).Select(x => x.Key))
            Runs.TryRemove(key, out _);
    }

    private sealed class RunState
    {
        public RunState(string runId, DateTimeOffset startedAt, DateTimeOffset updatedAt, string status, string finalMessage, List<MotorLogStage> stages)
        {
            RunId = runId; StartedAt = startedAt; UpdatedAt = updatedAt; Status = status; FinalMessage = finalMessage; Stages = stages;
        }
        public object Sync { get; } = new();
        public string RunId { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public string FinalMessage { get; set; }
        public List<MotorLogStage> Stages { get; }
        public MotorRunLog Snapshot() { lock (Sync) return new MotorRunLog(RunId, Status, StartedAt, UpdatedAt, FinalMessage, Stages.ToList()); }
    }
}
