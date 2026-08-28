using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessioneServiziCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlloggiatiWebAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "OsservatorioAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayTouristAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WubookAbilitato",
                table: "clienti",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlloggiatiWebAbilitato",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "OsservatorioAbilitato",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "PayTouristAbilitato",
                table: "clienti");

            migrationBuilder.DropColumn(
                name: "WubookAbilitato",
                table: "clienti");
        }
    }
}
