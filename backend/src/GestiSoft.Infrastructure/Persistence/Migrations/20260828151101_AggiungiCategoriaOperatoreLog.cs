using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiCategoriaOperatoreLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "log_eventi",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operatore",
                table: "log_eventi",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "log_eventi");

            migrationBuilder.DropColumn(
                name: "Operatore",
                table: "log_eventi");
        }
    }
}
