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
            //#2049
            ["Admin.Catalog.Products.Fields.DisableRewardPointsRedemption"] = "Disable purchasing with reward points",
            ["Admin.Catalog.Products.Fields.DisableRewardPointsRedemption.Hint"] = "Check to prevent customers from redeeming reward points on orders that include this product.",
            ["Admin.Configuration.Settings.ProductEditor.DisableRewardPointsRedemption"] = "Disable purchasing with reward points",

            //#8296
            ["Admin.Promotions.Discounts.Imported"] = "Discounts have been imported successfully.",
            ["Admin.Promotions.Discounts.List.ImportFromExcelTip"] = "Imported discounts are distinguished by ID. If the ID already exists, then its corresponding discount will be updated. You should not specify ID (leave 0) for new discounts. Duplicate names and coupon codes are allowed.",
            ["ActivityLog.ExportDiscounts"] = "{0} discounts were exported",
            ["ActivityLog.ImportDiscounts"] = "{0} discounts were imported",
        });

        #endregion
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
