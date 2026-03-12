using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aegis.MarketData.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bar",
                columns: table => new
                {
                    bar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    interval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    bar_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    open = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    high = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    low = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    close = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    volume = table.Column<long>(type: "bigint", nullable: false),
                    session_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    market_date = table.Column<DateOnly>(type: "date", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_feed = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    runtime_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bar", x => x.bar_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bar_symbol_interval",
                table: "bar",
                columns: new[] { "symbol", "interval" });

            migrationBuilder.CreateIndex(
                name: "IX_bar_symbol_interval_bar_time_utc",
                table: "bar",
                columns: new[] { "symbol", "interval", "bar_time_utc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bar");
        }
    }
}
