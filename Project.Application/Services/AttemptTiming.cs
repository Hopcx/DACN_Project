namespace Project.Application.Services;

public static class AttemptTiming
{
    // The caller supplies server UTC; tests can pass an exact boundary instant.
    public static bool IsExpired(DateTime serverNowUtc, DateTime deadlineUtc) => serverNowUtc >= deadlineUtc;

    public static bool CanStart(DateTime nowUtc, DateTime startUtc, DateTime endUtc) =>
        startUtc <= nowUtc && nowUtc < endUtc;

    public static DateTime Deadline(DateTime startedUtc, int durationMinutes, DateTime scheduleEndUtc)
    {
        if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        var durationEnd = startedUtc.AddMinutes(durationMinutes);
        return durationEnd < scheduleEndUtc ? durationEnd : scheduleEndUtc;
    }
}
