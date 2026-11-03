using Nop.Web.Framework.Models;

namespace Nop.Web.Areas.Admin.Models.Home;

/// <summary>
/// Represents a rate limit activity model
/// </summary>
public partial record RateLimitActivityModel : BaseNopModel
{
    #region Properties

    public int EpisodeCount { get; set; }

    public DateTime? LatestOn { get; set; }

    public string LatestShortMessage { get; set; }

    public string LogFilter { get; set; }

    #endregion
}
