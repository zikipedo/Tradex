using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExchangesAndTimeBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exchanges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_listing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beneficiary_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requester_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    owner_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dispute_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchanges", x => x.id);
                    table.CheckConstraint("ck_exchanges_duration", "duration_minutes IS NULL OR (duration_minutes BETWEEN 30 AND 480 AND duration_minutes % 30 = 0)");
                    table.CheckConstraint("ck_exchanges_not_self", "requester_id <> owner_id");
                    table.ForeignKey(
                        name: "fk_exchanges_listings_offered_listing_id",
                        column: x => x.offered_listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exchanges_listings_target_listing_id",
                        column: x => x.target_listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exchanges_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_exchanges_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "time_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exchange_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_transactions", x => x.id);
                    table.CheckConstraint("ck_time_transactions_not_self", "from_user_id <> to_user_id");
                    table.CheckConstraint("ck_time_transactions_positive", "minutes > 0");
                    table.ForeignKey(
                        name: "fk_time_transactions_exchanges_exchange_id",
                        column: x => x.exchange_id,
                        principalTable: "exchanges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_time_transactions_users_from_user_id",
                        column: x => x.from_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_time_transactions_users_to_user_id",
                        column: x => x.to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exchanges_offered_listing_id",
                table: "exchanges",
                column: "offered_listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_exchanges_owner_id_created_at",
                table: "exchanges",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exchanges_requester_id_created_at",
                table: "exchanges",
                columns: new[] { "requester_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exchanges_status_expires_at",
                table: "exchanges",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_exchanges_target_listing_id",
                table: "exchanges",
                column: "target_listing_id");

            migrationBuilder.CreateIndex(
                name: "ix_time_transactions_exchange_id",
                table: "time_transactions",
                column: "exchange_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_transactions_from_user_id_created_at",
                table: "time_transactions",
                columns: new[] { "from_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_time_transactions_to_user_id_created_at",
                table: "time_transactions",
                columns: new[] { "to_user_id", "created_at" });

            // Grand livre de la banque de temps append-only : la base refuse toute modification ou suppression.
            migrationBuilder.Sql("""
                CREATE FUNCTION time_transactions_block_changes() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'time_transactions est en ajout seul (append-only)';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_time_transactions_no_update_delete
                    BEFORE UPDATE OR DELETE ON time_transactions
                    FOR EACH ROW EXECUTE FUNCTION time_transactions_block_changes();

                CREATE TRIGGER trg_time_transactions_no_truncate
                    BEFORE TRUNCATE ON time_transactions
                    FOR EACH STATEMENT EXECUTE FUNCTION time_transactions_block_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_time_transactions_no_truncate ON time_transactions;
                DROP TRIGGER IF EXISTS trg_time_transactions_no_update_delete ON time_transactions;
                DROP FUNCTION IF EXISTS time_transactions_block_changes();
                """);

            migrationBuilder.DropTable(
                name: "time_transactions");

            migrationBuilder.DropTable(
                name: "exchanges");
        }
    }
}
