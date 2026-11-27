using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NativeOrchestrator.Data.SqlServer.Migrations;

/// <inheritdoc />
public partial class _20261127132727_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "saga");

        migrationBuilder.CreateTable(
            name: "Sagas",
            schema: "saga",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SagaType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ActiveInstanceKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CurrentStepIndex = table.Column<int>(type: "int", nullable: false),
                DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                NextRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CancellationRequested = table.Column<bool>(type: "bit", nullable: false),
                FailureKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                FailureStep = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Version = table.Column<int>(type: "int", nullable: false),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LeaseOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sagas", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SagaHistory",
            schema: "saga",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SagaType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                StepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Details = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SagaHistory", x => x.Id);
                table.ForeignKey(
                    name: "FK_SagaHistory_Sagas_SagaId",
                    column: x => x.SagaId,
                    principalSchema: "saga",
                    principalTable: "Sagas",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SagaSteps",
            schema: "saga",
            columns: table => new
            {
                StepIndex = table.Column<int>(type: "int", nullable: false),
                SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Attempt = table.Column<int>(type: "int", nullable: false),
                CheckCount = table.Column<int>(type: "int", nullable: false),
                CompensationAttempt = table.Column<int>(type: "int", nullable: false),
                StateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SagaSteps", x => new { x.SagaId, x.StepIndex });
                table.ForeignKey(
                    name: "FK_SagaSteps_Sagas_SagaId",
                    column: x => x.SagaId,
                    principalSchema: "saga",
                    principalTable: "Sagas",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SagaHistory_SagaId",
            schema: "saga",
            table: "SagaHistory",
            column: "SagaId");

        migrationBuilder.CreateIndex(
            name: "IX_Sagas_ActiveInstanceKey",
            schema: "saga",
            table: "Sagas",
            column: "ActiveInstanceKey",
            unique: true,
            filter: "[ActiveInstanceKey] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Sagas_SagaType_CorrelationId",
            schema: "saga",
            table: "Sagas",
            columns: new[] { "SagaType", "CorrelationId" },
            unique: true,
            filter: "[CorrelationId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Sagas_Status_NextRunAtUtc",
            schema: "saga",
            table: "Sagas",
            columns: new[] { "Status", "NextRunAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SagaHistory",
            schema: "saga");

        migrationBuilder.DropTable(
            name: "SagaSteps",
            schema: "saga");

        migrationBuilder.DropTable(
            name: "Sagas",
            schema: "saga");
    }
}
