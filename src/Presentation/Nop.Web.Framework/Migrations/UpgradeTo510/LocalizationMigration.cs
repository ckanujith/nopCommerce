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

            //NOPCOM-SDLC-20261104-001
            ["ShoppingCart.ClearCart"] = "Clear cart",
            ["ShoppingCart.ClearCart.Confirm"] = "Are you sure you want to remove all items from your cart?",
            ["Admin.Configuration.Settings.ShoppingCart.ShowClearCartButton"] = "Show \"Clear cart\" button",
            ["Admin.Configuration.Settings.ShoppingCart.ShowClearCartButton.Hint"] = "Check to show a button on the shopping cart page that removes all items from the cart.",
        });

        #endregion
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
