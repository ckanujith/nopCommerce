using AwesomeAssertions;
using Nop.Web.Framework.RateLimiting;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.RateLimiting;

[TestFixture]
public class ClientStandingTests
{
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    private static readonly PenaltySchedule _schedule = new(_window, TimeSpan.FromMinutes(30));
    private static readonly DateTimeOffset _t0 = DateTimeOffset.Parse("2026-11-03T09:00:00Z");

    private static (ClientStanding Next, PenaltyEpisode Episode) Escalate(ClientStanding standing, DateTimeOffset now, PenaltySchedule schedule = null)
    {
        return standing.Escalate(now, _window, schedule ?? _schedule, TrafficClass.Storefront);
    }

    [Test]
    public void ConsecutiveViolationsDoubleThePenaltyUpToTheCeiling()
    {
        var standing = ClientStanding.Clean;
        var now = _t0;
        var penalties = new List<double>();

        for (var i = 0; i < 7; i++)
        {
            (standing, var episode) = Escalate(standing, now);
            episode.Streak.Should().Be(i + 1);
            penalties.Add(episode.Penalty.TotalMinutes);
            now = standing.BlockedUntil;
        }

        penalties.Should().Equal(1, 2, 4, 8, 16, 30, 30);
    }

    [Test]
    public void PenaltyNeverOverflowsForHugeStreaks()
    {
        _schedule.PenaltyFor(int.MaxValue, _window).Should().Be(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void PenaltyIsNeverShorterThanWhatTheWindowReported()
    {
        _schedule.PenaltyFor(1, TimeSpan.FromMinutes(3)).Should().Be(TimeSpan.FromMinutes(3));
    }

    [Test]
    public void NoEscalationScheduleAlwaysReturnsTheWindowRetryAfter()
    {
        var none = PenaltySchedule.None(_window);
        var standing = ClientStanding.Clean;
        var now = _t0;

        for (var i = 0; i < 5; i++)
        {
            (standing, var episode) = standing.Escalate(now, _window, none, TrafficClass.Admin);
            episode.Penalty.Should().Be(_window);
            now = standing.BlockedUntil;
        }
    }

    [Test]
    public void RejectionsWhileBlockedAreAbsorbedAndReportedOnTheNextEpisode()
    {
        var (blocked, _) = Escalate(ClientStanding.Clean, _t0);
        blocked.IsBlockedAt(_t0.AddSeconds(30)).Should().BeTrue();

        var absorbed = blocked.Absorb().Absorb();
        absorbed.Streak.Should().Be(1);
        absorbed.BlockedUntil.Should().Be(blocked.BlockedUntil);

        var (next, episode) = Escalate(absorbed, _t0.AddSeconds(90));
        episode.Streak.Should().Be(2);
        episode.RejectedDuringPreviousPenalty.Should().Be(2);
        next.AbsorbedSinceLastEpisode.Should().Be(0);
    }

    [Test]
    public void ClientIsForgivenAfterBehavingForAsLongAsItWasPunished()
    {
        var (standing, _) = Escalate(ClientStanding.Clean, _t0);
        var forgivenAt = standing.BlockedUntil + standing.LastPenalty;

        standing.IsForgivenAt(forgivenAt - TimeSpan.FromTicks(1)).Should().BeFalse();
        standing.IsForgivenAt(forgivenAt).Should().BeTrue();

        Escalate(standing, forgivenAt - TimeSpan.FromTicks(1)).Episode.Streak.Should().Be(2);
        Escalate(standing, forgivenAt).Episode.Streak.Should().Be(1);
    }

    [Test]
    public void SettleDropsToCleanOnlyOnceForgiven()
    {
        var (standing, _) = Escalate(ClientStanding.Clean, _t0);

        standing.SettleAt(standing.BlockedUntil).Should().Be(standing);
        standing.SettleAt(standing.BlockedUntil + standing.LastPenalty).Should().Be(ClientStanding.Clean);
    }

    [Test]
    public void ConfigIsParsedIntoPolicyAtTheBoundary()
    {
        InboundRateLimitPolicy.FromConfig(new() { PermitLimit = 0 }).Should().BeNull();

        var policy = InboundRateLimitPolicy.FromConfig(new() { PermitLimit = 5, QueueCount = -1, RejectionStatusCode = 200, MaxPenaltyMinutes = 0 });
        policy.QueueLimit.Should().Be(0);
        policy.RejectionStatusCode.Should().Be(429);
        policy.Storefront.Ceiling.Should().Be(TimeSpan.Zero);
        policy.ScheduleFor(TrafficClass.Admin).Ceiling.Should().Be(TimeSpan.Zero);
    }
}
