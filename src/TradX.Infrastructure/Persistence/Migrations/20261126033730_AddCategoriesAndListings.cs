using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TradX.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condition = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    neighborhood = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    availability = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    wanted_in_return = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_donation = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    status_before_suspension = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    suspension_appeal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listings", x => x.id);
                    table.ForeignKey(
                        name: "fk_listings_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_listings_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "listing_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_photos_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "is_active", "name" },
                values: new object[,]
                {
                    { new Guid("0193f0a0-0000-7000-8000-000000000001"), true, "Électronique" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000002"), true, "Maison et jardin" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000003"), true, "Vêtements et mode" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000004"), true, "Cours et soutien scolaire" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000005"), true, "Bricolage et réparation" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000006"), true, "Informatique" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000007"), true, "Transport et déménagement" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000008"), true, "Beauté et bien-être" },
                    { new Guid("0193f0a0-0000-7000-8000-000000000009"), true, "Cuisine et alimentation" },
                    { new Guid("0193f0a0-0000-7000-8000-00000000000a"), true, "Enfants et bébé" },
                    { new Guid("0193f0a0-0000-7000-8000-00000000000b"), true, "Sport et loisirs" },
                    { new Guid("0193f0a0-0000-7000-8000-00000000000c"), true, "Autres" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_photos_listing_id_position",
                table: "listing_photos",
                columns: new[] { "listing_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_category_id",
                table: "listings",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_listings_owner_id_status",
                table: "listings",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_category_id_published_at",
                table: "listings",
                columns: new[] { "status", "category_id", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_published_at",
                table: "listings",
                columns: new[] { "status", "published_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_photos");

            migrationBuilder.DropTable(
                name: "listings");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
