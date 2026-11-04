using FluentMigrator;
using Nop.Core.Domain.Logging;
using Nop.Data;

namespace Nop.Data.Migrations.UpgradeTo510;

[NopUpdateMigration("2026-10-28 00:00:00", "5.10", UpdateMigrationType.Data)]
public class DataMigration : Migration
{
    private readonly INopDataProvider _dataProvider;

    public DataMigration(INopDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        var activityLogTypeTable = _dataProvider.GetTable<ActivityLogType>();

        //#8296
        if (!activityLogTypeTable.Any(alt => string.Compare(alt.SystemKeyword, "ExportDiscounts", StringComparison.InvariantCultureIgnoreCase) == 0))
        {
            _dataProvider.InsertEntity(
                new ActivityLogType
                {
                    SystemKeyword = "ExportDiscounts",
                    Enabled = true,
                    Name = "Discounts were exported"
                }
            );
        }

        if (!activityLogTypeTable.Any(alt => string.Compare(alt.SystemKeyword, "ImportDiscounts", StringComparison.InvariantCultureIgnoreCase) == 0))
        {
            _dataProvider.InsertEntity(
                new ActivityLogType
                {
                    SystemKeyword = "ImportDiscounts",
                    Enabled = true,
                    Name = "Discounts were imported"
                }
            );
        }
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
