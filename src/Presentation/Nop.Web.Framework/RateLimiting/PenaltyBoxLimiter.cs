using System.Threading.RateLimiting;

namespace Nop.Web.Framework.RateLimiting;

/// <remarks>
/// The partitioned limiter's heartbeat calls TryReplenish on replenishing partitions, which refills the window without a
/// timer per client. It also disposes partitions idle for longer than its idle limit, so IdleDuration stays null until the
/// client is forgiven: eviction is then equivalent to resetting the standing to Clean
/// </remarks>
internal sealed class PenaltyBoxLimiter : ReplenishingRateLimiter
{
    private readonly FixedWindowRateLimiter _window;
    private readonly PenaltySchedule _schedule;
    private readonly TrafficClass _traffic;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private ClientStanding _standing;

    public PenaltyBoxLimiter(InboundRateLimitPolicy policy, TrafficClass traffic, TimeProvider time)
    {
        _window = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            QueueLimit = policy.QueueLimit,
            Window = policy.Window,
            AutoReplenishment = false,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
        _schedule = policy.ScheduleFor(traffic);
        _traffic = traffic;
        _time = time;
    }

    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (_gate)
                return _standing.IsForgivenAt(_time.GetUtcNow()) ? _window.IdleDuration : null;
        }
    }

    public override bool IsAutoReplenishing => false;

    public override TimeSpan ReplenishmentPeriod => _window.ReplenishmentPeriod;

    public override bool TryReplenish() => _window.TryReplenish();

    public override RateLimiterStatistics GetStatistics() => _window.GetStatistics();

    /// <remarks>
    /// RateLimitingMiddleware treats a failed AttemptAcquire as "now try AcquireAsync" and hands only that second lease to
    /// OnRejected, so a rejection here must leave the standing untouched
    /// </remarks>
    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_standing.IsBlockedAt(now))
                return PenaltyLease.Blocked(_standing.RemainingAt(now));

            var lease = _window.AttemptAcquire(permitCount);
            if (lease.IsAcquired)
                _standing = _standing.SettleAt(now);

            return lease;
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (_standing.IsBlockedAt(now))
                return Absorb(now);
        }

        var lease = await _window.AcquireAsync(permitCount, cancellationToken);

        lock (_gate)
            return Resolve(lease, _time.GetUtcNow());
    }

    private RateLimitLease Resolve(RateLimitLease windowLease, DateTimeOffset now)
    {
        if (windowLease.IsAcquired)
        {
            _standing = _standing.SettleAt(now);
            return windowLease;
        }

        var windowRetryAfter = windowLease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : _schedule.Window;
        windowLease.Dispose();

        if (_standing.IsBlockedAt(now))
            return Absorb(now);

        (_standing, var episode) = _standing.Escalate(now, windowRetryAfter, _schedule, _traffic);

        return PenaltyLease.Escalated(_standing.LastPenalty, episode);
    }

    private RateLimitLease Absorb(DateTimeOffset now)
    {
        _standing = _standing.Absorb();

        return PenaltyLease.Blocked(_standing.RemainingAt(now));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _window.Dispose();
    }

    protected override ValueTask DisposeAsyncCore() => _window.DisposeAsync();

    private sealed class PenaltyLease : RateLimitLease
    {
        private static readonly string[] _blockedMetadataNames = [MetadataName.RetryAfter.Name];
        private static readonly string[] _escalatedMetadataNames = [MetadataName.RetryAfter.Name, InboundRateLimiter.EpisodeMetadata.Name];

        private readonly TimeSpan _retryAfter;
        private readonly PenaltyEpisode _episode;

        private PenaltyLease(TimeSpan retryAfter, PenaltyEpisode episode)
        {
            _retryAfter = retryAfter;
            _episode = episode;
        }

        public static PenaltyLease Blocked(TimeSpan retryAfter) => new(retryAfter, null);

        public static PenaltyLease Escalated(TimeSpan retryAfter, PenaltyEpisode episode) => new(retryAfter, episode);

        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => _episode is null ? _blockedMetadataNames : _escalatedMetadataNames;

        public override bool TryGetMetadata(string metadataName, out object metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = _retryAfter;
                return true;
            }

            if (_episode is not null && metadataName == InboundRateLimiter.EpisodeMetadata.Name)
            {
                metadata = _episode;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
