using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AggiungiFatturazioneSdi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SdiAbilitato",
                table: "strutture",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SdiAttivo",
                table: "impostazioni_struttura",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EsitoSdiAtUtc",
                table: "dati_fattura",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FatturaStornataId",
                table: "dati_fattura",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificativoSdi",
                table: "dati_fattura",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviataSdiAtUtc",
                table: "dati_fattura",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotaSdi",
                table: "dati_fattura",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatoSdi",
                table: "dati_fattura",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contatori_progressivo_invio",
                columns: table => new
                {
                    CodiceFiscale = table.Column<string>(type: "character varying(28)", maxLength: 28, nullable: false),
                    UltimoProgressivo = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contatori_progressivo_invio", x => x.CodiceFiscale);
                });

            migrationBuilder.CreateTable(
                name: "file_ricevuti_sdi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentificativoSdi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Prova = table.Column<bool>(type: "boolean", nullable: false),
                    NomeFile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    File = table.Column<byte[]>(type: "bytea", nullable: false),
                    NomeFileMetadati = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Metadati = table.Column<byte[]>(type: "bytea", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentificativoCessionario = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Fornitore = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Esito = table.Column<int>(type: "integer", nullable: false),
                    Messaggio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RicevutoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_ricevuti_sdi", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trasmissioni_fattura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatiFatturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    NomeFile = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProgressivoInvio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CodiceTrasmittente = table.Column<string>(type: "character varying(28)", maxLength: 28, nullable: false),
                    ViaCanale = table.Column<bool>(type: "boolean", nullable: false),
                    Prova = table.Column<bool>(type: "boolean", nullable: false),
                    Xml = table.Column<byte[]>(type: "bytea", nullable: false),
                    Esito = table.Column<int>(type: "integer", nullable: false),
                    IdentificativoSdi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InviataAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EsitoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Nota = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InviataDa = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trasmissioni_fattura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trasmissioni_fattura_dati_fattura_DatiFatturaId",
                        column: x => x.DatiFatturaId,
                        principalTable: "dati_fattura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trasmissioni_fattura_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fatture_ricevute",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileRicevutoSdiId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndiceNelFile = table.Column<int>(type: "integer", nullable: false),
                    IdentificativoSdi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Fornitore = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FornitorePartitaIva = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FornitoreCodiceFiscale = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TipoDocumento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Divisa = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ImportoTotale = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Imponibile = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Imposta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Impronta = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RicevutaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LettaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fatture_ricevute", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fatture_ricevute_file_ricevuti_sdi_FileRicevutoSdiId",
                        column: x => x.FileRicevutoSdiId,
                        principalTable: "file_ricevuti_sdi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fatture_ricevute_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ricevute_sdi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrasmissioneFatturaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    IdentificativoSdi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NomeFileFattura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NomeFile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Xml = table.Column<byte[]>(type: "bytea", nullable: false),
                    Prova = table.Column<bool>(type: "boolean", nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RicevutaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ricevute_sdi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ricevute_sdi_trasmissioni_fattura_TrasmissioneFatturaId",
                        column: x => x.TrasmissioneFatturaId,
                        principalTable: "trasmissioni_fattura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_FatturaStornataId",
                table: "dati_fattura",
                column: "FatturaStornataId");

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_IdentificativoSdi",
                table: "dati_fattura",
                column: "IdentificativoSdi");

            migrationBuilder.CreateIndex(
                name: "IX_fatture_ricevute_FileRicevutoSdiId",
                table: "fatture_ricevute",
                column: "FileRicevutoSdiId");

            migrationBuilder.CreateIndex(
                name: "IX_fatture_ricevute_StrutturaId",
                table: "fatture_ricevute",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_fatture_ricevute_StrutturaId_Data",
                table: "fatture_ricevute",
                columns: new[] { "StrutturaId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_fatture_ricevute_StrutturaId_Impronta",
                table: "fatture_ricevute",
                columns: new[] { "StrutturaId", "Impronta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_ricevuti_sdi_IdentificativoSdi_Prova",
                table: "file_ricevuti_sdi",
                columns: new[] { "IdentificativoSdi", "Prova" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_file_ricevuti_sdi_StrutturaId",
                table: "file_ricevuti_sdi",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ricevute_sdi_StrutturaId",
                table: "ricevute_sdi",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ricevute_sdi_TrasmissioneFatturaId",
                table: "ricevute_sdi",
                column: "TrasmissioneFatturaId");

            migrationBuilder.CreateIndex(
                name: "IX_trasmissioni_fattura_DatiFatturaId",
                table: "trasmissioni_fattura",
                column: "DatiFatturaId");

            migrationBuilder.CreateIndex(
                name: "IX_trasmissioni_fattura_IdentificativoSdi",
                table: "trasmissioni_fattura",
                column: "IdentificativoSdi");

            migrationBuilder.CreateIndex(
                name: "IX_trasmissioni_fattura_NomeFile",
                table: "trasmissioni_fattura",
                column: "NomeFile",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trasmissioni_fattura_StrutturaId",
                table: "trasmissioni_fattura",
                column: "StrutturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_dati_fattura_dati_fattura_FatturaStornataId",
                table: "dati_fattura",
                column: "FatturaStornataId",
                principalTable: "dati_fattura",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dati_fattura_dati_fattura_FatturaStornataId",
                table: "dati_fattura");

            migrationBuilder.DropTable(
                name: "contatori_progressivo_invio");

            migrationBuilder.DropTable(
                name: "fatture_ricevute");

            migrationBuilder.DropTable(
                name: "ricevute_sdi");

            migrationBuilder.DropTable(
                name: "file_ricevuti_sdi");

            migrationBuilder.DropTable(
                name: "trasmissioni_fattura");

            migrationBuilder.DropIndex(
                name: "IX_dati_fattura_FatturaStornataId",
                table: "dati_fattura");

            migrationBuilder.DropIndex(
                name: "IX_dati_fattura_IdentificativoSdi",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "SdiAbilitato",
                table: "strutture");

            migrationBuilder.DropColumn(
                name: "SdiAttivo",
                table: "impostazioni_struttura");

            migrationBuilder.DropColumn(
                name: "EsitoSdiAtUtc",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "FatturaStornataId",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "IdentificativoSdi",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "InviataSdiAtUtc",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "NotaSdi",
                table: "dati_fattura");

            migrationBuilder.DropColumn(
                name: "StatoSdi",
                table: "dati_fattura");
        }
    }
}
