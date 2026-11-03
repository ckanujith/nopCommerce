using FluentMigrator;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Data.Migrations;

namespace Nop.Web.Framework.Migrations.UpgradeTo510;

[NopMigration("2026-11-03 00:00:00", "Pseudo-migration to update appSettings.json file", MigrationProcessType.Update)]
public class AppSettingsMigration : MigrationBase
{
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        var commonConfig = Singleton<AppSettings>.Instance.Get<CommonConfig>();
        if (commonConfig.RejectionStatusCode != 503)
            return;

        commonConfig.RejectionStatusCode = 429;

        AppSettingsHelper.SaveAppSettings(new List<IConfig> { commonConfig }, EngineContext.Current.Resolve<INopFileProvider>());
    }

    public override void Down() { }
}
