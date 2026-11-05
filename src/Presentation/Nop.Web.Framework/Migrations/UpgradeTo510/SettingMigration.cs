using FluentMigrator;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Web.Framework.Extensions;

namespace Nop.Web.Framework.Migrations.UpgradeTo510;

[NopUpdateMigration("2026-10-28 00:00:00", "5.10", UpdateMigrationType.Settings)]
public class SettingMigration : MigrationBase
{
    /// <summary>Collect the UP migration expressions</summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        //#2049
        this.SetSettingIfNotExists<ProductEditorSettings, bool>(settings => settings.DisableRewardPointsRedemption, false);

        //NOPCOM-SDLC-20261104-001
        // Databases that already applied this 5.10 settings migration do not re-run it.
        // ShowClearCartButton stays at the C# default (false) on those databases until an admin enables it.
        this.SetSettingIfNotExists<ShoppingCartSettings, bool>(settings => settings.ShowClearCartButton, true);
    }

    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
