using Microsoft.AspNetCore.Http;
using Nop.Core.Configuration;

namespace Nop.Web.Framework.RateLimiting;

public enum TrafficClass
{
    Storefront = 0,

    Admin = 1
}

/// <summary>
/// Exponential penalty schedule: penalty(n) = max(windowRetryAfter, min(Ceiling, Window * 2^(n-1))).
/// A zero ceiling means no escalation: the penalty is always what the window itself reported
/// </summary>
public sealed record PenaltySchedule
{
    public PenaltySchedule(TimeSpan window, TimeSpan ceiling)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(ceiling, TimeSpan.Zero);

        Window = window;
        Ceiling = ceiling == TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Max(ceiling.Ticks, window.Ticks));
    }

    public static PenaltySchedule None(TimeSpan window) => new(window, TimeSpan.Zero);

    public TimeSpan Window { get; }

    public TimeSpan Ceiling { get; }

    /// <param name="streak">1-based consecutive violation count</param>
    /// <param name="windowRetryAfter">What the inner fixed window itself reported</param>
    public TimeSpan PenaltyFor(int streak, TimeSpan windowRetryAfter)
    {
        if (Ceiling == TimeSpan.Zero)
            return windowRetryAfter;

        var penalty = Window;
        for (var i = 1; i < streak && penalty < Ceiling; i++)
            penalty += penalty;

        return TimeSpan.FromTicks(Math.Max(windowRetryAfter.Ticks, Math.Min(Ceiling.Ticks, penalty.Ticks)));
    }
}

public sealed record InboundRateLimitPolicy
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    private InboundRateLimitPolicy()
    {
    }

    public int PermitLimit { get; private init; }

    public int QueueLimit { get; private init; }

    public TimeSpan Window { get; private init; }

    public int RejectionStatusCode { get; private init; }

    public PenaltySchedule Storefront { get; private init; }

    public PenaltySchedule Admin { get; private init; }

    public PenaltySchedule ScheduleFor(TrafficClass traffic) => traffic == TrafficClass.Admin ? Admin : Storefront;

    /// <returns>Null when the permit limit is not positive, meaning rate limiting is off</returns>
    public static InboundRateLimitPolicy FromConfig(CommonConfig config) => FromConfig(config, DefaultWindow);

    /// <returns>Null when the permit limit is not positive, meaning rate limiting is off</returns>
    public static InboundRateLimitPolicy FromConfig(CommonConfig config, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.PermitLimit <= 0)
            return null;

        var ceiling = config.MaxPenaltyMinutes <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(config.MaxPenaltyMinutes);

        return new InboundRateLimitPolicy
        {
            PermitLimit = config.PermitLimit,
            QueueLimit = Math.Max(0, config.QueueCount),
            Window = window,
            RejectionStatusCode = config.RejectionStatusCode is >= 400 and <= 599
                ? config.RejectionStatusCode
                : StatusCodes.Status429TooManyRequests,
            Storefront = new PenaltySchedule(window, ceiling),
            Admin = PenaltySchedule.None(window)
        };
    }
}
