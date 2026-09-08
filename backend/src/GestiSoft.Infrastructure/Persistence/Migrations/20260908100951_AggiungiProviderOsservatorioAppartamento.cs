using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiProviderOsservatorioAppartamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Provider",
                table: "osservatorio_appartamenti",
                type: "integer",
                nullable: false,
                defaultValue: 1); // ProviderOsservatorio.Sicilia — l'unico esistente, tutte le righe correnti sono Sicilia
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "osservatorio_appartamenti");
        }
    }
}
