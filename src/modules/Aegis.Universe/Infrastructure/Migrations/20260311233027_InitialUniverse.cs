using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace Aegis.Universe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialUniverse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "symbol",
                columns: table => new
                {
                    symbol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    asset_class = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbol", x => x.symbol_id);
                });

            migrationBuilder.CreateTable(
                name: "watchlist",
                columns: table => new
                {
                    watchlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    watchlist_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    is_mutable = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlist", x => x.watchlist_id);
                });

            migrationBuilder.CreateTable(
                name: "watchlist_item",
                columns: table => new
                {
                    watchlist_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    watchlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_utc = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlist_item", x => x.watchlist_item_id);
                    table.ForeignKey(
                        name: "FK_watchlist_item_symbol_symbol_id",
                        column: x => x.symbol_id,
                        principalTable: "symbol",
                        principalColumn: "symbol_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_watchlist_item_watchlist_watchlist_id",
                        column: x => x.watchlist_id,
                        principalTable: "watchlist",
                        principalColumn: "watchlist_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_symbol_ticker",
                table: "symbol",
                column: "ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_normalized_name",
                table: "watchlist",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_item_symbol_id",
                table: "watchlist_item",
                column: "symbol_id");

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_item_watchlist_id_symbol_id",
                table: "watchlist_item",
                columns: new[] { "watchlist_id", "symbol_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "watchlist_item");

            migrationBuilder.DropTable(
                name: "symbol");

            migrationBuilder.DropTable(
                name: "watchlist");
        }
    }
}
