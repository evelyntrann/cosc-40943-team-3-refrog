namespace ReFrog.Client.Tests.IdentityVerification;

/// <summary>
/// A clock tests move by hand (§8.2.2). Timers it creates fire during <see cref="Advance"/>, on the calling thread,
/// so a test never waits on real time.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (timers)
        {
            timers.Add(timer);
        }

        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        now += elapsed;
        List<ManualTimer> due;
        lock (timers)
        {
            due = timers.Where(timer => timer.DueAt is { } at && at <= now).ToList();
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
            return true;
        }

        public void Fire()
        {
            DueAt = null;
            callback(state);
        }

        public void Dispose()
        {
            DueAt = null;
            lock (clock.timers)
            {
                clock.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
