using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistenzaTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_assistenza",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Oggetto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Stato = table.Column<int>(type: "integer", nullable: false),
                    AutoreUtenteId = table.Column<Guid>(type: "uuid", nullable: true),
                    UltimoMessaggioClienteAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UltimoMessaggioStaffAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LettoClienteAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LettoStaffAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChiusoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnonimizzatoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_assistenza", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_utenti_AutoreUtenteId",
                        column: x => x.AutoreUtenteId,
                        principalTable: "utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ticket_assistenza_messaggi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    DaStaff = table.Column<bool>(type: "boolean", nullable: false),
                    AutoreUtenteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Testo = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_assistenza_messaggi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_messaggi_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_messaggi_ticket_assistenza_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ticket_assistenza",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_messaggi_utenti_AutoreUtenteId",
                        column: x => x.AutoreUtenteId,
                        principalTable: "utenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ticket_assistenza_allegati",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessaggioId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeFile = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DimensioneByte = table.Column<long>(type: "bigint", nullable: false),
                    Percorso = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EliminatoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_assistenza_allegati", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_allegati_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_assistenza_allegati_ticket_assistenza_messaggi_Messa~",
                        column: x => x.MessaggioId,
                        principalTable: "ticket_assistenza_messaggi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_AutoreUtenteId",
                table: "ticket_assistenza",
                column: "AutoreUtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_Numero",
                table: "ticket_assistenza",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_Stato_UltimoMessaggioClienteAtUtc",
                table: "ticket_assistenza",
                columns: new[] { "Stato", "UltimoMessaggioClienteAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_StrutturaId",
                table: "ticket_assistenza",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_allegati_MessaggioId",
                table: "ticket_assistenza_allegati",
                column: "MessaggioId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_allegati_StrutturaId",
                table: "ticket_assistenza_allegati",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_messaggi_AutoreUtenteId",
                table: "ticket_assistenza_messaggi",
                column: "AutoreUtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_messaggi_StrutturaId",
                table: "ticket_assistenza_messaggi",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assistenza_messaggi_TicketId_CreatedAtUtc",
                table: "ticket_assistenza_messaggi",
                columns: new[] { "TicketId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_assistenza_allegati");

            migrationBuilder.DropTable(
                name: "ticket_assistenza_messaggi");

            migrationBuilder.DropTable(
                name: "ticket_assistenza");
        }
    }
}
