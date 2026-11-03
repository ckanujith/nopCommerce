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

        //503 was the previous default; 429 is what tells a client that it, not the server, must back off
        var commonConfig = Singleton<AppSettings>.Instance.Get<CommonConfig>();
        if (commonConfig.RejectionStatusCode != 503)
            return;

        commonConfig.RejectionStatusCode = 429;

        AppSettingsHelper.SaveAppSettings(new List<IConfig> { commonConfig }, EngineContext.Current.Resolve<INopFileProvider>());
    }

    public override void Down() { }
}
