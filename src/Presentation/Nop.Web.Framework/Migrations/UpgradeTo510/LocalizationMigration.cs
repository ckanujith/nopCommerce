using FluentMigrator;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Web.Framework.Extensions;

namespace Nop.Web.Framework.Migrations.UpgradeTo510;

[NopUpdateMigration("2026-10-28 00:00:00", "5.10", UpdateMigrationType.Localization)]
public class LocalizationMigration : MigrationBase
{
    /// <summary>Collect the UP migration expressions</summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        #region Delete locales

        this.DeleteLocaleResources(new List<string>
        {
            
        });

        #endregion

        #region Rename locales

        this.RenameLocales(new Dictionary<string, string>
        {
            
        });

        #endregion

        #region Add or update locales

        this.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            ["Admin.Configuration.AppSettings.Common.MaxPenaltyMinutes"] = "Max penalty (minutes)",
            ["Admin.Configuration.AppSettings.Common.MaxPenaltyMinutes.Hint"] = "Upper bound for the exponential penalty applied to a client that keeps exceeding the permit limit in consecutive windows (1, 2, 4, 8 ... minutes). If set to 0, then there is no escalation. Admin area requests never escalate.",
            ["Admin.Configuration.AppSettings.Common.RejectionStatusCode.Hint"] = "Default status code to set on the response when a request is rejected. A Retry-After header is always sent.",
        });

        #endregion
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}