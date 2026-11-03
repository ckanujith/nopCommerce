using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Data;
using Nop.Services.Logging;

namespace Nop.Web.Framework.RateLimiting;

/// <summary>
/// The one way to turn on inbound rate limiting: registration, pipeline gating, partitioning, the rejection response
/// and System Log reporting
/// </summary>
public static class InboundRateLimiter
{
    internal static readonly MetadataName<PenaltyEpisode> EpisodeMetadata = new("nop.rate-limit.episode");

    /// <summary>
    /// Global cap on System Log rows written by this feature, to protect the database during distributed floods
    /// </summary>
    private const int LOG_BUDGET_PER_MINUTE = 60;
    private const int LOG_BUDGET_BURST = 20;

    public static bool IsEnabled(CommonConfig config) => InboundRateLimitPolicy.FromConfig(config) is not null;

    /// <summary>
    /// Configure the rate limiter options: global limiter, rejection status code and rejection handler
    /// </summary>
    /// <param name="options">Rate limiter options</param>
    /// <param name="config">Common config</param>
    /// <param name="time">Clock used for penalties; null to use the system clock</param>
    public static void Configure(RateLimiterOptions options, CommonConfig config, TimeProvider time = null)
    {
        var policy = InboundRateLimitPolicy.FromConfig(config)
            ?? throw new InvalidOperationException("Inbound rate limiting is disabled: the permit limit must be greater than 0");
        var budget = new EpisodeLogBudget(LOG_BUDGET_PER_MINUTE, LOG_BUDGET_BURST);

        options.GlobalLimiter = CreateGlobalLimiter(policy, time ?? TimeProvider.System);
        options.RejectionStatusCode = policy.RejectionStatusCode;
        options.OnRejected = (context, cancellationToken) => OnRejectedAsync(context, budget, cancellationToken);
    }

    public static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(InboundRateLimitPolicy policy, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(time);

        return PartitionedRateLimiter.Create<HttpContext, ClientPartition>(context =>
            RateLimitPartition.Get(ClientPartition.From(context), partition => new PenaltyBoxLimiter(policy, partition.Traffic, time)));
    }

    /// <summary>
    /// True only on the single rejected lease that escalated a client
    /// </summary>
    public static bool TryGetEpisode(RateLimitLease lease, out PenaltyEpisode episode) => lease.TryGetMetadata(EpisodeMetadata, out episode);

    /// <remarks>
    /// Never resolves IWorkContext: getting the current customer would create a guest customer for every rejected bot
    /// </remarks>
    private static async ValueTask OnRejectedAsync(OnRejectedContext context, EpisodeLogBudget budget, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : InboundRateLimitPolicy.DefaultWindow;
        var seconds = Math.Max(1, (long)Math.Ceiling(retryAfter.TotalSeconds));

        var response = context.HttpContext.Response;
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        response.ContentType = "text/plain; charset=utf-8";

        if (TryGetEpisode(context.Lease, out var episode) && DataSettingsManager.IsDatabaseInstalled() && budget.TryTake(out var suppressed))
        {
            try
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger>();
                await RateLimitLog.WriteEpisodeAsync(logger, episode, suppressed);
            }
            catch
            {
                //visibility is best-effort; a logging failure must not turn the rejection into a server error
            }
        }

        await response.WriteAsync($"Too many requests. Retry after {seconds} seconds.", cancellationToken);
    }

    private static string ClientKey(IPAddress address)
    {
        if (address is null)
            return "unknown";

        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4().ToString();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        //one IPv6 subscriber usually owns a whole /64, so rotating addresses inside it must share a bucket
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);

        return $"{new IPAddress(bytes)}/64";
    }

    /// <summary>
    /// Partition key built from server-trusted inputs only: the remote IP after forwarded headers are applied, and the matched route
    /// </summary>
    private readonly record struct ClientPartition(TrafficClass Traffic, string Client)
    {
        /// <summary>
        /// Storefront when the matched route has no admin area. API plugins and public controllers land here and use the exponential schedule. Admin area requests use the flat window.
        /// </summary>
        public static ClientPartition From(HttpContext context)
        {
            var traffic = string.Equals(context.Request.RouteValues["area"] as string, AreaNames.ADMIN, StringComparison.OrdinalIgnoreCase)
                ? TrafficClass.Admin
                : TrafficClass.Storefront;

            return new ClientPartition(traffic, ClientKey(context.Connection.RemoteIpAddress));
        }
    }

    /// <summary>
    /// Log write budget shared by all partitions. Suppressed episodes are counted into the next written row, not lost
    /// </summary>
    private sealed class EpisodeLogBudget
    {
        private readonly TokenBucketRateLimiter _bucket;
        private int _suppressed;

        public EpisodeLogBudget(int perMinute, int burst)
        {
            _bucket = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = burst,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1) / perMinute,
                AutoReplenishment = true,
                QueueLimit = 0
            });
        }

        public bool TryTake(out int suppressedSinceLastWrite)
        {
            using var lease = _bucket.AttemptAcquire();
            if (!lease.IsAcquired)
            {
                Interlocked.Increment(ref _suppressed);
                suppressedSinceLastWrite = 0;

                return false;
            }

            suppressedSinceLastWrite = Interlocked.Exchange(ref _suppressed, 0);

            return true;
        }
    }
}
