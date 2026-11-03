using System.Net;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Nop.Core.Configuration;
using Nop.Web.Framework;
using Nop.Web.Framework.RateLimiting;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.RateLimiting;

[TestFixture]
public class InboundRateLimiterTests
{
    private FakeTimeProvider _time;
    private PartitionedRateLimiter<HttpContext> _limiter;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider();
        _limiter = InboundRateLimiter.CreateGlobalLimiter(PolicyWithPermitLimit(2), _time);
    }

    [TearDown]
    public void TearDown()
    {
        _limiter.Dispose();
    }

    private static InboundRateLimitPolicy PolicyWithPermitLimit(int permitLimit, TimeSpan? window = null)
    {
        return InboundRateLimitPolicy.FromConfig(new CommonConfig { PermitLimit = permitLimit, MaxPenaltyMinutes = 30 },
            window ?? InboundRateLimitPolicy.DefaultWindow);
    }

    private static HttpContext HttpContextFor(string ip, string area = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (area != null)
            context.Request.RouteValues["area"] = area;

        return context;
    }

    private static TimeSpan RetryAfter(RateLimitLease lease)
    {
        lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();

        return retryAfter;
    }

    private async Task ExhaustAsync(HttpContext context, int permits = 2)
    {
        for (var i = 0; i < permits; i++)
        {
            using var lease = await _limiter.AcquireAsync(context);
            lease.IsAcquired.Should().BeTrue();
        }
    }

    private async Task<List<(TimeSpan RetryAfter, PenaltyEpisode Episode)>> ViolateRepeatedlyAsync(HttpContext context, int violations)
    {
        var results = new List<(TimeSpan, PenaltyEpisode)>();
        for (var i = 0; i < violations; i++)
        {
            using var lease = await _limiter.AcquireAsync(context);
            lease.IsAcquired.Should().BeFalse();
            InboundRateLimiter.TryGetEpisode(lease, out var episode).Should().BeTrue();
            var retryAfter = RetryAfter(lease);
            results.Add((retryAfter, episode));
            _time.Advance(retryAfter);
        }

        return results;
    }

    [Test]
    public async Task FloodedClientGetsRetryAfterThatGrowsOnConsecutiveEscalations()
    {
        var client = HttpContextFor("198.51.100.7");
        await ExhaustAsync(client);

        var violations = await ViolateRepeatedlyAsync(client, 4);

        violations.Select(v => v.RetryAfter.TotalSeconds).Should().Equal(60, 120, 240, 480);
        violations.Select(v => v.Episode.Streak).Should().Equal(1, 2, 3, 4);
        violations.Should().OnlyContain(v => v.Episode.Traffic == TrafficClass.Storefront);
    }

    [Test]
    public async Task RequestsWhileBlockedGetTheRemainingTimeAndNeverEscalate()
    {
        var client = HttpContextFor("198.51.100.7");
        await ExhaustAsync(client);

        using (var first = await _limiter.AcquireAsync(client))
            InboundRateLimiter.TryGetEpisode(first, out _).Should().BeTrue();

        _time.Advance(TimeSpan.FromSeconds(20));
        for (var i = 0; i < 3; i++)
        {
            using var blocked = await _limiter.AcquireAsync(client);
            blocked.IsAcquired.Should().BeFalse();
            InboundRateLimiter.TryGetEpisode(blocked, out _).Should().BeFalse();
            RetryAfter(blocked).Should().Be(TimeSpan.FromSeconds(40));
        }

        _time.Advance(TimeSpan.FromSeconds(40));
        using var next = await _limiter.AcquireAsync(client);
        InboundRateLimiter.TryGetEpisode(next, out var episode).Should().BeTrue();
        episode.Streak.Should().Be(2);
        episode.RejectedDuringPreviousPenalty.Should().Be(3);
    }

    [Test]
    public async Task AttemptAcquireRejectionLeavesTheStandingUntouched()
    {
        var client = HttpContextFor("198.51.100.7");
        await ExhaustAsync(client);

        for (var i = 0; i < 3; i++)
        {
            using var attempt = _limiter.AttemptAcquire(client);
            attempt.IsAcquired.Should().BeFalse();
            InboundRateLimiter.TryGetEpisode(attempt, out _).Should().BeFalse();
        }

        using var lease = await _limiter.AcquireAsync(client);
        InboundRateLimiter.TryGetEpisode(lease, out var episode).Should().BeTrue();
        episode.Streak.Should().Be(1);
    }

    [Test]
    public async Task AdminTrafficHasItsOwnBucketAndNeverEscalates()
    {
        var storefront = HttpContextFor("198.51.100.7");
        var admin = HttpContextFor("198.51.100.7", AreaNames.ADMIN);
        await ExhaustAsync(storefront);

        await ExhaustAsync(admin);
        var violations = await ViolateRepeatedlyAsync(admin, 4);

        violations.Should().OnlyContain(v => v.RetryAfter == InboundRateLimitPolicy.DefaultWindow && v.Episode.Traffic == TrafficClass.Admin);
    }

    [Test]
    public async Task ClientIsTheRemoteAddressWithIpv6GroupedByPrefix()
    {
        await ExhaustAsync(HttpContextFor("198.51.100.7"));
        await ExhaustAsync(HttpContextFor("198.51.100.8"));

        await ExhaustAsync(HttpContextFor("2001:db8:1:2::1"), permits: 1);
        using (var samePrefix = await _limiter.AcquireAsync(HttpContextFor("2001:db8:1:2:ffff::9")))
            samePrefix.IsAcquired.Should().BeTrue();
        using (var overLimit = await _limiter.AcquireAsync(HttpContextFor("2001:db8:1:2:abcd::1")))
            overLimit.IsAcquired.Should().BeFalse();
        using (var otherPrefix = await _limiter.AcquireAsync(HttpContextFor("2001:db8:1:3::1")))
            otherPrefix.IsAcquired.Should().BeTrue();

        await ExhaustAsync(HttpContextFor("203.0.113.5"), permits: 1);
        using var mapped = await _limiter.AcquireAsync(HttpContextFor("::ffff:203.0.113.5"));
        mapped.IsAcquired.Should().BeTrue();
        using var mappedOverLimit = await _limiter.AcquireAsync(HttpContextFor("203.0.113.5"));
        mappedOverLimit.IsAcquired.Should().BeFalse();
    }

    [Test]
    public async Task ForgivenClientStartsOverAtTheFirstViolation()
    {
        var client = HttpContextFor("198.51.100.7");
        await ExhaustAsync(client);
        await ViolateRepeatedlyAsync(client, 2);

        _time.Advance(TimeSpan.FromMinutes(2));

        using var lease = await _limiter.AcquireAsync(client);
        InboundRateLimiter.TryGetEpisode(lease, out var episode).Should().BeTrue();
        episode.Streak.Should().Be(1);
        RetryAfter(lease).Should().Be(InboundRateLimitPolicy.DefaultWindow);
    }

    /// <summary>
    /// The partitioned limiter evicts partitions idle for more than 10 seconds of real time. A penalized client must survive that
    /// sweep, or eviction would silently reset its streak
    /// </summary>
    [Test]
    public async Task PenalizedPartitionSurvivesIdleEvictionAndForgivenPartitionIsEvicted()
    {
        _limiter.Dispose();
        _limiter = InboundRateLimiter.CreateGlobalLimiter(PolicyWithPermitLimit(1, TimeSpan.FromMilliseconds(100)), _time);
        var forgiven = HttpContextFor("198.51.100.1");
        var penalized = HttpContextFor("198.51.100.2");

        await ExhaustAsync(forgiven, permits: 1);
        (await _limiter.AcquireAsync(forgiven)).IsAcquired.Should().BeFalse();
        _time.Advance(TimeSpan.FromSeconds(1));
        await ExhaustAsync(penalized, permits: 1);
        (await _limiter.AcquireAsync(penalized)).IsAcquired.Should().BeFalse();

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_limiter.GetStatistics(forgiven).TotalSuccessfulLeases != 0)
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "a forgiven idle partition is evicted");
            await Task.Delay(250);
        }

        _limiter.GetStatistics(penalized).TotalSuccessfulLeases.Should().Be(1);
        using var lease = await _limiter.AcquireAsync(penalized);
        lease.IsAcquired.Should().BeFalse();
        InboundRateLimiter.TryGetEpisode(lease, out _).Should().BeFalse();
    }
}
