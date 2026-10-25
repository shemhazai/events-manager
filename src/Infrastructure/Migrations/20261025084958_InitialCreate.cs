using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventsManager.Infrastructure.Migrations
{
    /// <summary>
    /// Migration initiale PostgreSQL, et seule migration du dépôt. Les six migrations SQL Server
    /// qui la précédaient ont été retirées de l'arbre avec leur snapshot : elles ne se rejouent
    /// pas sur un autre provider, et le trail de décisions reste entier dans le git log.
    /// Partie générée : le schéma app, les trois tables, les colonnes générées STORED (Year,
    /// NameLower, LabelLower), les deux CHECK de la fenêtre de commande, les PK, les FK et les
    /// index — dont les deux index uniques qui portent l'insensibilité à la casse.
    /// Partie écrite à la main (réf. anciennes migrations EventsAudit et Orders) : les colonnes
    /// d'audit DB-only des trois tables et le trigger BEFORE UPDATE qui remplit Updated*. Le
    /// modèle EF ne connaît aucune de ces colonnes et le snapshot reste volontairement muet.
    /// Le Down généré suffit pour les tables, DropTable emportant colonnes et triggers. Seule la
    /// fonction partagée demande un retrait explicite.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderWindowStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrderWindowEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NameLower = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, computedColumnSql: "lower(\"Name\")", stored: true),
                    Year = table.Column<int>(type: "integer", nullable: false, computedColumnSql: "EXTRACT(YEAR FROM \"Date\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_OrderWindow_BothOrNeither", "(\"OrderWindowStartDate\" IS NULL AND \"OrderWindowEndDate\" IS NULL) OR (\"OrderWindowStartDate\" IS NOT NULL AND \"OrderWindowEndDate\" IS NOT NULL)");
                    table.CheckConstraint("CK_Events_OrderWindow_Order", "\"OrderWindowStartDate\" IS NULL OR (\"OrderWindowStartDate\" <= \"OrderWindowEndDate\" AND \"OrderWindowEndDate\" <= \"Date\")");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LabelLower = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, computedColumnSql: "lower(\"Label\")", stored: true)
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
                name: "IX_Events_Date",
                schema: "app",
                table: "Events",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Events_NameLower_Year",
                schema: "app",
                table: "Events",
                columns: new[] { "NameLower", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId_LabelLower",
                schema: "app",
                table: "OrderLines",
                columns: new[] { "OrderId", "LabelLower" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EventId_CreatedAt",
                schema: "app",
                table: "Orders",
                columns: new[] { "EventId", "CreatedAt" });

            AddAuditColumns(migrationBuilder, "Events");
            AddAuditColumns(migrationBuilder, "Orders");
            AddAuditColumns(migrationBuilder, "OrderLines");

            // UNE seule fonction pour les trois tables, là où le T-SQL exigeait trois corps
            // distincts dont un joignant la PK composite d'OrderLines : celle-ci n'écrit que deux
            // colonnes, qui portent le même nom partout, elle est donc agnostique de la table.
            // BEFORE UPDATE et non AFTER : écrire NEW directement supprime l'UPDATE récursif du
            // T-SQL et son garde de niveau d'imbrication (TRIGGER_NESTLEVEL). C'est une
            // simplification réelle, pas une traduction littérale.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION app.set_audit_columns() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    NEW."UpdatedBy"   := current_user;
                    NEW."UpdatedDate" := now() AT TIME ZONE 'utc';
                    RETURN NEW;
                END;
                $$
                """);

            // Un appel Sql() par instruction, et non un lot unique : le corps de la fonction
            // ci-dessus est en guillemets-dollar, et découper évite de faire reposer la migration
            // sur la façon dont Npgsql scinde un lot multi-instructions.
            CreateAuditTrigger(migrationBuilder, "Events");
            CreateAuditTrigger(migrationBuilder, "Orders");
            CreateAuditTrigger(migrationBuilder, "OrderLines");
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

            migrationBuilder.DropTable(
                name: "Events",
                schema: "app");

            // DropTable a emporté les trois triggers avec leurs tables ; la fonction, elle, ne
            // dépend d'aucune table et survivrait.
            migrationBuilder.Sql("DROP FUNCTION app.set_audit_columns()");
        }

        /// <summary>
        /// Colonnes d'audit DB-only d'une table : Added* remplies à l'insertion par contraintes
        /// DEFAULT, Updated* laissées nulles et remplies par le trigger. Invisibles d'EF, pas
        /// même en shadow property. « timestamp without time zone » est le report fidèle du type
        /// temporel de l'ancienne base : les valeurs sont UTC par convention, et c'est la base
        /// qui les écrit.
        /// </summary>
        private static void AddAuditColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                schema: "app",
                table: table,
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValueSql: "current_user");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddedDate",
                schema: "app",
                table: table,
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now() AT TIME ZONE 'utc'");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "app",
                table: table,
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                schema: "app",
                table: table,
                type: "timestamp without time zone",
                nullable: true);
        }

        private static void CreateAuditTrigger(MigrationBuilder migrationBuilder, string table)
        {
            // Identifiants quotés : PostgreSQL replierait en minuscules les noms non quotés, et
            // les noms de triggers sont cités tels quels par les tests d'audit.
            migrationBuilder.Sql(
                $"""
                 CREATE TRIGGER "TR_{table}_Update_Audit"
                 BEFORE UPDATE ON app."{table}"
                 FOR EACH ROW EXECUTE FUNCTION app.set_audit_columns()
                 """);
        }
    }
}
