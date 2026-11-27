using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NativeOrchestrator.Data.SqlServer.Migrations;

/// <inheritdoc />
public partial class _20261127010746_SingleInstanceSagas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActiveInstanceKey",
            schema: "saga",
            table: "Sagas",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Sagas_ActiveInstanceKey",
            schema: "saga",
            table: "Sagas",
            column: "ActiveInstanceKey",
            unique: true,
            filter: "[ActiveInstanceKey] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Sagas_ActiveInstanceKey",
            schema: "saga",
            table: "Sagas");

        migrationBuilder.DropColumn(
            name: "ActiveInstanceKey",
            schema: "saga",
            table: "Sagas");
    }
}
