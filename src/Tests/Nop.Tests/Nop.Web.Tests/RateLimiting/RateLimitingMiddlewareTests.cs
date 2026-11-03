using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Nop.Core.Configuration;
using Nop.Core.Domain.Logging;
using Nop.Services.Logging;
using Nop.Web.Controllers;
using Nop.Web.Framework.RateLimiting;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.RateLimiting;

[TestFixture]
public class RateLimitingMiddlewareTests : BaseNopTest
{
    private FakeTimeProvider _time;
    private ServiceProvider _services;
    private RequestDelegate _pipeline;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(GetService<ILogger>());
        services.AddRateLimiter(options => InboundRateLimiter.Configure(options, new CommonConfig { PermitLimit = 2 }, _time));
        _services = services.BuildServiceProvider();

        var app = new ApplicationBuilder(_services);
        app.UseRateLimiter();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        _pipeline = app.Build();
    }

    [TearDown]
    public void TearDown()
    {
        _services.Dispose();
    }

    private async Task<HttpResponse> SendAsync(Endpoint endpoint = null)
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        context.Response.Body = new MemoryStream();
        if (endpoint != null)
            context.SetEndpoint(endpoint);

        await _pipeline(context);

        return context.Response;
    }

    private static string ReadBody(HttpResponse response)
    {
        response.Body.Position = 0;

        return new StreamReader(response.Body).ReadToEnd();
    }

    [Test]
    public async Task FloodedClientGets429WithGrowingRetryAfterAndOneLogRowPerEscalation()
    {
        var since = DateTime.UtcNow.AddSeconds(-1);
        (await SendAsync()).StatusCode.Should().Be(StatusCodes.Status200OK);
        (await SendAsync()).StatusCode.Should().Be(StatusCodes.Status200OK);

        var retryAfters = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var rejected = await SendAsync();
            rejected.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            retryAfters.Add(rejected.Headers.RetryAfter);
            ReadBody(rejected).Should().Be($"Too many requests. Retry after {rejected.Headers.RetryAfter} seconds.");

            var whileBlocked = await SendAsync();
            whileBlocked.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            whileBlocked.Headers.RetryAfter.ToString().Should().Be(rejected.Headers.RetryAfter);

            _time.Advance(TimeSpan.FromSeconds(int.Parse(rejected.Headers.RetryAfter)));
        }

        retryAfters.Should().Equal("60", "120", "240");

        var logger = GetService<ILogger>();
        var activity = await RateLimitLog.ReadActivityAsync(logger, RateLimitLogQuery.Dashboard(DateTime.UtcNow));
        activity.EpisodeCount.Should().Be(3);
        activity.LatestShortMessage.Should().StartWith(RateLimitLog.MessageMarker);

        var rows = await logger.GetAllLogsAsync(fromUtc: since, message: RateLimitLog.MessageMarker);
        rows.Should().OnlyContain(row => row.LogLevel == LogLevel.Warning && row.CustomerId == null);
        rows.Select(row => row.ShortMessage).Should().BeEquivalentTo(
            "Rate limit: Storefront client penalized: violation #1, blocked for 60 s.",
            "Rate limit: Storefront client penalized: violation #2, blocked for 120 s.",
            "Rate limit: Storefront client penalized: violation #3, blocked for 240 s.");
    }

    [Test]
    public async Task ScheduleTaskRunTaskIsNeverLimited()
    {
        var runTask = typeof(ScheduleTaskController).GetMethod(nameof(ScheduleTaskController.RunTask));
        var endpoint = new Endpoint(null, new EndpointMetadataCollection(runTask.GetCustomAttributes(true)), "RunTask");

        for (var i = 0; i < 5; i++)
            (await SendAsync(endpoint)).StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
