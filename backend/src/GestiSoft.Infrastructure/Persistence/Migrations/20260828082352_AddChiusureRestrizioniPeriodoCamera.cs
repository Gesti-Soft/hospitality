using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChiusureRestrizioniPeriodoCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chiusure_camera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataInizio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataFine = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    Quantita = table.Column<int>(type: "integer", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chiusure_camera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chiusure_camera_camere_CameraId",
                        column: x => x.CameraId,
                        principalTable: "camere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chiusure_camera_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restrizioni_soggiorno_camera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataInizio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataFine = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MinStay = table.Column<int>(type: "integer", nullable: true),
                    MaxStay = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restrizioni_soggiorno_camera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restrizioni_soggiorno_camera_camere_CameraId",
                        column: x => x.CameraId,
                        principalTable: "camere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_restrizioni_soggiorno_camera_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chiusure_camera_CameraId_DataInizio_DataFine",
                table: "chiusure_camera",
                columns: new[] { "CameraId", "DataInizio", "DataFine" });

            migrationBuilder.CreateIndex(
                name: "IX_chiusure_camera_StrutturaId",
                table: "chiusure_camera",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_restrizioni_soggiorno_camera_CameraId_DataInizio_DataFine",
                table: "restrizioni_soggiorno_camera",
                columns: new[] { "CameraId", "DataInizio", "DataFine" });

            migrationBuilder.CreateIndex(
                name: "IX_restrizioni_soggiorno_camera_StrutturaId",
                table: "restrizioni_soggiorno_camera",
                column: "StrutturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chiusure_camera");

            migrationBuilder.DropTable(
                name: "restrizioni_soggiorno_camera");
        }
    }
}
