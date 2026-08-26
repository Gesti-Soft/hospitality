using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthImpostazioniLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "impostazioni_struttura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoliziaStatoAttiva = table.Column<bool>(type: "boolean", nullable: false),
                    OsservatorioAttivo = table.Column<bool>(type: "boolean", nullable: false),
                    PayTouristAttivo = table.Column<bool>(type: "boolean", nullable: false),
                    OraInvioGiornaliero = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TassaSoggiornoPrezzo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TassaSoggiornoMaxGiorni = table.Column<int>(type: "integer", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_impostazioni_struttura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_impostazioni_struttura_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "log_eventi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Livello = table.Column<int>(type: "integer", nullable: false),
                    Messaggio = table.Column<string>(type: "text", nullable: false),
                    Dettaglio = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: true),
                    Origine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_eventi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "utenti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Cognome = table.Column<string>(type: "text", nullable: true),
                    IsSuperAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attivo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_utenti_clienti_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "utenti_strutture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UtenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ruolo = table.Column<int>(type: "integer", nullable: false),
                    BookingRead = table.Column<bool>(type: "boolean", nullable: false),
                    BookingWrite = table.Column<bool>(type: "boolean", nullable: false),
                    ReservationRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReservationWrite = table.Column<bool>(type: "boolean", nullable: false),
                    StatePoliceRead = table.Column<bool>(type: "boolean", nullable: false),
                    StatePoliceWrite = table.Column<bool>(type: "boolean", nullable: false),
                    StatePoliceSettings = table.Column<bool>(type: "boolean", nullable: false),
                    SettingAgency = table.Column<bool>(type: "boolean", nullable: false),
                    SettingUser = table.Column<bool>(type: "boolean", nullable: false),
                    SettingRoomRead = table.Column<bool>(type: "boolean", nullable: false),
                    SettingRoomWrite = table.Column<bool>(type: "boolean", nullable: false),
                    RoomStatusUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    FinanceRead = table.Column<bool>(type: "boolean", nullable: false),
                    FinanceWrite = table.Column<bool>(type: "boolean", nullable: false),
                    RestaurantRead = table.Column<bool>(type: "boolean", nullable: false),
                    RestaurantWrite = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_utenti_strutture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_utenti_strutture_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_utenti_strutture_utenti_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_impostazioni_struttura_StrutturaId",
                table: "impostazioni_struttura",
                column: "StrutturaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_log_eventi_ClienteId_StrutturaId_CreatedAtUtc",
                table: "log_eventi",
                columns: new[] { "ClienteId", "StrutturaId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_log_eventi_CorrelationId",
                table: "log_eventi",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_utenti_ClienteId",
                table: "utenti",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_utenti_Email",
                table: "utenti",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_utenti_strutture_StrutturaId",
                table: "utenti_strutture",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_utenti_strutture_UtenteId_StrutturaId",
                table: "utenti_strutture",
                columns: new[] { "UtenteId", "StrutturaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "impostazioni_struttura");

            migrationBuilder.DropTable(
                name: "log_eventi");

            migrationBuilder.DropTable(
                name: "utenti_strutture");

            migrationBuilder.DropTable(
                name: "utenti");
        }
    }
}
