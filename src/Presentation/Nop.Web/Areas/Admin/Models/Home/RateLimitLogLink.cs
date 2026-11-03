using System.Globalization;
using Nop.Web.Areas.Admin.Models.Logging;
using Nop.Web.Framework.RateLimiting;

namespace Nop.Web.Areas.Admin.Models.Home;

public readonly record struct RateLimitLogLink
{
    public string Message { get; }

    public int LogLevelId { get; }

    public DateTime CreatedOnFrom { get; }

    private RateLimitLogLink(string message, int logLevelId, DateTime createdOnFrom)
    {
        Message = message;
        LogLevelId = logLevelId;
        CreatedOnFrom = createdOnFrom;
    }

    public static RateLimitLogLink From(RateLimitLogQuery query, TimeZoneInfo userZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(query.SinceUtc, DateTimeKind.Utc), userZone);

        return new RateLimitLogLink(query.Message, (int)query.Level, local.Date);
    }

    public Dictionary<string, string> RouteValues()
    {
        return new Dictionary<string, string>
        {
            [nameof(LogSearchModel.Message)] = Message,
            [nameof(LogSearchModel.LogLevelId)] = LogLevelId.ToString(CultureInfo.InvariantCulture),
            [nameof(LogSearchModel.CreatedOnFrom)] = CreatedOnFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
    }
}
