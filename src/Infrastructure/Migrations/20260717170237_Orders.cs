using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsManager.Infrastructure.Migrations
{
    /// <summary>
    /// Tables app.Orders et app.OrderLines. Partie générée : structure, PK composite
    /// (OrderId, Label) des lignes, FK physiques, index (EventId, CreatedAt).
    /// Partie complétée à la main (réf. migration EventsAudit) — le modèle EF ne
    /// connaît pas ces colonnes et le snapshot reste volontairement inchangé :
    /// colonnes d'audit DB-only des deux tables, AddedBy/AddedDate remplies à
    /// l'insertion par contraintes DEFAULT, UpdatedBy/UpdatedDate par les triggers
    /// AFTER UPDATE déclarés côté EF via HasTrigger (cf. OrderConfiguration).
    /// Le Down généré suffit : DropTable emporte colonnes et triggers.
    /// </summary>
    public partial class Orders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "app",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "app",
                columns: table => new
                {
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => new { x.OrderId, x.Label });
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "app",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EventId_CreatedAt",
                schema: "app",
                table: "Orders",
                columns: new[] { "EventId", "CreatedAt" });

            AddAuditColumns(migrationBuilder, "Orders");
            AddAuditColumns(migrationBuilder, "OrderLines");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER app.TR_Orders_Update_Audit
                ON app.Orders
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    -- Ceinture-bretelles : si RECURSIVE_TRIGGERS était activé un jour,
                    -- l'UPDATE ci-dessous ne doit pas relancer ce trigger en boucle.
                    IF TRIGGER_NESTLEVEL(@@PROCID) > 1
                        RETURN;

                    UPDATE o
                    SET UpdatedBy = SUSER_SNAME(),
                        UpdatedDate = SYSUTCDATETIME()
                    FROM app.Orders AS o
                    INNER JOIN inserted AS i
                        ON i.Id = o.Id;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER app.TR_OrderLines_Update_Audit
                ON app.OrderLines
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    -- Ceinture-bretelles : si RECURSIVE_TRIGGERS était activé un jour,
                    -- l'UPDATE ci-dessous ne doit pas relancer ce trigger en boucle.
                    IF TRIGGER_NESTLEVEL(@@PROCID) > 1
                        RETURN;

                    -- Jointure sur la PK composite (OrderId, Label) de app.OrderLines.
                    UPDATE ol
                    SET UpdatedBy = SUSER_SNAME(),
                        UpdatedDate = SYSUTCDATETIME()
                    FROM app.OrderLines AS ol
                    INNER JOIN inserted AS i
                        ON i.OrderId = ol.OrderId
                       AND i.Label = ol.Label;
                END;
                """);
        }

        private static void AddAuditColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                schema: "app",
                table: table,
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValueSql: "SUSER_SNAME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddedDate",
                schema: "app",
                table: table,
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "app",
                table: table,
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                schema: "app",
                table: table,
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "app");
        }
    }
}
