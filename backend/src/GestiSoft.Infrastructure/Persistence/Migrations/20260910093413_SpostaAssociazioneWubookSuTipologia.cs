using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpostaAssociazioneWubookSuTipologia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Nuove colonne Wubook su tipologie_camera (il pool), prima di toccare "camere".
            migrationBuilder.AddColumn<string>(
                name: "CodiceCameraWubook",
                table: "tipologie_camera",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCameraWubook",
                table: "tipologie_camera",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WubookAttiva",
                table: "tipologie_camera",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WubookSoloWoodoo",
                table: "tipologie_camera",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TipologiaId",
                table: "prenotazioni",
                type: "uuid",
                nullable: true);

            // 2. Carry-forward: ogni camera reale già associata a Wubook riporta la propria
            // associazione sulla Tipologia a cui appartiene — verificato prima di scrivere questa
            // migration che nessuna Tipologia della struttura reale ha due camere associate con
            // IdCameraWubook diversi (in quel caso l'UPDATE...FROM sceglierebbe una riga qualunque,
            // non deterministico: da qui l'importanza di averlo escluso a priori).
            migrationBuilder.Sql(@"
                UPDATE tipologie_camera AS t
                SET ""IdCameraWubook"" = c.""IdCameraWubook"",
                    ""WubookAttiva"" = c.""WubookAttiva"",
                    ""CodiceCameraWubook"" = c.""CodiceCameraWubook"",
                    ""WubookSoloWoodoo"" = c.""WubookSoloWoodoo""
                FROM camere AS c
                WHERE c.""TipologiaId"" = t.""Id"" AND c.""WubookAttiva"" = true;
            ");

            // 3. Rimuove le vecchie colonne per-camera, ora che i dati reali sono stati riportati.
            migrationBuilder.DropIndex(
                name: "IX_camere_StrutturaId_IdCameraWubook",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "CodiceCameraWubook",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "IdCameraWubook",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "PrezzoWubookOverride",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "WubookAttiva",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "WubookDisponibilita",
                table: "camere");

            migrationBuilder.DropColumn(
                name: "WubookSoloWoodoo",
                table: "camere");

            migrationBuilder.CreateIndex(
                name: "IX_tipologie_camera_StrutturaId_IdCameraWubook",
                table: "tipologie_camera",
                columns: new[] { "StrutturaId", "IdCameraWubook" });

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_TipologiaId",
                table: "prenotazioni",
                column: "TipologiaId");

            migrationBuilder.AddForeignKey(
                name: "FK_prenotazioni_tipologie_camera_TipologiaId",
                table: "prenotazioni",
                column: "TipologiaId",
                principalTable: "tipologie_camera",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prenotazioni_tipologie_camera_TipologiaId",
                table: "prenotazioni");

            migrationBuilder.DropIndex(
                name: "IX_tipologie_camera_StrutturaId_IdCameraWubook",
                table: "tipologie_camera");

            migrationBuilder.DropIndex(
                name: "IX_prenotazioni_TipologiaId",
                table: "prenotazioni");

            migrationBuilder.AddColumn<string>(
                name: "CodiceCameraWubook",
                table: "camere",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCameraWubook",
                table: "camere",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrezzoWubookOverride",
                table: "camere",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WubookAttiva",
                table: "camere",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WubookDisponibilita",
                table: "camere",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "WubookSoloWoodoo",
                table: "camere",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Carry-back best-effort (solo per un rollback in locale/sviluppo): riporta su ogni
            // camera i valori della propria Tipologia, se era associata.
            migrationBuilder.Sql(@"
                UPDATE camere AS c
                SET ""IdCameraWubook"" = t.""IdCameraWubook"",
                    ""WubookAttiva"" = t.""WubookAttiva"",
                    ""CodiceCameraWubook"" = t.""CodiceCameraWubook"",
                    ""WubookSoloWoodoo"" = t.""WubookSoloWoodoo""
                FROM tipologie_camera AS t
                WHERE c.""TipologiaId"" = t.""Id"" AND t.""WubookAttiva"" = true;
            ");

            migrationBuilder.DropColumn(
                name: "CodiceCameraWubook",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "IdCameraWubook",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "WubookAttiva",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "WubookSoloWoodoo",
                table: "tipologie_camera");

            migrationBuilder.DropColumn(
                name: "TipologiaId",
                table: "prenotazioni");

            migrationBuilder.CreateIndex(
                name: "IX_camere_StrutturaId_IdCameraWubook",
                table: "camere",
                columns: new[] { "StrutturaId", "IdCameraWubook" });
        }
    }
}
