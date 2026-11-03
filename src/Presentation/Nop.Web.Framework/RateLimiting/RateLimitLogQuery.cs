using Nop.Core.Domain.Logging;

namespace Nop.Web.Framework.RateLimiting;

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

    /// <param name="utcNow">UTC clock. Any other <see cref="DateTimeKind"/> is rejected.</param>
    public static RateLimitLogQuery Dashboard(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The dashboard clock must be UTC.", nameof(utcNow));

        return new(utcNow - Length);
    }
}
