using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OspitePrenotazioneUnoAUno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ospiti_PrenotazioneId",
                table: "ospiti");

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_PrenotazioneId",
                table: "ospiti",
                column: "PrenotazioneId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ospiti_PrenotazioneId",
                table: "ospiti");

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_PrenotazioneId",
                table: "ospiti",
                column: "PrenotazioneId");
        }
    }
}
