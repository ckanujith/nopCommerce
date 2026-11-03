using System.Diagnostics;

namespace Nop.Web.Framework.RateLimiting;

/// <summary>
/// One client's standing. Immutable and pure: every transition returns a new value and time is a parameter.
/// </summary>
/// <remarks>
/// A rejection while blocked is <see cref="Absorb"/>, never <see cref="Escalate"/>, so concurrent rejections of one
/// client escalate once: the second one finds the client already blocked.
/// A client is forgiven after behaving for as long as it was last punished (BlockedUntil + LastPenalty)
/// </remarks>
public readonly record struct ClientStanding
{
    public static ClientStanding Clean => default;

    public int Streak { get; private init; }

    public DateTimeOffset BlockedUntil { get; private init; }

    public TimeSpan LastPenalty { get; private init; }

    /// <summary>
    /// Requests rejected while blocked since the last episode; reported on the next episode
    /// </summary>
    public int AbsorbedSinceLastEpisode { get; private init; }

    public bool IsBlockedAt(DateTimeOffset now) => now < BlockedUntil;

    public bool IsForgivenAt(DateTimeOffset now) => Streak == 0 || now >= BlockedUntil + LastPenalty;

    public TimeSpan RemainingAt(DateTimeOffset now) => IsBlockedAt(now) ? BlockedUntil - now : TimeSpan.Zero;

    public ClientStanding Absorb() => this with { AbsorbedSinceLastEpisode = AbsorbedSinceLastEpisode + 1 };

    /// <summary>
    /// An admitted request. Drops to Clean once forgiven so the partition can go idle and be evicted
    /// </summary>
    public ClientStanding SettleAt(DateTimeOffset now) => IsForgivenAt(now) ? Clean : this;

    /// <summary>
    /// The window rejected a request from a client that was not blocked
    /// </summary>
    public (ClientStanding Next, PenaltyEpisode Episode) Escalate(DateTimeOffset now, TimeSpan windowRetryAfter,
        PenaltySchedule schedule, TrafficClass traffic)
    {
        Debug.Assert(!IsBlockedAt(now));

        var streak = IsForgivenAt(now) ? 1 : Streak + 1;
        var penalty = schedule.PenaltyFor(streak, windowRetryAfter);
        var next = new ClientStanding { Streak = streak, BlockedUntil = now + penalty, LastPenalty = penalty };

        return (next, new PenaltyEpisode(traffic, streak, penalty, AbsorbedSinceLastEpisode));
    }
}

/// <summary>
/// Emitted exactly once per escalation. Carries no IP or URL: the logger records those under the store's privacy setting
/// </summary>
public sealed record PenaltyEpisode(TrafficClass Traffic, int Streak, TimeSpan Penalty, int RejectedDuringPreviousPenalty);
