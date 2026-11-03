using System.Diagnostics;

namespace Nop.Web.Framework.RateLimiting;

public readonly record struct ClientStanding
{
    public static ClientStanding Clean => default;

    public int Streak { get; private init; }

    public DateTimeOffset BlockedUntil { get; private init; }

    public TimeSpan LastPenalty { get; private init; }

    public int AbsorbedSinceLastEpisode { get; private init; }

    public bool IsBlockedAt(DateTimeOffset now) => now < BlockedUntil;

    public bool IsForgivenAt(DateTimeOffset now) => Streak == 0 || now >= BlockedUntil + LastPenalty;

    public TimeSpan RemainingAt(DateTimeOffset now) => IsBlockedAt(now) ? BlockedUntil - now : TimeSpan.Zero;

    public ClientStanding Absorb() => this with { AbsorbedSinceLastEpisode = AbsorbedSinceLastEpisode + 1 };

    public ClientStanding SettleAt(DateTimeOffset now) => IsForgivenAt(now) ? Clean : this;

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

public sealed record PenaltyEpisode(TrafficClass Traffic, int Streak, TimeSpan Penalty, int RejectedDuringPreviousPenalty);
