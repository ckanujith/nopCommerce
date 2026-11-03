using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Infrastructure.Cache;
using Nop.Web.Areas.Admin.Models.Home;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.RateLimiting;
using ILogger = Nop.Services.Logging.ILogger;

namespace Nop.Web.Areas.Admin.Components;

public partial class RateLimitActivityViewComponent : NopViewComponent
{
    protected readonly AppSettings _appSettings;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILogger _logger;
    protected readonly IPermissionService _permissionService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IWorkContext _workContext;

    public RateLimitActivityViewComponent(AppSettings appSettings,
        IDateTimeHelper dateTimeHelper,
        ILogger logger,
        IPermissionService permissionService,
        IStaticCacheManager staticCacheManager,
        IWorkContext workContext)
    {
        _appSettings = appSettings;
        _dateTimeHelper = dateTimeHelper;
        _logger = logger;
        _permissionService = permissionService;
        _staticCacheManager = staticCacheManager;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!InboundRateLimiter.IsEnabled(_appSettings.Get<CommonConfig>()))
            return Content(string.Empty);

        if (!await _permissionService.AuthorizeAsync(StandardPermission.System.MANAGE_SYSTEM_LOG))
            return Content(string.Empty);

        if (await _workContext.GetCurrentVendorAsync() != null)
            return Content(string.Empty);

        var activity = await _staticCacheManager.GetAsync(NopModelCacheDefaults.RateLimitActivityKey,
            () => RateLimitLog.ReadActivityAsync(_logger, RateLimitLogQuery.Dashboard(DateTime.UtcNow)));

        var zone = await _dateTimeHelper.GetCurrentTimeZoneAsync();
        var model = new RateLimitActivityModel
        {
            EpisodeCount = activity.EpisodeCount,
            LatestOn = activity.LatestOnUtc is DateTime latest
                ? await _dateTimeHelper.ConvertToUserTimeAsync(latest, DateTimeKind.Utc)
                : null,
            LatestShortMessage = activity.LatestShortMessage,
            LogLink = RateLimitLogLink.From(activity.Query, zone)
        };

        return await ViewAsync(model);
    }
}
