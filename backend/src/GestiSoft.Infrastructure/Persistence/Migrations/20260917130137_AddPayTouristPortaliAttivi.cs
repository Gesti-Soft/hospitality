using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayTouristPortaliAttivi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "paytourist_portali_attivi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayTouristIntegrazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdPortale = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paytourist_portali_attivi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_paytourist_portali_attivi_paytourist_integrazioni_PayTouris~",
                        column: x => x.PayTouristIntegrazioneId,
                        principalTable: "paytourist_integrazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_paytourist_portali_attivi_PayTouristIntegrazioneId_IdPortale",
                table: "paytourist_portali_attivi",
                columns: new[] { "PayTouristIntegrazioneId", "IdPortale" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "paytourist_portali_attivi");
        }
    }
}
