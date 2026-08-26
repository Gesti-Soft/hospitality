using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clienti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RagioneSociale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PartitaIva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clienti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "strutture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strutture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_strutture_clienti_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_strutture_ClienteId",
                table: "strutture",
                column: "ClienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "strutture");

            migrationBuilder.DropTable(
                name: "clienti");
        }
    }
}
