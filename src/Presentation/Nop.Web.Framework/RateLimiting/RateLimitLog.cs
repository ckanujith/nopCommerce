using Nop.Core.Domain.Logging;
using Nop.Services.Logging;

namespace Nop.Web.Framework.RateLimiting;

/// <summary>
/// What the dashboard shows. Built from Log rows; the Log table is the only source of truth
/// </summary>
public sealed record RateLimitActivity(int EpisodeCount, DateTime? LatestOnUtc, string LatestShortMessage)
{
    public static RateLimitActivity None { get; } = new(0, null, null);
}

/// <summary>
/// The System Log contract of inbound rate limiting: the row format (write) and the activity query (read)
/// </summary>
public static class RateLimitLog
{
    /// <summary>
    /// Prefix of every short message this feature writes. No brackets, because the message filter becomes a SQL LIKE pattern
    /// </summary>
    public const string MessageMarker = "Rate limit:";

    /// <remarks>
    /// The short message is rendered as HTML in the admin area, so it holds only enum names and numbers.
    /// IP address and page URL are filled by the logger from the current request
    /// </remarks>
    internal static Task WriteEpisodeAsync(ILogger logger, PenaltyEpisode episode, int suppressedSinceLastWrite)
    {
        var seconds = (long)Math.Ceiling(episode.Penalty.TotalSeconds);
        var shortMessage = $"{MessageMarker} {episode.Traffic} client penalized: violation #{episode.Streak}, blocked for {seconds} s.";
        var fullMessage = string.Join(Environment.NewLine,
            $"Traffic: {episode.Traffic}",
            $"Violation streak: {episode.Streak}",
            $"Penalty: {episode.Penalty}",
            $"Requests rejected during the previous penalty: {episode.RejectedDuringPreviousPenalty}",
            $"Episodes not logged since the last entry (global log budget): {suppressedSinceLastWrite}");

        return logger.InsertLogAsync(LogLevel.Warning, shortMessage, fullMessage);
    }

    /// <summary>
    /// Count and latest episode logged since the passed date
    /// </summary>
    public static async Task<RateLimitActivity> ReadActivityAsync(ILogger logger, DateTime sinceUtc)
    {
        var page = await logger.GetAllLogsAsync(fromUtc: sinceUtc, message: MessageMarker, logLevel: LogLevel.Warning, pageIndex: 0, pageSize: 1);

        return page.TotalCount == 0
            ? RateLimitActivity.None
            : new RateLimitActivity(page.TotalCount, page[0].CreatedOnUtc, page[0].ShortMessage);
    }
}
