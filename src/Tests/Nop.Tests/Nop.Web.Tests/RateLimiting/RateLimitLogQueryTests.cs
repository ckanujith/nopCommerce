using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Nop.Core.Configuration;
using Nop.Core.Domain.Logging;
using Nop.Web.Areas.Admin.Models.Home;
using Nop.Web.Areas.Admin.Models.Logging;
using Nop.Web.Framework.RateLimiting;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.RateLimiting;

[TestFixture]
public class RateLimitLogQueryTests
{
    private static readonly DateTime _utc = new(2026, 11, 3, 10, 20, 0, DateTimeKind.Utc);

    [Test]
    public void DashboardRejectsAClockThatIsNotUtc()
    {
        var local = () => RateLimitLogQuery.Dashboard(new DateTime(2026, 11, 3, 10, 20, 0, DateTimeKind.Local));
        var unspecified = () => RateLimitLogQuery.Dashboard(new DateTime(2026, 11, 3, 10, 20, 0, DateTimeKind.Unspecified));

        local.Should().Throw<ArgumentException>().WithParameterName("utcNow");
        unspecified.Should().Throw<ArgumentException>().WithParameterName("utcNow");
    }

    [Test]
    public void LinkUsesTheUtcCalendarDateOfTheWindowStart()
    {
        var query = RateLimitLogQuery.Dashboard(_utc);
        query.SinceUtc.Kind.Should().Be(DateTimeKind.Utc);
        query.Level.Should().Be(RateLimitLog.Level);

        var link = RateLimitLogLink.From(query, TimeZoneInfo.Utc);

        link.Message.Should().Be(RateLimitLog.MessageMarker);
        link.LogLevelId.Should().Be((int)LogLevel.Warning);
        link.CreatedOnFrom.Should().Be(new DateTime(2026, 11, 2));

        var route = link.RouteValues();
        route.Keys.Should().BeEquivalentTo(
            nameof(LogSearchModel.Message),
            nameof(LogSearchModel.LogLevelId),
            nameof(LogSearchModel.CreatedOnFrom));
        route[nameof(LogSearchModel.CreatedOnFrom)].Should().Be("2026-11-02");
    }

    [Test]
    public void LinkUsesTheLocalDateWhenTheZoneIsEastOfUtc()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("east", TimeSpan.FromHours(14), "east", "east");
        var link = RateLimitLogLink.From(RateLimitLogQuery.Dashboard(_utc), zone);

        link.CreatedOnFrom.Should().Be(new DateTime(2026, 11, 3));
    }

    [Test]
    public void ShippedRejectionStatusCodeMatchesTheConfigDefault()
    {
        var configDefault = new CommonConfig().RejectionStatusCode;
        configDefault.Should().Be(StatusCodes.Status429TooManyRequests);

        var path = Path.Combine(RepoRoot(), "src", "Presentation", "Nop.Web", "App_Data", "appsettings.json");
        if (!File.Exists(path))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var code = doc.RootElement.GetProperty("CommonConfig").GetProperty("RejectionStatusCode").GetInt32();

        code.Should().Be(configDefault);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "NopCommerce.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate src/NopCommerce.sln.");
    }
}
