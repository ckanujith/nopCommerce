using FluentMigrator;
using Nop.Core.Domain.Catalog;
using Nop.Data.Extensions;

namespace Nop.Data.Migrations.UpgradeTo510;

[NopSchemaMigration("2026-11-04 00:00:01", "SchemaMigration for 5.10.0")]
public class SchemaMigration : ForwardOnlyMigration
{
    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        //#2049
        this.AddOrAlterColumnFor<Product>(t => t.DisableRewardPointsRedemption)
            .AsBoolean()
            .NotNullable()
            .WithDefaultValue(false);
    }
}
