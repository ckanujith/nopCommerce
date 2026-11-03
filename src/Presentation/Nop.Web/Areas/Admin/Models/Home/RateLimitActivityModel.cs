using Nop.Web.Framework.Models;

namespace Nop.Web.Areas.Admin.Models.Home;

public partial record RateLimitActivityModel : BaseNopModel
{
    public int EpisodeCount { get; set; }

    public DateTime? LatestOn { get; set; }

    public string LatestShortMessage { get; set; }

    public RateLimitLogLink LogLink { get; set; }
}
