using Nop.Core.Domain.Logging;
using Nop.Services.Logging;

namespace Nop.Web.Framework.RateLimiting;

/// <summary>
/// What the dashboard shows. Built from Log rows; the Log table is the only source of truth
/// </summary>
public sealed record RateLimitActivity(int EpisodeCount, DateTime? LatestOnUtc, string LatestShortMessage, RateLimitLogQuery Query);

/// <summary>
/// The System Log contract of inbound rate limiting: the row format (write) and the activity query (read)
/// </summary>
public static class RateLimitLog
{
    /// <summary>
    /// Prefix of every short message this feature writes. No brackets, because the message filter becomes a SQL LIKE pattern
    /// </summary>
    public const string MessageMarker = "Rate limit:";

    /// <summary>
    /// Level of every row this feature writes. The dashboard query uses this same value.
    /// </summary>
    public const LogLevel Level = LogLevel.Warning;

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

        return logger.InsertLogAsync(Level, shortMessage, fullMessage);
    }

    /// <summary>
    /// Count and latest episode in the query window. A zero count still carries that query.
    /// </summary>
    public static async Task<RateLimitActivity> ReadActivityAsync(ILogger logger, RateLimitLogQuery query)
    {
        var page = await logger.GetAllLogsAsync(fromUtc: query.SinceUtc, message: query.Message, logLevel: query.Level, pageIndex: 0, pageSize: 1);

        return page.TotalCount == 0
            ? new RateLimitActivity(0, null, null, query)
            : new RateLimitActivity(page.TotalCount, page[0].CreatedOnUtc, page[0].ShortMessage, query);
    }
}
