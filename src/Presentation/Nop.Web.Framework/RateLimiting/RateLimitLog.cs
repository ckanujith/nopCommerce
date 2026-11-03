using Nop.Core.Domain.Logging;
using Nop.Services.Logging;

namespace Nop.Web.Framework.RateLimiting;

public sealed record RateLimitActivity(int EpisodeCount, DateTime? LatestOnUtc, string LatestShortMessage, RateLimitLogQuery Query);

public static class RateLimitLog
{
    public const string MessageMarker = "Rate limit:";

    public const LogLevel Level = LogLevel.Warning;

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

    public static async Task<RateLimitActivity> ReadActivityAsync(ILogger logger, RateLimitLogQuery query)
    {
        var page = await logger.GetAllLogsAsync(fromUtc: query.SinceUtc, message: query.Message, logLevel: query.Level, pageIndex: 0, pageSize: 1);

        return page.TotalCount == 0
            ? new RateLimitActivity(0, null, null, query)
            : new RateLimitActivity(page.TotalCount, page[0].CreatedOnUtc, page[0].ShortMessage, query);
    }
}
