using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestiSoft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaOperativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canali_vendita",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canali_vendita", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canali_vendita_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comuni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codice = table.Column<long>(type: "bigint", nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provincia = table.Column<string>(type: "text", nullable: true),
                    CodiceBelfiore = table.Column<string>(type: "text", nullable: true),
                    Cap = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comuni", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dati_aziendali",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Iso2 = table.Column<string>(type: "text", nullable: true),
                    PIva = table.Column<string>(type: "text", nullable: true),
                    CodiceFiscale = table.Column<string>(type: "text", nullable: true),
                    Denominazione = table.Column<string>(type: "text", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Cognome = table.Column<string>(type: "text", nullable: true),
                    RegimeFiscale = table.Column<int>(type: "integer", nullable: true),
                    Indirizzo = table.Column<string>(type: "text", nullable: true),
                    NCivico = table.Column<string>(type: "text", nullable: true),
                    Cap = table.Column<string>(type: "text", nullable: true),
                    Comune = table.Column<string>(type: "text", nullable: true),
                    Provincia = table.Column<string>(type: "text", nullable: true),
                    Nazione = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dati_aziendali", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dati_aziendali_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dati_cliente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Iso2 = table.Column<string>(type: "text", nullable: true),
                    PIva = table.Column<string>(type: "text", nullable: true),
                    CodiceFiscale = table.Column<string>(type: "text", nullable: true),
                    Denominazione = table.Column<string>(type: "text", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Cognome = table.Column<string>(type: "text", nullable: true),
                    Indirizzo = table.Column<string>(type: "text", nullable: true),
                    NCivico = table.Column<string>(type: "text", nullable: true),
                    Cap = table.Column<string>(type: "text", nullable: true),
                    LuogoResidenza = table.Column<string>(type: "text", nullable: true),
                    Provincia = table.Column<string>(type: "text", nullable: true),
                    Cittadinanza = table.Column<string>(type: "text", nullable: true),
                    CodiceDestinatario = table.Column<string>(type: "text", nullable: true),
                    Pec = table.Column<string>(type: "text", nullable: true),
                    CustomerKey = table.Column<string>(type: "text", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dati_cliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dati_cliente_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documenti_identita",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    Codice = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documenti_identita", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "entrate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEntrata = table.Column<string>(type: "text", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    ImportoEntrata = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: true),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Anno = table.Column<int>(type: "integer", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entrate_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "spese",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoSpesa = table.Column<string>(type: "text", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    ImportoSpesa = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Descrizione = table.Column<string>(type: "text", nullable: true),
                    MetodoPagamento = table.Column<string>(type: "text", nullable: true),
                    DataSpesa = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Anno = table.Column<int>(type: "integer", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spese", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spese_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stati",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codice = table.Column<long>(type: "bigint", nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeInglese = table.Column<string>(type: "text", nullable: true),
                    Acronimo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stati", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipi_alloggiato",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codice = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Descrizione = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipi_alloggiato", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tipologie_camera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipologiaCamera = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SpesePulizia = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Animali = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Cauzione = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PrezzoDefault = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NumeroImplementoPersona = table.Column<int>(type: "integer", nullable: false),
                    Implemento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipologie_camera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tipologie_camera_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dati_fattura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatiClienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Progressivo = table.Column<int>(type: "integer", nullable: false),
                    TipoDocumento = table.Column<int>(type: "integer", nullable: true),
                    RegimeFiscale = table.Column<int>(type: "integer", nullable: true),
                    NumeroDocumento = table.Column<int>(type: "integer", nullable: false),
                    DataDocumento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Divisa = table.Column<string>(type: "text", nullable: true),
                    Descrizione = table.Column<string>(type: "text", nullable: true),
                    Quantita = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrezzoUnitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AliquotaIva = table.Column<int>(type: "integer", nullable: true),
                    Natura = table.Column<int>(type: "integer", nullable: true),
                    PrezzoTotale = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImportoTotale = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Arrotondamento = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Link = table.Column<string>(type: "text", nullable: true),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dati_fattura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dati_fattura_dati_cliente_DatiClienteId",
                        column: x => x.DatiClienteId,
                        principalTable: "dati_cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_dati_fattura_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "camere",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipologiaId = table.Column<Guid>(type: "uuid", nullable: true),
                    StateRoom = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CapacitaOspiti = table.Column<int>(type: "integer", nullable: true),
                    SoggiornoMinimo = table.Column<int>(type: "integer", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camere", x => x.Id);
                    table.ForeignKey(
                        name: "FK_camere_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_camere_tipologie_camera_TipologiaId",
                        column: x => x.TipologiaId,
                        principalTable: "tipologie_camera",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prenotazioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: true),
                    Agenzia = table.Column<string>(type: "text", nullable: true),
                    NumeroPrenotazione = table.Column<string>(type: "text", nullable: true),
                    ImportoPrenotazione = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ImportoPagato = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ImportoTotale = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CheckIn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckOut = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NumeroOspiti = table.Column<int>(type: "integer", nullable: true),
                    StatePolice = table.Column<bool>(type: "boolean", nullable: false),
                    PMS = table.Column<bool>(type: "boolean", nullable: false),
                    PayTourist = table.Column<bool>(type: "boolean", nullable: false),
                    CheckEditSelection = table.Column<string>(type: "text", nullable: true),
                    Supply = table.Column<string>(type: "text", nullable: true),
                    Anno = table.Column<int>(type: "integer", nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StatoPrenotazione = table.Column<int>(type: "integer", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prenotazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prenotazioni_camere_CameraId",
                        column: x => x.CameraId,
                        principalTable: "camere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prenotazioni_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prezzi_camera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataInizio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataFine = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrezzoPerNotte = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prezzi_camera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prezzi_camera_camere_CameraId",
                        column: x => x.CameraId,
                        principalTable: "camere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prezzi_camera_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cauzioni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportoCauzione = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DataInserimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cauzioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cauzioni_prenotazioni_PrenotazioneId",
                        column: x => x.PrenotazioneId,
                        principalTable: "prenotazioni",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cauzioni_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ospiti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoOspite = table.Column<string>(type: "text", nullable: true),
                    PrenotazioneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Permanenza = table.Column<int>(type: "integer", nullable: true),
                    DataNascita = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Sesso = table.Column<int>(type: "integer", nullable: true),
                    Cognome = table.Column<string>(type: "text", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Cittadinanza = table.Column<string>(type: "text", nullable: true),
                    LuogoNascita = table.Column<string>(type: "text", nullable: true),
                    StatoNascita = table.Column<string>(type: "text", nullable: true),
                    LuogoResidenza = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    NumeroDocumento = table.Column<string>(type: "text", nullable: true),
                    RilascioDocumento = table.Column<string>(type: "text", nullable: true),
                    EsenteDaTassa = table.Column<bool>(type: "boolean", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ospiti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ospiti_prenotazioni_PrenotazioneId",
                        column: x => x.PrenotazioneId,
                        principalTable: "prenotazioni",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ospiti_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ospiti_righe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OspiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CameraId = table.Column<Guid>(type: "uuid", nullable: true),
                    Permanenza = table.Column<int>(type: "integer", nullable: true),
                    DataNascita = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Sesso = table.Column<int>(type: "integer", nullable: true),
                    Cognome = table.Column<string>(type: "text", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Cittadinanza = table.Column<string>(type: "text", nullable: true),
                    LuogoNascita = table.Column<string>(type: "text", nullable: true),
                    StatoNascita = table.Column<string>(type: "text", nullable: true),
                    LuogoResidenza = table.Column<string>(type: "text", nullable: true),
                    PostoLetto = table.Column<bool>(type: "boolean", nullable: true),
                    EsenteDaTassa = table.Column<bool>(type: "boolean", nullable: false),
                    StrutturaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ospiti_righe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ospiti_righe_camere_CameraId",
                        column: x => x.CameraId,
                        principalTable: "camere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ospiti_righe_ospiti_OspiteId",
                        column: x => x.OspiteId,
                        principalTable: "ospiti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ospiti_righe_strutture_StrutturaId",
                        column: x => x.StrutturaId,
                        principalTable: "strutture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_camere_StrutturaId",
                table: "camere",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_camere_StrutturaId_Nome",
                table: "camere",
                columns: new[] { "StrutturaId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_camere_TipologiaId",
                table: "camere",
                column: "TipologiaId");

            migrationBuilder.CreateIndex(
                name: "IX_canali_vendita_StrutturaId",
                table: "canali_vendita",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_canali_vendita_StrutturaId_Descrizione",
                table: "canali_vendita",
                columns: new[] { "StrutturaId", "Descrizione" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cauzioni_PrenotazioneId",
                table: "cauzioni",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_cauzioni_StrutturaId",
                table: "cauzioni",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_comuni_Codice",
                table: "comuni",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dati_aziendali_StrutturaId",
                table: "dati_aziendali",
                column: "StrutturaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dati_cliente_StrutturaId",
                table: "dati_cliente",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_DatiClienteId",
                table: "dati_fattura",
                column: "DatiClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_StrutturaId",
                table: "dati_fattura",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_dati_fattura_StrutturaId_Anno_Progressivo",
                table: "dati_fattura",
                columns: new[] { "StrutturaId", "Anno", "Progressivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documenti_identita_Codice",
                table: "documenti_identita",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entrate_StrutturaId",
                table: "entrate",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_entrate_StrutturaId_Data",
                table: "entrate",
                columns: new[] { "StrutturaId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_PrenotazioneId",
                table: "ospiti",
                column: "PrenotazioneId");

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_StrutturaId",
                table: "ospiti",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_righe_CameraId",
                table: "ospiti_righe",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_righe_OspiteId",
                table: "ospiti_righe",
                column: "OspiteId");

            migrationBuilder.CreateIndex(
                name: "IX_ospiti_righe_StrutturaId",
                table: "ospiti_righe",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_CameraId",
                table: "prenotazioni",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_StrutturaId",
                table: "prenotazioni",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_prenotazioni_StrutturaId_CheckIn_CheckOut",
                table: "prenotazioni",
                columns: new[] { "StrutturaId", "CheckIn", "CheckOut" });

            migrationBuilder.CreateIndex(
                name: "IX_prezzi_camera_CameraId_DataInizio_DataFine",
                table: "prezzi_camera",
                columns: new[] { "CameraId", "DataInizio", "DataFine" });

            migrationBuilder.CreateIndex(
                name: "IX_prezzi_camera_StrutturaId",
                table: "prezzi_camera",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_spese_StrutturaId",
                table: "spese",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_spese_StrutturaId_DataSpesa",
                table: "spese",
                columns: new[] { "StrutturaId", "DataSpesa" });

            migrationBuilder.CreateIndex(
                name: "IX_stati_Codice",
                table: "stati",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipi_alloggiato_Codice",
                table: "tipi_alloggiato",
                column: "Codice",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tipologie_camera_StrutturaId",
                table: "tipologie_camera",
                column: "StrutturaId");

            migrationBuilder.CreateIndex(
                name: "IX_tipologie_camera_StrutturaId_TipologiaCamera",
                table: "tipologie_camera",
                columns: new[] { "StrutturaId", "TipologiaCamera" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canali_vendita");

            migrationBuilder.DropTable(
                name: "cauzioni");

            migrationBuilder.DropTable(
                name: "comuni");

            migrationBuilder.DropTable(
                name: "dati_aziendali");

            migrationBuilder.DropTable(
                name: "dati_fattura");

            migrationBuilder.DropTable(
                name: "documenti_identita");

            migrationBuilder.DropTable(
                name: "entrate");

            migrationBuilder.DropTable(
                name: "ospiti_righe");

            migrationBuilder.DropTable(
                name: "prezzi_camera");

            migrationBuilder.DropTable(
                name: "spese");

            migrationBuilder.DropTable(
                name: "stati");

            migrationBuilder.DropTable(
                name: "tipi_alloggiato");

            migrationBuilder.DropTable(
                name: "dati_cliente");

            migrationBuilder.DropTable(
                name: "ospiti");

            migrationBuilder.DropTable(
                name: "prenotazioni");

            migrationBuilder.DropTable(
                name: "camere");

            migrationBuilder.DropTable(
                name: "tipologie_camera");
        }
    }
}
