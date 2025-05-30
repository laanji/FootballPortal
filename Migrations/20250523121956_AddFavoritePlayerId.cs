using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FootballPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoritePlayerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FavoritePlayerId",
                table: "Users",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FavoritePlayerId",
                table: "Users");
        }
    }
}
