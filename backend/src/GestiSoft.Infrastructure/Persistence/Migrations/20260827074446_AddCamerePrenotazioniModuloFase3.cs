using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCamerePrenotazioniModuloFase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TipologiaId",
                table: "prezzi_camera",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_prezzi_camera_TipologiaId_DataInizio_DataFine",
                table: "prezzi_camera",
                columns: new[] { "TipologiaId", "DataInizio", "DataFine" });

            migrationBuilder.AddForeignKey(
                name: "FK_prezzi_camera_tipologie_camera_TipologiaId",
                table: "prezzi_camera",
                column: "TipologiaId",
                principalTable: "tipologie_camera",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prezzi_camera_tipologie_camera_TipologiaId",
                table: "prezzi_camera");

            migrationBuilder.DropIndex(
                name: "IX_prezzi_camera_TipologiaId_DataInizio_DataFine",
                table: "prezzi_camera");

            migrationBuilder.DropColumn(
                name: "TipologiaId",
                table: "prezzi_camera");
        }
    }
}
