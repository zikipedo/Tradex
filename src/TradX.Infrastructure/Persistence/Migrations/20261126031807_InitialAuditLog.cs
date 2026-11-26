using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_resource_type_resource_id",
                table: "audit_logs",
                columns: new[] { "resource_type", "resource_id" });

            // Journal d'audit append-only : la base elle-même refuse UPDATE, DELETE et TRUNCATE,
            // même si un bug ou un attaquant passait par l'application.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_logs_block_changes() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_logs est en ajout seul (append-only)';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_audit_logs_no_update_delete
                    BEFORE UPDATE OR DELETE ON audit_logs
                    FOR EACH ROW EXECUTE FUNCTION audit_logs_block_changes();

                CREATE TRIGGER trg_audit_logs_no_truncate
                    BEFORE TRUNCATE ON audit_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_block_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_logs_no_truncate ON audit_logs;
                DROP TRIGGER IF EXISTS trg_audit_logs_no_update_delete ON audit_logs;
                DROP FUNCTION IF EXISTS audit_logs_block_changes();
                """);

            migrationBuilder.DropTable(
                name: "audit_logs");
        }
    }
}
