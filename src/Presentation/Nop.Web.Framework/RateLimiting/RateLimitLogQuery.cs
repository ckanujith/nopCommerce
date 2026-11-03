using Nop.Core.Domain.Logging;

namespace Nop.Web.Framework.RateLimiting;

/// <summary>
/// The only dashboard window. It counts <see cref="RateLimitLog.Level"/> rows that match <see cref="RateLimitLog.MessageMarker"/>, for <see cref="Length"/> before a UTC clock.
/// </summary>
public readonly record struct RateLimitLogQuery
{
    public static readonly TimeSpan Length = TimeSpan.FromHours(24);

    public DateTime SinceUtc { get; }

    public LogLevel Level { get; }

    public string Message { get; }

    private RateLimitLogQuery(DateTime sinceUtc)
    {
        SinceUtc = sinceUtc;
        Level = RateLimitLog.Level;
        Message = RateLimitLog.MessageMarker;
    }

    /// <summary>
    /// The window the Home card counts and the log link opens.
    /// </summary>
    /// <param name="utcNow">UTC clock. Any other <see cref="DateTimeKind"/> is rejected.</param>
    public static RateLimitLogQuery Dashboard(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The dashboard clock must be UTC.", nameof(utcNow));

        return new(utcNow - Length);
    }
}
