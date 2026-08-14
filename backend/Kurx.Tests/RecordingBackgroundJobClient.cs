using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace Kurx.Tests;

/// <summary>
/// Records enqueued Hangfire jobs instead of running them.
///
/// The test host registers a real Hangfire server, so anything enqueued during a test executes on a
/// background worker against <c>kurx_test</c> — which races the drop/re-migrate the next test class
/// performs (<c>57P01: terminating connection due to administrator command</c>) and makes any
/// assertion about a fan-out nondeterministic. Recording instead keeps enqueue observable while the
/// test decides when, and whether, the job body runs.
/// </summary>
public class RecordingBackgroundJobClient : IBackgroundJobClient
{
    private readonly List<Job> _jobs = [];
    private readonly object _lock = new();

    public IReadOnlyList<Job> Jobs
    {
        get { lock (_lock) return _jobs.ToList(); }
    }

    /// <summary>True when a job was enqueued for <paramref name="method"/> with an argument equal to
    /// <paramref name="firstArg"/> — enough to assert "this message's fan-out was scheduled".</summary>
    public bool WasEnqueued(string method, object firstArg)
    {
        lock (_lock)
            return _jobs.Any(j => j.Method.Name == method
                && j.Args.Count > 0 && Equals(j.Args[0], firstArg));
    }

    public void Clear()
    {
        lock (_lock) _jobs.Clear();
    }

    public string Create(Job job, IState state)
    {
        lock (_lock) _jobs.Add(job);
        return Guid.NewGuid().ToString();
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;
}
