using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MessagesCursorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_messages_exchange_id_id",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "ix_messages_exchange_id_sent_at_id",
                table: "messages",
                columns: new[] { "exchange_id", "sent_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_messages_exchange_id_sent_at_id",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "ix_messages_exchange_id_id",
                table: "messages",
                columns: new[] { "exchange_id", "id" });
        }
    }
}
