# Guida all'integrazione del Sistema di Interscambio (SdI) — canale SDICoop

> Come portare in un altro software la trasmissione e la ricezione delle fatture elettroniche,
> partendo da zero: accreditamento del canale, certificati, infrastruttura, generazione dell'XML,
> invio, ricevute, ricezione delle fatture dei fornitori, collaudo, produzione e sorveglianza.
>
> È ricavata da **ManageSuite**, dove tutto questo funziona in produzione dal settembre 2026. Ogni
> regola segnata con ⚠️ è costata almeno uno scarto, un collaudo fallito o una giornata di
> diagnosi. Gli esempi di codice sono in **C# (.NET 8+, ASP.NET Core)** per il backend, con
> indicazioni per un frontend **React**.
>
> Redatta il 7 ottobre 2026. Le specifiche dell'Agenzia cambiano: dove la guida cita un menu o un
> limite numerico, verificarlo sulla documentazione ufficiale prima di contarci.

---

## Indice

0. [Segnaposto e ipotesi](#0-segnaposto-e-ipotesi)
1. [Il quadro d'insieme](#1-il-quadro-dinsieme)
2. [Accreditamento del canale e certificati](#2-accreditamento-del-canale-e-certificati)
3. [Infrastruttura: il vhost con mutua autenticazione](#3-infrastruttura-il-vhost-con-mutua-autenticazione)
4. [Generazione dell'XML FatturaPA](#4-generazione-dellxml-fatturapa)
5. [Trasmissione: RiceviFile](#5-trasmissione-ricevifile)
6. [Le ricevute: il servizio TrasmissioneFatture](#6-le-ricevute-il-servizio-trasmissionefatture)
7. [Ricezione delle fatture dei fornitori: RicezioneFatture](#7-ricezione-delle-fatture-dei-fornitori-ricezionefatture)
8. [Più aziende sullo stesso canale](#8-più-aziende-sullo-stesso-canale)
9. [Test di interoperabilità](#9-test-di-interoperabilità)
10. [Passaggio in produzione](#10-passaggio-in-produzione)
11. [Sorveglianza del canale](#11-sorveglianza-del-canale)
12. [Modello dati](#12-modello-dati)
13. [Frontend React: cosa mostrare e cosa evitare](#13-frontend-react-cosa-mostrare-e-cosa-evitare)
14. [Errori reali e loro causa](#14-errori-reali-e-loro-causa)
15. [Fuori ambito, e perché](#15-fuori-ambito-e-perché)
16. [Checklist](#16-checklist)

---

## 0. Segnaposto e ipotesi

| segnaposto | significato | esempio |
|---|---|---|
| `<PIVA>` | partita IVA del soggetto che accredita il canale (il **Sottoscrittore**) | `01234567890` |
| `<CF>` | il suo **codice fiscale**. Per una società di norma coincide con la P.IVA, per una ditta individuale è quello a 16 caratteri della persona | `RSSMRA80A01H501U` |
| `<HOST_SDI>` | il nome host dedicato al canale, **diverso** da quello dell'applicazione | `sdi.nuovosoftware.it` |
| `<CODICE_DEST>` | il codice destinatario B2B a 7 caratteri assegnato al canale in ricezione | `AB12C34` |

Ipotesi su cui è costruita la guida, emerse dalle domande iniziali:

- il canale è **nuovo e intestato alla nuova P.IVA** (accreditamento da zero, non si usa quello di
  ManageSuite);
- il software serve **più aziende** (multi-tenant): il canale trasmette per tutte e riceve per
  tutte;
- il server è un VPS con **Apache** (come ManageSuite: vhost Virtualmin, file `zz-sdi.conf`). Per
  Nginx c'è l'equivalente, non provato in produzione;
- solo fatture **B2B e B2C**. Le fatture verso la Pubblica Amministrazione richiedono la firma
  qualificata e restano fuori (§15).

---

## 1. Il quadro d'insieme

Il canale SDICoop è fatto di **quattro web service SOAP**: due li espone lo SdI e li chiamiamo noi,
due li esponiamo noi e li chiama lo SdI. Per il B2B ne servono **tre**.

```
         NOI (client)                                        SdI
  ┌──────────────────────┐   1. RiceviFile (fattura)   ┌─────────────────────┐
  │ invio fatture        │ ──────────────────────────► │ servizi.fatturapa.it│
  │                      │ ◄── IdentificativoSdI ───── │ /ricevi_file        │
  └──────────────────────┘                             └─────────────────────┘

         NOI (server, https://<HOST_SDI>)                    SdI
  ┌──────────────────────┐   2. TrasmissioneFatture    ┌─────────────────────┐
  │ /sdi/TrasmissioneFat.│ ◄────────────────────────── │ ricevute: consegna, │
  │  (6 operazioni)      │      (nessuna risposta)     │ scarto, mancata...  │
  ├──────────────────────┤   3. RicezioneFatture       │                     │
  │ /sdi/RicezioneFatture│ ◄────────────────────────── │ fatture dei         │
  │                      │ ──────── ER01 ────────────► │ fornitori           │
  └──────────────────────┘                             └─────────────────────┘

  (4. SdIRiceviNotifica: esito committente, solo fatture verso la PA → non serve)
```

Tutte e tre le connessioni sono a **mutua autenticazione TLS**: chi chiama presenta un certificato
client, chi risponde un certificato server, e **entrambi sono emessi dalla CA dell'Agenzia delle
Entrate** (con un'eccezione che costa cara, §5.2). Nessuna firma digitale qualificata serve per il
B2B: l'identità la dimostrano i certificati del canale.

Tempi di riferimento di ManageSuite, utili per pianificare:

| tappa | data |
|---|---|
| test di interoperabilità in trasmissione superati | 10/08/2026 |
| canale abilitato in produzione (conferma via PEC) | 17/09/2026 |
| test di interoperabilità in ricezione superato | 21/09/2026 |
| ricezione in produzione e codice destinatario B2B | 22/09/2026 |
| prima fattura vera spedita dal canale | 28/09/2026 |

---

## 2. Accreditamento del canale e certificati

### 2.1 Cosa serve prima di cominciare

- Un **VPS con IP pubblico** e un **nome host dedicato** al canale (`<HOST_SDI>`), con un record DNS
  A/AAAA. Non può essere lo stesso vhost dell'applicazione: il canale presenta un certificato
  dell'Agenzia, l'applicazione un certificato pubblico (Let's Encrypt), e un vhost ne presenta uno
  solo.
- Le credenziali per l'area riservata dell'Agenzia (SPID/CIE/CNS) del legale rappresentante, o di
  un incaricato, del soggetto `<PIVA>`.
- `openssl` sul server.

### 2.2 Dove si fa

Portale **`accreditamento.fatturapa.gov.it`** → *Ambiti* → **Sistema d'Interscambio**. Dal
5/5/2025 tutto passa da qui: il vecchio percorso *Strumenti → Gestire il canale* del portale
pubblico **non esiste più**, ma molte guide in rete lo descrivono ancora.

Si accredita un canale **SDICoop** (web service) sia in **trasmissione** sia in **ricezione**. La
modalità SDIFTP esiste ma è pensata per grandi volumi e richiede altri accordi: questa guida non la
tratta.

Il canale si può accreditare come **intermediario**: le istruzioni dicono che il canale «non deve
necessariamente essere intestato all'operatore che emette i documenti». Quindi **un solo canale
intestato alla nuova P.IVA trasmette e riceve per tutti i clienti** del software (§8).

### 2.3 Gli identificativi: attenzione a quale va dove

Nel modulo di accreditamento compaiono due identità, e finiscono in posti diversi:

| dato del modulo | dove finisce |
|---|---|
| **Sottoscrittore**: *Paese* + *Codice* (es. `IT` + `<PIVA>`) | nel **CN del certificato client**: `SDI-<Codice>`, e `C=<Paese>` |
| **Riferimento** (es. il codice fiscale della persona) | **non** entra nel certificato |

⚠️ **Il CN del certificato e l'`IdTrasmittente` della fattura non sono lo stesso numero.** Il CN
usa il *Codice* del Sottoscrittore scritto nel modulo (in ManageSuite: la P.IVA,
`CN=SDI-02837960810`). L'`IdTrasmittente/IdCodice` dentro l'XML e il prefisso del nome file vogliono
invece il **codice fiscale** del trasmittente (§4.3, scarto 00300). Per una società coincidono, per
una ditta individuale no. Tenerli in due variabili di configurazione distinte.

### 2.4 Chiavi e richieste di certificato (CSR)

Servono **due** certificati, e li firma la CA dell'Agenzia. Non si compra niente.

- **client**: ci identifica quando *noi* chiamiamo lo SdI (invio delle fatture);
- **server**: identifica il *nostro* endpoint quando lo SdI chiama noi (ricevute, fatture in
  arrivo).

Le chiavi private si generano **sul server che le userà**, e non escono di lì: sono l'identità del
canale.

```bash
sudo mkdir -p /etc/sdi/certs && cd /etc/sdi/certs

# --- client ---
sudo openssl genrsa -out sdi-client.key 2048
sudo openssl req -new -key sdi-client.key -out sdi-client.csr \
     -subj "/C=IT/CN=SDI-<PIVA>"

# --- server: CN = il nome host del canale (preferibile al CN SDI-…, perché lo verifica il TLS) ---
sudo openssl genrsa -out sdi-server.key 2048
sudo openssl req -new -key sdi-server.key -out sdi-server.csr \
     -subj "/C=IT/CN=<HOST_SDI>"

sudo chmod 600 *.key
# controllo prima di caricarle
openssl req -in sdi-client.csr -noout -subject
openssl req -in sdi-server.csr -noout -subject
```

Requisiti del portale: chiave **RSA 2048**, CSR in `.der`, `.pem` o `.arm`. Il `.csr` prodotto sopra
è PEM; se il portale lo rifiuta, convertirlo con
`openssl req -in x.csr -outform DER -out x.der`.

Fare subito un **backup cifrato** delle due chiavi fuori dal server (per esempio
`tar czf - *.key | gpg -c > sdi-chiavi.tgz.gpg`). Una chiave persa vuol dire un nuovo certificato,
e nel frattempo il canale è fermo.

### 2.5 Cosa restituisce il portale

Caricate le CSR, il portale rilascia due file, di solito `CER_CLIENT_IT<PIVA>.cer` e
`CER_SERVER_IT<PIVA>.cer`. Nel **kit di test** del portale ci sono poi i certificati dell'Agenzia
(in ManageSuite sono in `docs/SDI/KitDiTest/certificati/`):

| file | a cosa serve |
|---|---|
| `caentrate.cer` | CA di **produzione**: firma i certificati dei canali, compreso il client dello SdI |
| `CAEntratetest.cer` | CA di **collaudo** |
| `testservizi.fatturapa.it.cer` | certificato server dello SdI di collaudo |
| `servizi.fatturapa.it.cer` | certificato server dello SdI di produzione |
| `SistemaInterscambioFatturaPATest.cer` / `Sistema_Interscambio_Fattura_PA.cer` | parte pubblica del certificato **client** con cui lo SdI chiama noi |
| `SectigoRSA.cer`, `UserTrustCA.cer` | la catena **pubblica** del server di produzione (vedi §5.2) |

Il portale produce anche `RichiestaAccreditamento.zip` con WSDL e XSD. Gli stessi contratti sono
**pubblicati** su fatturapa.gov.it (*Documentazione SdI*, cartelle `/export/documenti/ws/…`) e
ManageSuite ne tiene copia in `docs/SDI/wsdl/`. Cercarli solo nel pacchetto è costato una serata.

### 2.6 Preparare i file per il server e per .NET

⚠️ **I certificati del canale arrivano in DER, Apache li vuole in PEM.** `openssl x509 -in file`
li legge lo stesso senza dirlo (OpenSSL 3 indovina il formato), quindi sembrano a posto e poi
Apache muore con `AH02562 ... no start line (Expecting: TRUSTED CERTIFICATE)`.

```bash
cd /etc/sdi/certs
openssl x509 -inform DER -in CER_SERVER_IT<PIVA>.cer -out sdi-server.crt
openssl x509 -inform DER -in CER_CLIENT_IT<PIVA>.cer -out sdi-client.crt
cat caentrate.cer CAEntratetest.cer > ca-agenzia.pem      # questi due sono già PEM

# Per .NET (e per Java): chiave + certificato client in un PKCS#12
openssl pkcs12 -export -inkey sdi-client.key -in sdi-client.crt \
        -name sdi-client -out sdi-client.p12             # chiede una password: va nel .env

chmod 644 *.crt *.pem ; chmod 600 *.key *.p12
# scadenza: è l'unico guasto del canale che si può prevenire (§11)
openssl x509 -in sdi-client.crt -noout -subject -enddate
```

Se l'applicazione gira in Docker, montare `/etc/sdi/certs` **in sola lettura** nel container.

### 2.7 Gli endpoint da dichiarare

Si dichiarano all'accreditamento e il codice deve rispondere **esattamente lì**:

| servizio | collaudo | produzione |
|---|---|---|
| ricevute | `https://<HOST_SDI>/sdi/test/TrasmissioneFatture` | `https://<HOST_SDI>/sdi/TrasmissioneFatture` |
| fatture in arrivo | `https://<HOST_SDI>/sdi/test/RicezioneFatture` | `https://<HOST_SDI>/sdi/RicezioneFatture` |

⚠️ **Percorsi distinti apposta.** Durante il collaudo lo SdI di prova manda ricevute e fatture
**finte**. Con un indirizzo solo, una ricevuta di prova marcherebbe «consegnata» una fattura vera,
e una fattura di prova finirebbe fra gli acquisti di un cliente. La distinzione la fa l'URL, non un
ragionamento nel codice: tutto ciò che arriva sotto `/sdi/test/` si **conserva e si mostra, ma non
si applica** (§6.5, §7.3).

---

## 3. Infrastruttura: il vhost con mutua autenticazione

### 3.1 Apache (configurazione in uso su ManageSuite)

File `/etc/apache2/sites-available/zz-sdi.conf`. Moduli: `sudo a2enmod ssl headers proxy proxy_http`.

```apache
<VirtualHost *:443>
    ServerName <HOST_SDI>

    # Il nostro certificato server, emesso dalla CA dell'Agenzia.
    # Chi lo apre col browser vede un avviso: è corretto, qui parla una macchina sola.
    SSLEngine on
    SSLCertificateFile    /etc/sdi/certs/sdi-server.crt
    SSLCertificateKeyFile /etc/sdi/certs/sdi-server.key
    SSLProtocol           -all +TLSv1.2 +TLSv1.3

    # Certificato client obbligatorio, firmato dalla CA dell'Agenzia (produzione + test)
    SSLCACertificateFile /etc/sdi/certs/ca-agenzia.pem
    SSLVerifyClient require
    SSLVerifyDepth 2

    # Prova della verifica per l'applicazione. L'unset viene PRIMA: senza, chiunque
    # raggiungesse il backend potrebbe dichiararsi verificato da sé.
    RequestHeader unset X-SdI-Client-Verified
    RequestHeader set   X-SdI-Client-Verified "%{SSL_CLIENT_VERIFY}s"
    RequestHeader set   X-SdI-Client-DN       "%{SSL_CLIENT_S_DN}s"

    ProxyPreserveHost On
    ProxyTimeout 120
    ProxyPass        /sdi/ http://127.0.0.1:5000/sdi/
    ProxyPassReverse /sdi/ http://127.0.0.1:5000/sdi/

    # Chiuso tutto, aperto solo il canale
    <Location />
        Require all denied
    </Location>

    # LA REGOLA CHE CONTA: la CA dell'Agenzia ha emesso un certificato client a OGNI
    # intermediario accreditato d'Italia. Fermarsi alla CA vorrebbe dire aprire a tutti
    # loro. Si pretende il soggetto esatto dello SdI (uguale in test e in produzione).
    <Location /sdi/>
        Require expr %{SSL_CLIENT_S_DN_CN} == "Sistema Interscambio Fattura PA"
    </Location>

    # Eccezione per la sonda della sorveglianza (§11), che chiama con il NOSTRO certificato.
    # DEVE stare dopo il blocco /sdi/: vince l'ultima <Location> che combacia.
    <Location /sdi/stato>
        Require expr %{SSL_CLIENT_S_DN_CN} in { "Sistema Interscambio Fattura PA", "SDI-<PIVA>" }
    </Location>

    ErrorLog  ${APACHE_LOG_DIR}/sdi-error.log
    CustomLog ${APACHE_LOG_DIR}/sdi-access.log combined
    LogLevel warn
</VirtualHost>
```

Tre trappole note:

- ⚠️ **Il nome del file comincia con `zz` di proposito.** Apache usa il *primo* vhost di una coppia
  indirizzo:porta come predefinito, e questo non deve mai esserlo: chiederebbe un certificato
  client a chiunque arrivi senza indicare un nome.
- ⚠️ **`apache2ctl configtest` non basta.** Controlla la sintassi ma non apre i certificati: dice
  «Syntax OK» su una configurazione che al reload fa cadere Apache, e con lui **tutti gli altri siti
  del server**. Attivarlo sempre con un ripristino automatico:
  ```bash
  sudo a2ensite zz-sdi && sudo systemctl restart apache2 && sleep 3
  curl -sf -o /dev/null https://<sito-principale>/ \
    || { sudo a2dissite zz-sdi; sudo systemctl restart apache2; }
  ```
- ⚠️ **Nell'eccezione per la sonda va il CN del *nostro* certificato** (`SDI-<PIVA>`). In
  ManageSuite la sonda rispondeva 403 perché quella riga mancava: la regola sul CN dello SdI era
  giusta, era la sonda a essere stata progettata senza tenerne conto.

Se il backend ha un prefisso (ManageSuite ha il context-path `/api/v1`), la traduzione la fa il
`ProxyPass`: gli indirizzi dichiarati all'Agenzia non devono contenerlo.

### 3.2 Nginx (equivalente, non provato in produzione)

```nginx
map $ssl_client_s_dn $sdi_cn_ok {
    default                                   0;
    "~CN=Sistema Interscambio Fattura PA(,|$)" 1;
}
map $ssl_client_s_dn $sonda_cn_ok {
    default                                   $sdi_cn_ok;
    "~CN=SDI-<PIVA>(,|$)"                     1;
}

server {
    listen 443 ssl;
    server_name <HOST_SDI>;

    ssl_certificate        /etc/sdi/certs/sdi-server.crt;
    ssl_certificate_key    /etc/sdi/certs/sdi-server.key;
    ssl_protocols          TLSv1.2 TLSv1.3;
    ssl_client_certificate /etc/sdi/certs/ca-agenzia.pem;
    ssl_verify_client      on;
    ssl_verify_depth       2;

    location = /sdi/stato {
        if ($sonda_cn_ok = 0) { return 403; }
        proxy_set_header X-SdI-Client-Verified $ssl_client_verify;   # sostituisce quello in arrivo
        proxy_set_header X-SdI-Client-DN       $ssl_client_s_dn;
        proxy_pass http://127.0.0.1:5000;
    }
    location /sdi/ {
        if ($sdi_cn_ok = 0) { return 403; }
        proxy_set_header X-SdI-Client-Verified $ssl_client_verify;
        proxy_set_header X-SdI-Client-DN       $ssl_client_s_dn;
        proxy_read_timeout 120s;
        proxy_pass http://127.0.0.1:5000;
    }
    location / { return 403; }
}
```

Con Nginx, `$ssl_client_verify` vale `SUCCESS` quando la verifica riesce, come con Apache. Il
formato di `$ssl_client_s_dn` (RFC 2253) va controllato sul primo accesso vero.

### 3.3 Lato applicazione

- Kestrel in ascolto **solo su `127.0.0.1`**: l'unica porta pubblica è quella di Apache.
- Un middleware su `/sdi/*` che accetta la richiesta solo se `X-SdI-Client-Verified` vale `SUCCESS`
  (§7.3 spiega perché in ricezione la conseguenza è diversa che in trasmissione). In sviluppo,
  senza mutua autenticazione davanti, il controllo si disattiva lasciando vuoto il nome
  dell'intestazione in configurazione.
- Un **log di accesso** dedicato a `/sdi/*`, con metodo, indirizzo, byte in ingresso, stato, byte in
  uscita ed estratto della risposta. Il portale, quando qualcosa non va, dice solo «Errore durante
  l'invio di un messaggio tramite WS - Err: NULL». Senza questa riga, distinguere «non è arrivata»
  da «è arrivata e ho risposto male» richiede il log di Apache, quello dell'applicazione e un
  pomeriggio. Per confronto: la nostra risposta `ER01` pesa 277 byte.
- **Fuso orario `Europe/Rome`** nel container (`ENV TZ=Europe/Rome`). In ManageSuite il container
  girava in UTC, e ogni data scritta dal server nasceva due ore indietro: la campanella diceva
  «circa 2 ore fa» per una ricevuta appena arrivata. In .NET la scelta strutturale giusta è
  conservare `DateTimeOffset` (o UTC) e mostrare in ora italiana.

---

## 4. Generazione dell'XML FatturaPA

### 4.1 Tracciato e strumenti

- Schema **FatturaPA 1.2.2**, namespace
  `http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2`. Serve anche
  `xmldsig-core-schema.xsd`, importato dallo schema principale: tenerli entrambi in locale (ManageSuite
  li ha in `backend/src/main/resources/xsd/`).
- La radice è qualificata (`<p:FatturaElettronica versione="FPR12" xmlns:p="…">`), gli elementi
  interni **no**.
- `versione="FPR12"` per tutti i destinatari privati, `FPA12` solo verso la PA.
- **Validare sempre contro l'XSD prima di salvare**: un XML che non passa lo schema non deve mai
  diventare una fattura emessa.

In C#: generare le classi con `xsd.exe` o `XmlSchemaClassGenerator` (`dotnet xscgen`), oppure
costruire l'albero con `XDocument`. Con poche decine di campi effettivamente usati, `XDocument` è
più semplice da leggere e da tenere allineato. La validazione:

```csharp
var schemi = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
schemi.Add("http://www.w3.org/2000/09/xmldsig#", "xsd/xmldsig-core-schema.xsd");
schemi.Add("http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2",
           "xsd/Schema_del_file_xml_FatturaPA_v1.2.2.xsd");
schemi.Compile();

var errori = new List<string>();
doc.Validate(schemi, (_, e) => errori.Add(e.Message));
if (errori.Count > 0)
    throw new FatturaNonValidaException("L'XML non rispetta lo schema FatturaPA: " + string.Join("; ", errori));
```

### 4.2 Due regole che valgono per tutto il documento

- ⚠️ **Testo solo Latin-1.** I campi testo ammettono Basic Latin + Latin-1 Supplement (`U+0020–007E`,
  `U+00A0–00FF`). Un trattino lungo, una virgoletta tipografica o il simbolo dell'euro fanno
  scartare il file. Prima di scrivere traslitterare: `– — −` → `-`, `‘ ’ ‚` → `'`, `“ ” „` → `"`,
  `…` → `...`, `€` → `EUR`, a capo e tabulazioni → spazio; il resto si toglie.
- **Lunghezze massime**, da tagliare dopo la traslitterazione: Denominazione 80, Nome/Cognome 60,
  Indirizzo/Comune 60, Descrizione riga 1000, CodiceValore 35, UnitaMisura 10, Causale in blocchi
  da 200 (fino a 800), IdDocumento 20, CIG/CUP 15, RiferimentoNormativo **100**.

### 4.3 Header — DatiTrasmissione

| campo | valore |
|---|---|
| `IdTrasmittente/IdPaese` | `IT` |
| `IdTrasmittente/IdCodice` | ⚠️ il **codice fiscale di chi spedisce davvero il file**, non la P.IVA (scarto **00300**). Su un canale multi-azienda è il CF del titolare del canale (§8) |
| `ProgressivoInvio` | il progressivo del file (§5.4) |
| `FormatoTrasmissione` | `FPR12` (o `FPA12`) |
| `CodiceDestinatario` | vedi tabella sotto |
| `PECDestinatario` | solo con `0000000` e se il cliente ha una PEC |

Il codice destinatario:

| situazione | `CodiceDestinatario` |
|---|---|
| cliente con codice destinatario | il suo codice, 7 caratteri, maiuscolo |
| cliente senza codice (privato, o ditta senza codice) | `0000000`, più la PEC se c'è |
| cliente **estero** (UE o extra-UE) | `XXXXXXX`, **sempre**. Forzarlo anche in anagrafica, non solo nell'XML |
| Pubblica Amministrazione | codice IPA di **6** caratteri (fuori ambito) |
| fattura intestata alla posizione **privata** di un titolare di P.IVA | `0000000`: il codice registrato è quello della ditta, e la fattura finirebbe nel canale sbagliato |
| autofattura (TD16–TD19, TD21, TD27, TD28) | il codice dell'azienda stessa, o `0000000` |

### 4.4 Header — CedentePrestatore e CessionarioCommittente

**Cedente** (chi vende):

- `IdFiscaleIVA` (`IT` + P.IVA a 11 cifre) **obbligatorio**; `CodiceFiscale` se presente.
- `Anagrafica/Denominazione`, oppure `Nome` + `Cognome`: ⚠️ **mai entrambi** (scarto **00200**). Un
  eventuale nome commerciale/insegna non va **mai** nell'XML.
- `RegimeFiscale`: `RF01` ordinario, `RF19` forfettario (gli altri, se servono, vanno studiati).
- `Sede`: Indirizzo, CAP (5 cifre), Comune, Provincia (sigla), Nazione.
- `Contatti` facoltativi: includerli **solo** se validi (telefono di 5–12 cifre, email ben formata),
  altrimenti l'XSD rifiuta il documento.

**Cessionario** (chi compra), per tipo di soggetto:

| tipo | IdFiscaleIVA | CodiceFiscale | Anagrafica | Sede |
|---|---|---|---|---|
| privato | — | obbligatorio | Nome + Cognome | italiana |
| ditta individuale / professionista | P.IVA | CF a 16 caratteri | Nome + Cognome | italiana |
| società | P.IVA | di solito = P.IVA | Denominazione | italiana |
| ente non commerciale | facoltativa | obbligatorio | Denominazione | italiana |
| estero | `IdPaese` del paese + identificativo, oppure `OO99999999999` se manca | — | come sopra | CAP `00000` se non è di 5 cifre, **senza** Provincia, `Nazione` ISO |

Almeno uno fra P.IVA e codice fiscale è obbligatorio per i soggetti italiani.

### 4.5 Body — DatiGenerali

- `TipoDocumento`: TD01 fattura, TD04 nota di credito, TD05 nota di debito, TD06 parcella, TD24/TD25
  fattura differita, più le autofatture (TD16–TD19, TD21, TD27, TD28). Il catalogo completo è nelle
  specifiche tecniche.
- `Divisa` `EUR`, `Data`, `Numero`, `ImportoTotaleDocumento`.
- **TD04**: `DatiFattureCollegate` con numero e data della fattura stornata, **obbligatori** per noi
  anche se lo SdI non li pretende. Il collegamento interno fra i due documenti tenerlo come **chiave
  esterna vera**: il campo dell'XML è testo libero.
- `DatiOrdineAcquisto` (con CIG/CUP) e `DatiDDT` (0..N: una differita può raggruppare più DDT)
  quando servono.
- `Causale` spezzata in blocchi da 200 caratteri.

**Il numero di fattura**: l'univocità sta in un vincolo di database
`(azienda, anno, sezionale, numero)`, non nella stringa. ManageSuite ha scelto `12` invece di
`12/2026`, perché l'anno è già la data del documento. Due accorgimenti utili:

- permettere di **scegliere il numero a mano** (un cliente che arriva da un altro gestionale alla
  23ª fattura deve poter partire dalla 23): il salto in avanti si accetta, il doppione no;
- ⚠️ se il numero si compone anche nel frontend, deve uscire **identico** a quello del backend. In
  ManageSuite lo componevano tre punti dell'editor, e cambiarne il formato solo nel backend faceva
  promettere un numero diverso da quello emesso.

### 4.6 Body — righe e riepiloghi: i calcoli che lo SdI controlla

Lo SdI ricalcola e scarta se i conti non tornano al centesimo. Le regole:

1. **Per riga**: `PrezzoTotale = Quantità × PrezzoUnitario − sconto`, arrotondato a 2 decimali.
   Lo sconto unitario in `ScontoMaggiorazione/Importo` va a **8 decimali** (controllo **00423**).
2. **Sconto di testata**: va **ripartito sulle righe** in proporzione all'importo, con il metodo
   dei **resti più grandi**: si assegnano i centesimi interi per difetto e quelli avanzati alle righe
   con il resto maggiore. Così la somma delle quote è esattamente lo sconto.
3. **Riepilogo per aliquota** (`DatiRiepilogo`): `ImponibileImporto = Σ PrezzoTotale` delle righe di
   quell'aliquota **+ contributo cassa** se ha quella aliquota (controllo **00422**).
4. **Imposta = ImponibileImporto × Aliquota / 100**, arrotondata a 2 decimali, calcolata **sul
   riepilogo e non sommando le righe** (controllo **00421**).
5. `ImportoTotaleDocumento = Σ imponibili + Σ imposte`, **sempre comprensivo di IVA**, anche in
   split payment.
6. `EsigibilitaIVA` (`I` immediata, `D` differita, `S` scissione dei pagamenti) solo sui riepiloghi
   con aliquota > 0; su quelli a 0% vanno `Natura` e `RiferimentoNormativo`.

Ripartizione con i resti più grandi, in C#:

```csharp
static decimal[] Ripartisci(decimal importo, IReadOnlyList<decimal> pesi)
{
    long centesimi = (long)Math.Round(importo * 100, MidpointRounding.AwayFromZero);
    decimal totale = pesi.Sum();
    var quote = new long[pesi.Count];
    var resti = new (int i, decimal r)[pesi.Count];
    if (totale == 0) return quote.Select(_ => 0m).ToArray();

    long assegnati = 0;
    for (int i = 0; i < pesi.Count; i++)
    {
        decimal esatto = centesimi * pesi[i] / totale;
        quote[i] = (long)Math.Floor(esatto);
        resti[i] = (i, esatto - quote[i]);
        assegnati += quote[i];
    }
    foreach (var (i, _) in resti.OrderByDescending(x => x.r).Take((int)(centesimi - assegnati)))
        quote[i]++;
    return quote.Select(c => c / 100m).ToArray();
}
```

⚠️ Usare sempre `decimal` e `MidpointRounding.AwayFromZero` (l'equivalente di `HALF_UP`). Il
default di .NET è l'arrotondamento bancario, che a volte dà un centesimo diverso da quello dello SdI.

### 4.7 Natura IVA e dicitura di legge

Ogni importo con aliquota 0% porta una **Natura** e, nel riepilogo, un `RiferimentoNormativo`
(massimo 100 caratteri). L'elenco completo:

| codice | significato |
|---|---|
| N1 | escluse ex art. 15 |
| N2.1 / N2.2 | non soggette: per carenza di territorialità / altri casi, **compreso il forfettario** |
| N3.1 … N3.6 | non imponibili: esportazioni, cessioni intra-UE, San Marino, assimilate, dichiarazione d'intento, altre |
| N4 | esenti |
| N5 | regime del margine |
| N6.1 … N6.9 | **inversione contabile** (reverse charge), per settore |
| N7 | IVA assolta in altro Stato UE |

Regole:

- **Forfettario (RF19)**: sempre `N2.2`, a prescindere dall'aliquota scelta, e **nessuna riga con
  IVA** su una vendita. Un forfettario non emette mai in N6.
- Tenere **un solo elenco** dei codici con la dicitura suggerita, usato sia dal backend sia dal
  frontend (ManageSuite lo espone con un endpoint): un secondo elenco diverge su un testo che finisce
  stampato in fattura.
- **Congelare Natura e dicitura sulla fattura** al momento dell'emissione. L'XML non si riscrive, ma
  il PDF di cortesia si rigenera a ogni scarico: se rileggesse la configurazione di oggi, una fattura
  di due anni fa direbbe una cosa diversa dal documento in archivio presso lo SdI.
- ManageSuite gestisce **una sola Natura per documento**. Va bene per il reverse charge (quelle
  fatture sono interamente in inversione), ma una fattura mista richiede riepiloghi per coppia
  (aliquota, natura).

**Controlli che lo SdI non fa** e che quindi tocca fare a noi:

- N6 + esigibilità `S`: reverse charge e split payment sono modi **opposti** di spostare l'IVA sul
  cliente, insieme non hanno senso;
- N6 verso un cliente **senza P.IVA**: un privato non può autoliquidare;
- N6 **senza dicitura**: l'annotazione «Inversione contabile» è obbligatoria (art. 21 c. 6-bis DPR
  633/72), e va stampata anche sul PDF.

### 4.8 Bollo virtuale

- È **dovuto** quando l'imponibile senza IVA supera **77,47 €** e la Natura è fra `N2.1`, `N2.2`,
  `N3.5`, `N3.6`, `N4`. Si dichiara con `DatiBollo/BolloVirtuale = SI` e `ImportoBollo = 2.00`.
- `DatiBollo` **dichiara** il bollo, non lo addebita: **non entra** nell'`ImportoTotaleDocumento`.
- Per farlo pagare al cliente serve una **riga vera** («Rivalsa imposta di bollo», 2,00 €). ⚠️ Quella
  riga prende **la Natura del compenso** (di norma **N2.2**), **non N1**: risposta a interpello
  428/2022, il bollo riaddebitato «assume natura di ricavo o compenso». ManageSuite all'inizio aveva
  messo N1, ed era sbagliato.
- La riga di rivalsa resta **fuori** dalla ripartizione dello sconto di testata, dalla base di
  ritenuta e cassa, e **dalla soglia dei 77,47 €**: altrimenti deciderebbe da sé di essere dovuta.
- Se il bollo sia dovuto lo decide **una funzione sola** del generatore: con la rivalsa, si genera,
  si aggiunge la riga e si genera di nuovo.

### 4.9 Ritenuta, cassa previdenziale, split payment

- **Ritenuta d'acconto** (`DatiRitenuta`): tipo RT01–RT06, aliquota, importo, causale CU (A…ZO).
  Base = imponibile delle righe (+ contributo cassa se soggetto a ritenuta). Ogni riga soggetta porta
  `Ritenuta = SI`. Nascosta per il forfettario, che non vi è soggetto.
- **Cassa previdenziale** (`DatiCassaPrevidenziale`): TC01–TC22, aliquota, importo = imponibile
  righe × aliquota, aliquota IVA del contributo (Natura se 0%). Il contributo **entra nel
  riepilogo** della sua aliquota IVA (controllo 00422).
- **Netto a pagare** = totale − ritenuta, e con split payment (`S`) **anche − IVA**.

### 4.10 Body — DatiPagamento

- `CondizioniPagamento`: TP01 a rate, TP02 completo, TP03 anticipo.
- Un `DettaglioPagamento` per rata: `ModalitaPagamento` MP01–MP23, `DataScadenzaPagamento`,
  `ImportoPagamento`, `IBAN` se bonifico. Rata unica senza importo = netto a pagare; con più rate,
  ognuna ha il suo importo.
- IBAN **scelto da un elenco di conti registrati**, non scritto a mano: un carattere sbagliato in
  fattura fa arrivare il pagamento altrove.
- Nelle autofatture `DatiPagamento` non c'è.

### 4.11 Validazione dei dati prima di generare

Un **validatore che raccoglie tutti i problemi** e blocca l'emissione con l'elenco completo, senza
ripieghi inventati. Il generatore che «indovina» un dato mancante produce fatture sbagliate che
passano lo SdI. Il minimo:

- **azienda**: P.IVA a 11 cifre, CF valido, indirizzo, comune, CAP a 5 cifre, provincia a 2 lettere,
  regime supportato;
- **cliente**: anagrafica coerente (nome+cognome **oppure** ragione sociale), P.IVA o CF, indirizzo,
  CAP e provincia italiani (non per gli esteri), nazione ISO diversa da `IT` per gli esteri,
  codice destinatario di 6–7 caratteri, PEC ben formata;
- **documento**: tipo valido, data, almeno una riga, TD04 con fattura collegata, almeno una rata,
  modalità MP valide, IBAN valido, importo su ogni rata se più di una;
- **IVA**: nessuna riga con IVA per un forfettario, Natura configurata e **unica** per gli importi a
  0%, i tre controlli N6 del §4.7;
- **data della P.IVA del cliente**: se il cliente ha aperto la P.IVA il 1° marzo, una fattura con
  P.IVA datata febbraio va bloccata (o intestata al privato).

La **bozza** resta sempre salvabile: il validatore scatta all'**emissione**.

### 4.12 Il PDF di cortesia

Il PDF non ha valore fiscale e deve dirlo. Su **ogni pagina**, non solo sull'ultima (di una copia
di cortesia capita di consegnare un foglio solo): *«Documento non valido ai fini fiscali ai sensi
ART.21 DPR633/72 l'originale è disponibile all'indirizzo PEC o codice SDI fornito, oppure nell'area
riservata dell'Agenzia delle Entrate»*. Con Natura N6 stampare «Inversione contabile - » davanti
alla dicitura. Le bozze escono marcate BOZZA.

---

## 5. Trasmissione: RiceviFile

### 5.1 Il contratto

| | valore |
|---|---|
| endpoint collaudo | `https://testservizi.fatturapa.it/ricevi_file` |
| endpoint produzione | `https://servizi.fatturapa.it/ricevi_file` |
| WSDL | `SdIRiceviFile_v1.0.wsdl` |
| namespace dei tipi | `http://www.fatturapa.gov.it/sdi/ws/trasmissione/v1.0/types` |
| SOAPAction | `"http://www.fatturapa.it/SdIRiceviFile/RiceviFile"` (con le virgolette) |
| Content-Type | `text/xml; charset=utf-8` (SOAP 1.1) |

Richiesta:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
  <soapenv:Body>
    <ns:fileSdIAccoglienza xmlns:ns="http://www.fatturapa.gov.it/sdi/ws/trasmissione/v1.0/types">
      <NomeFile>IT<CF>_00042.xml</NomeFile>
      <File>…base64 del file…</File>
    </ns:fileSdIAccoglienza>
  </soapenv:Body>
</soapenv:Envelope>
```

Risposta, dentro `rispostaSdIRiceviFile`: `IdentificativoSdI`, `DataOraRicezione` e, se il file non
è stato preso, `Errore`:

| codice | significato |
|---|---|
| `EI01` | file vuoto |
| `EI02` | servizio momentaneamente non disponibile: riprovare più tardi |
| `EI03` | utente non abilitato alla trasmissione: controllare l'accreditamento |

⚠️ **La risposta arriva in MTOM** (multipart XOP/MIME): il corpo comincia con un delimitatore `--…`
e la busta SOAP sta dentro la prima parte. Chi legge tutto il corpo come un XML unico non ci capisce
niente: è successo al primo invio vero di ManageSuite. Va estratta la busta prima di leggerla.

### 5.2 Il client con mutua autenticazione, in C#

⚠️ **Il certificato server di produzione NON è firmato dalla CA dell'Agenzia.** Misurato con
`openssl s_client -connect servizi.fatturapa.it:443`:

```
subject = O=SOGEI, CN=servizi.fatturapa.it
issuer  = O=Sectigo Limited, CN=Sectigo Public Server Authentication CA OV R36
```

È una CA pubblica commerciale. La CA dell'Agenzia firma i **nostri** certificati di canale e quelli
di collaudo. ManageSuite fidandosi *solo* della CA dell'Agenzia è morto al primo invio vero con
«PKIX path building failed». Bisogna fidarsi **delle CA di sistema oppure di quella dell'Agenzia**.
Non è un allentamento: la verifica del nome host resta, e la nostra identità la dimostra il
certificato client, l'unica cosa che lo SdI controlla di noi.

```csharp
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

public sealed record SdiRisposta(string? IdentificativoSdI, DateTimeOffset? DataOraRicezione, string? Errore)
{
    public bool Accettato => Errore is null && IdentificativoSdI is not null;
}

public sealed class SdiTransmissionClient
{
    const string NsTypes = "http://www.fatturapa.gov.it/sdi/ws/trasmissione/v1.0/types";
    const string SoapAction = "\"http://www.fatturapa.it/SdIRiceviFile/RiceviFile\"";

    readonly HttpClient _http;
    readonly Uri _endpoint;

    public bool AmbienteDiProva => _endpoint.Host.StartsWith("testservizi.", StringComparison.OrdinalIgnoreCase);

    public SdiTransmissionClient(SdiOptions opt)
    {
        _endpoint = new Uri(opt.Endpoint);

        // .NET 9: X509CertificateLoader.LoadPkcs12FromFile(...); prima: new X509Certificate2(path, pwd)
        var client = X509CertificateLoader.LoadPkcs12FromFile(opt.KeystorePath, opt.KeystorePassword);
        var agenzia = new X509Certificate2Collection();
        agenzia.ImportFromPemFile(opt.TruststorePath);          // caentrate + CAEntratetest

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(30),
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { client },
                RemoteCertificateValidationCallback = (_, cert, _, errori) => FidatoServer(cert, errori, agenzia),
            },
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };   // costruito una volta sola
    }

    /// CA di sistema (produzione: Sectigo) OPPURE CA dell'Agenzia (collaudo). Il nome host non si negozia.
    static bool FidatoServer(X509Certificate? cert, SslPolicyErrors errori, X509Certificate2Collection agenzia)
    {
        if (errori == SslPolicyErrors.None) return true;
        if (cert is null || errori != SslPolicyErrors.RemoteCertificateChainErrors) return false;

        using var catena = new X509Chain();
        catena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        catena.ChainPolicy.CustomTrustStore.AddRange(agenzia);
        catena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;   // come il default di Java
        return catena.Build(new X509Certificate2(cert));
    }

    public async Task<SdiRisposta> InviaAsync(string nomeFile, byte[] contenuto, CancellationToken ct = default)
    {
        string busta = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
              <soapenv:Body>
                <ns:fileSdIAccoglienza xmlns:ns="{NsTypes}">
                  <NomeFile>{System.Security.SecurityElement.Escape(nomeFile)}</NomeFile>
                  <File>{Convert.ToBase64String(contenuto)}</File>
                </ns:fileSdIAccoglienza>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(busta, Encoding.UTF8, "text/xml"),
        };
        req.Headers.TryAddWithoutValidation("SOAPAction", SoapAction);

        HttpResponseMessage resp;
        try { resp = await _http.SendAsync(req, ct); }
        catch (Exception e)
        {
            // Qui NON sappiamo se lo SdI abbia ricevuto: va detto, non nascosto
            throw new SdiException("SDI_UNREACHABLE",
                $"Non è stato possibile contattare lo SdI ({e.Message}). Controlla sul portale prima di riprovare: il file potrebbe essere partito.");
        }

        byte[] corpo = await resp.Content.ReadAsByteArrayAsync(ct);
        var root = Radice(EstraiBusta(corpo))
            ?? throw new SdiException("SDI_BAD_RESPONSE", $"Risposta SdI non interpretabile (HTTP {(int)resp.StatusCode}).");

        var fault = Trova(root, "Fault");
        if (fault is not null)
            throw new SdiException("SDI_FAULT", "Lo SdI ha risposto: " + (Testo(fault, "faultstring") ?? "errore SOAP"));

        var r = Trova(root, "rispostaSdIRiceviFile") ?? root;
        string? id = Testo(r, "IdentificativoSdI"), errore = Testo(r, "Errore");
        if (id is null && errore is null)
            throw new SdiException("SDI_BAD_RESPONSE", "Risposta SdI senza identificativo né errore.");
        return new SdiRisposta(id, DateTimeOffset.TryParse(Testo(r, "DataOraRicezione"), out var d) ? d : null, errore);
    }

    /// La risposta è MTOM: la busta sta nella prima parte. Se il corpo non comincia col delimitatore, si lascia com'è.
    static byte[] EstraiBusta(byte[] corpo)
    {
        string s = Encoding.UTF8.GetString(corpo);
        if (!s.StartsWith("--")) return corpo;
        int inizio = s.IndexOf("<?xml", StringComparison.Ordinal);
        if (inizio < 0)
        {
            int envelope = s.IndexOf("Envelope", StringComparison.Ordinal);   // <soapenv:Envelope, <S:Envelope…
            inizio = envelope < 0 ? -1 : s.LastIndexOf('<', envelope);
        }
        if (inizio < 0) return corpo;
        int fine = s.IndexOf("\r\n--", inizio, StringComparison.Ordinal);
        if (fine < 0) fine = s.IndexOf("\n--", inizio, StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(fine < 0 ? s[inizio..] : s[inizio..fine]);
    }

    // Lettura per NOME LOCALE: i prefissi li sceglie chi risponde, e non devono importarci
    internal static XElement? Radice(byte[] xml)
    {
        try
        {
            var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
            using var r = System.Xml.XmlReader.Create(new MemoryStream(xml), settings);
            return XDocument.Load(r).Root;
        }
        catch { return null; }
    }
    internal static XElement? Trova(XElement da, string nome) =>
        da.Descendants().FirstOrDefault(e => e.Name.LocalName == nome);
    internal static string? Testo(XElement da, string nome) =>
        Trova(da, nome)?.Value.Trim() is { Length: > 0 } t ? t : null;
}
```

Note:

- su Linux il `.p12` si apre senza problemi; se il certificato client avesse una CA intermedia,
  va inclusa nel `.p12`;
- **disabilitare la verifica del server** («accetta tutto») per far partire il primo invio è la
  scorciatoia da non prendere: il canale deve fallire se dall'altra parte non c'è lo SdI.

### 5.3 Cosa fare con la risposta

```
risposta = client.Invia(nomeFile, xml)

se risposta non accettata      → errore all'utente con il codice EI0x; la fattura resta «da inviare»
se l'endpoint è di COLLAUDO    → NON segnare la fattura come inviata (vedi sotto)
altrimenti                     → stato SENT, sdi_id = IdentificativoSdI, data invio = DataOraRicezione,
                                 riga di trasmissione aggiornata, audit «INVIO_SDI_CANALE»
```

- ⚠️ **In collaudo la fattura non si tocca.** Il file è partito davvero, ma verso uno SdI dove non ha
  valore. Segnarla inviata la farebbe sembrare trasmessa quando non è arrivata da nessuna parte, ed è
  l'errore che costa di più perché non si vede.
- ⚠️ **Una fattura spedita dal canale ha sempre l'`IdentificativoSdI`.** Quindi «inviata» con
  identificativo vuoto vuol dire **solo** che qualcuno l'ha registrata a mano. In ManageSuite due
  fatture sono rimaste un giorno «inviate» senza esserlo mai, perché nel menu c'era solo «Registra
  invio» (che annota un caricamento manuale) e il bottone che spedisce no (§13). Tracciare i due
  eventi con nomi diversi nell'audit (`INVOICE_SDI_SENT` = canale, `INVOICE_SDI_UPDATED` = a mano)
  è ciò che ha permesso la diagnosi.
- **Errore di rete = esito ignoto.** Il messaggio deve dire di controllare sul portale prima di
  riprovare. Rispedire subito un file forse già arrivato porta a un doppione.

### 5.4 Il nome del file e il progressivo

Formato: **`IT` + `<CF del trasmittente>` + `_` + `<progressivo>` + `.xml`** (o `.xml.p7m` per un
file firmato CAdES). Il prefisso **deve combaciare** con `IdTrasmittente` dentro l'XML.

Il progressivo è la parte che ha fatto più danni:

- ⚠️ **È un contatore vero, non l'id della fattura.** ManageSuite all'inizio usava
  `base36(invoice.id)`: fisso per fattura, quindi dopo uno scarto si poteva solo rimandare **lo
  stesso nome**, che lo SdI rifiuta a prescindere dall'esito precedente (scarto **00002**, «nome file
  duplicato»). Nessuna via d'uscita.
- **Si consuma a ogni file generato**: emissione **e** rigenerazione dopo uno scarto.
- **È del soggetto trasmittente, non del programma.** Il cassetto fiscale è uno solo: i numeri già
  usati da quel codice fiscale col portale AdE o col software del commercialista sono **già spesi**.
  Inizializzarlo **sopra** qualunque valore possa essere stato usato, perché i buchi non costano
  niente e un numero riusato è uno scarto.
- **Non torna mai indietro**, nemmeno a mano.
- **Lock in scrittura** sulla riga del contatore (`SELECT … FOR UPDATE` in Postgres,
  `WITH (UPDLOCK, ROWLOCK)` in SQL Server): due emissioni contemporanee prenderebbero lo stesso numero,
  e il doppione si scoprirebbe solo allo scarto. Su un canale multi-azienda la contesa è reale.
- Formato **decimale a 5 cifre** (`00042`): è un numero che l'utente deve poter confrontare col
  cassetto e dettare al telefono.
- ⚠️ **Limite da prevedere**: le specifiche limitano il progressivo nel **nome file** a 5 caratteri
  alfanumerici (il campo `ProgressivoInvio` dell'XML ne ammette 10), e `NomeFile` deve stare in
  `[a-zA-Z0-9_\.]{9,50}`. Con il decimale si arriva a 99.999 file. Su un canale che trasmette per
  molte aziende conviene prevedere da subito il passaggio a base 36 (`ZZZZZ` ≈ 60 milioni), senza mai
  riusare un valore. Verificare il limite sulle specifiche tecniche correnti.

```sql
-- Postgres; una riga sola per il canale
UPDATE sdi_transmitter SET last_progressivo = last_progressivo + 1
WHERE id = 1
RETURNING last_progressivo;          -- l'UPDATE prende già il lock di riga
```

**Rifare l'XML dopo uno scarto**: si rigenera il file con i dati corretti **tenendo numero e data**
(una fattura scartata, per lo SdI, non è mai esistita: il numero non è bruciato e non serve una nota
di credito) e con un **progressivo nuovo**. Consentito solo su fatture emesse e non trasmesse, o
scartate. Una fattura **consegnata** non si riscrive mai: si storna con una TD04.

---

## 6. Le ricevute: il servizio TrasmissioneFatture

### 6.1 Il contratto

Lo esponiamo noi su `/sdi/TrasmissioneFatture` (e `/sdi/test/…`). WSDL
`TrasmissioneFatture_v1.1.wsdl`, namespace dei tipi
`http://www.fatturapa.gov.it/sdi/ws/trasmissione/v1.0/types`, SOAPAction
`http://www.fatturapa.it/TrasmissioneFatture/<Operazione>`.

Sei operazioni, **tutte con lo stesso corpo** (`IdentificativoSdI`, `NomeFile`, `File` in base64) e
**nessuna risposta SOAP**:

| elemento radice del corpo | contenuto |
|---|---|
| `ricevutaConsegna` | RC — consegnata al destinatario |
| `notificaMancataConsegna` | MC — non recapitabile, messa a disposizione nel cassetto |
| `notificaScarto` | NS — scartata |
| `notificaEsito` | NE — esito del committente (solo PA) |
| `notificaDecorrenzaTermini` | DT — decorrenza termini (solo PA) |
| `attestazioneTrasmissioneFattura` | AT — attestazione di avvenuta trasmissione con impossibilità di recapito (solo PA) |

Si risponde con l'HTTP e basta. ManageSuite risponde **202** (comportamento di Spring WS per le
operazioni one-way) e il collaudo è passato; un **200** con corpo vuoto è ugualmente corretto per
SOAP 1.1.

### 6.2 Regole del servizio

- ⚠️ **Mai un SOAP Fault, mai un 500.** Un errore viene letto come «riprova», e lo SdI ritenterebbe
  la consegna della stessa ricevuta all'infinito. Qualunque problema si scrive nel log e si
  risponde comunque.
- **Il `File` va decodificato con un decoder tollerante agli a capo**: i client SOAP spezzano il
  base64 su più righe. `Convert.FromBase64String` ignora gli spazi bianchi, quindi va bene.
- **Si conserva sempre il file grezzo**, anche quando non si riesce a leggerlo, agganciandolo alla
  trasmissione per convenzione del nome (`<nomefile>_<TIPO>_<n>.xml`) e scrivendo nel log i primi
  byte. Dal canale una ricevuta **non si riscarica**.

### 6.3 Leggere la ricevuta

Dentro `File` c'è un XML firmato XAdES. Leggerlo **per nome locale degli elementi**, non con classi
generate dall'XSD: i tipi sono sei con campi diversi, c'è un blocco `<ds:Signature>` dentro (non
entrarci quando si cerca un elemento), e legarsi allo schema significa rompersi al primo campo
inatteso.

⚠️ **Il tipo si riconosce dall'elemento radice, e i nomi dello SdI non sono regolari.** Lo stesso
messaggio si chiama *Ricevuta…* da una parte e *Notifica…* dall'altra, e non sempre nello stesso
verso: la mancata consegna è `RicevutaImpossibilitaRecapito` nel file scaricato dal portale e
`NotificaMancataConsegna` sul canale, mentre lo scarto è `NotificaScarto` dal portale e
**`RicevutaScarto` dal canale**. Una lista di nomi ha fatto ignorare in silenzio uno scarto vero. Si
**toglie il prefisso** e si guarda il resto:

```csharp
static TipoRicevuta TipoDa(string radice)
{
    string c = radice.StartsWith("Ricevuta") ? radice["Ricevuta".Length..]
             : radice.StartsWith("Notifica") ? radice["Notifica".Length..]
             : radice;
    return c switch
    {
        "Consegna"                                  => TipoRicevuta.RC,
        "ImpossibilitaRecapito" or "MancataConsegna" => TipoRicevuta.MC,
        "Scarto"                                    => TipoRicevuta.NS,
        "DecorrenzaTermini"                         => TipoRicevuta.DT,
        "AttestazioneTrasmissioneFattura"           => TipoRicevuta.AT,
        "Esito"                                     => TipoRicevuta.NE,
        _                                           => TipoRicevuta.Sconosciuta,
    };
}
```

Campi utili: `IdentificativoSdI`, `NomeFile` (l'aggancio alla fattura), `DataOraRicezione`,
`DataOraConsegna`, `DataMessaADisposizione` (MC), `MessageId`, `Note`, `Hash`, e per lo scarto
`ListaErrori/Errore` con `Codice`, `Descrizione`, `Suggerimento`.

- La **`Descrizione` della ricevuta** è figlia diretta della radice. Ce n'è un'altra dentro
  `<Destinatario>` che descrive il canale di recapito: presa a caso, finisce sulla fattura al posto
  dell'esito. Fa eccezione la notifica di esito, dove sta dentro `<EsitoCommittente>`.
- Le ricevute **scaricate dal portale** hanno spesso il testo codificato due volte («Ã¨» al posto di
  «è»). Si ripara a coppie: un carattere `0xC3` o `0xC2` seguito da uno fra `0x80` e `0xBF` è una
  sequenza UTF-8 letta come Latin-1. Se il testo non contiene coppie del genere, si lascia com'è.

### 6.4 Dalla ricevuta allo stato della fattura

| ricevuta | stato della fattura | note |
|---|---|---|
| RC | **consegnata** | |
| MC, AT | **non consegnata** | ⚠️ è una fattura **valida**: depositata nel cassetto fiscale del cliente. Colore ambra, non rosso. Basta dare al cliente la copia di cortesia |
| NS | **scartata** | l'unico vero rifiuto: si corregge e si **rifà l'XML** (progressivo nuovo, stesso numero) |
| DT, NE | invariato | arrivano dopo la consegna e non la smentiscono: si annotano |

Come si aggancia la ricevuta: **prima per `NomeFile`**, che è il dato preciso; se non basta, per
`IdentificativoSdI` (utile su una trasmissione registrata a mano). Per questo serve una tabella
**`invoice_transmissions` con una riga per ogni file generato**: il nome file cambia a ogni
rigenerazione, e senza storico una ricevuta in ritardo sul file vecchio non troverebbe più la
fattura. La fattura riassume l'ultima trasmissione (stato, identificativo, date, nota), lo storico
le tiene tutte.

Su multi-azienda: dal canale non c'è nessun utente collegato, quindi l'azienda è **quella della
fattura trovata**, non quella di un contesto di sessione. A ogni ricevuta, un **avviso in-app**
all'azienda: uno scarto è un avviso di livello warning.

### 6.5 Ricevute di collaudo

Sotto `/sdi/test/` la ricevuta si **conserva e si mostra** (su `invoice_transmissions` con un flag
`prova`), ma **non si applica**: stato, identificativo e date della fattura restano intatti.
Inizialmente ManageSuite si limitava a scriverle nel log, e durante il collaudo non si vedevano da
nessuna parte.

### 6.6 Il server SOAP in ASP.NET Core

Non serve un framework SOAP (CoreWCF): i messaggi sono pochi e piccoli, e leggerli dal corpo per
nome locale è più robusto di un binding sul contratto.

```csharp
// Program.cs — Kestrel solo su 127.0.0.1
app.UseMiddleware<SdiVerificaCertificato>();        // §7.3: controlla X-SdI-Client-Verified su /sdi/*

app.MapPost("/sdi/TrasmissioneFatture",      (HttpContext c, SdiRicevuteHandler h) => h.GestisciAsync(c, prova: false));
app.MapPost("/sdi/test/TrasmissioneFatture", (HttpContext c, SdiRicevuteHandler h) => h.GestisciAsync(c, prova: true));
app.MapPost("/sdi/RicezioneFatture",         (HttpContext c, SdiRicezioneHandler h) => h.GestisciAsync(c, prova: false));
app.MapPost("/sdi/test/RicezioneFatture",    (HttpContext c, SdiRicezioneHandler h) => h.GestisciAsync(c, prova: true));
app.MapMethods("/sdi/stato", new[] { "GET", "POST" }, (HttpContext c) =>
    Results.Text(c.Request.Headers["X-SdI-Client-Verified"] == "SUCCESS" ? "OK" : "NON VERIFICATO"));

public sealed class SdiRicevuteHandler(ILogger<SdiRicevuteHandler> log, SdiReceiptService ricevute)
{
    public async Task<IResult> GestisciAsync(HttpContext ctx, bool prova)
    {
        try
        {
            using var ms = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(ms);
            var busta = SdiTransmissionClient.Radice(ms.ToArray());
            var corpo = busta?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Body")?.Elements().FirstOrDefault();
            if (corpo is null) { log.LogWarning("Chiamata SdI senza corpo leggibile"); return Results.StatusCode(202); }

            string operazione = corpo.Name.LocalName;                  // ricevutaConsegna, notificaScarto…
            string? nomeFile = Figlio(corpo, "NomeFile");
            string? id       = Figlio(corpo, "IdentificativoSdI");
            byte[]? file     = Figlio(corpo, "File") is { } b64 ? Convert.FromBase64String(b64) : null;

            if (file is { Length: > 0 })
            {
                if (prova) await ricevute.AnnotaProvaAsync(nomeFile, file);   // conserva, non applica
                else       await ricevute.ImportaDalCanaleAsync(nomeFile, file);
            }
            log.LogInformation("{Prova}{Op} — id {Id}, file {File}", prova ? "[TEST SdI] " : "", operazione, id, nomeFile);
        }
        catch (Exception e)
        {
            log.LogError(e, "Ricevuta SdI non elaborata");    // MAI un Fault verso lo SdI
        }
        return Results.StatusCode(202);
    }

    static string? Figlio(XElement p, string nome) =>
        p.Elements().FirstOrDefault(e => e.Name.LocalName == nome)?.Value.Trim() is { Length: > 0 } t ? t : null;
}
```

---

## 7. Ricezione delle fatture dei fornitori: RicezioneFatture

### 7.1 Perché questa strada

È il modo di far arrivare **da sole** le fatture di acquisto dei clienti del software, e funziona
per tutti allo stesso modo: **nessuna firma qualificata, nessuna delega**. Il cliente registra il
codice destinatario del canale come proprio **indirizzo telematico** su Fatture e Corrispettivi
(un minuto, lo fa da sé) e da quel momento le fatture dei suoi fornitori arrivano nel momento in cui
vengono emesse. L'indirizzo telematico registrato vince su quello che il fornitore scrive in
fattura.

Cosa non dà: lo **storico** (arriva solo da qui in avanti: per il passato serve lo scarico massivo,
che vuole la firma, §15) e la **conservazione a norma**, che resta il servizio gratuito
dell'Agenzia.

### 7.2 Il contratto, misurato e non dedotto

WSDL `RicezioneFatture_v1.0.wsdl`, tipi `RicezioneTypes_v1.0.xsd`. Come parla davvero lo SdI
(misurato in collaudo, prima erano tutte ipotesi):

```
Content-Type: text/xml                       (SOAP 1.1)
SOAPAction:   "http://www.fatturapa.it/RicezioneFatture/RiceviFattureSdI"
```

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
  <soapenv:Body>
    <ns2:fileSdIConMetadati xmlns:ns2="http://www.fatturapa.gov.it/sdi/ws/ricezione/v1.0/types">
      <IdentificativoSdI>32667922</IdentificativoSdI>
      <NomeFile>IT01234567890_abc12.xml.p7m</NomeFile>
      <File>…base64…</File>
      <NomeFileMetadati>IT01234567890_abc12_MT_001.xml</NomeFileMetadati>
      <Metadati>…base64…</Metadati>
    </ns2:fileSdIConMetadati>
  </soapenv:Body>
</soapenv:Envelope>
```

- Il servizio sta su `www.fatturapa.it`, non `fatturapa.gov.it`, e l'operazione si chiama
  `RiceviFattureSdI`.
- ⚠️ **`RicezioneTypes_v1.0.xsd` non dichiara `elementFormDefault`**, quindi vale `unqualified`: la
  radice (`fileSdIConMetadati`, `rispostaRiceviFatture`) è qualificata, **i figli no**.
- `IdentificativoSdI` è un intero di al massimo 12 cifre; `NomeFile` sta in
  `[a-zA-Z0-9_\.]{9,50}`.
- Seconda operazione: `NotificaDecorrenzaTermini` (radice `fileSdI`), **solo fatture PA**: si
  accetta in silenzio, senza risposta, senza Fault.

### 7.3 La risposta: `ER01`, e quando NON darla

La risposta di presa in carico:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
  <soapenv:Body>
    <rispostaRiceviFatture xmlns="http://www.fatturapa.gov.it/sdi/ws/ricezione/v1.0/types">
      <Esito xmlns="">ER01</Esito>
    </rispostaRiceviFatture>
  </soapenv:Body>
</soapenv:Envelope>
```

⚠️ **Quell'`xmlns=""` è il collaudo del 21/09/2026.** Mettere anche `Esito` nel namespace della radice
produce un XML che sembra giusto e che lo SdI legge come esito **nullo**: il portale segna
«Errore durante l'invio di un messaggio tramite WS - **Err: NULL**», e quel NULL va letto alla
lettera. Dentro una radice con namespace predefinito, un figlio «senza namespace» va dichiarato con
`xmlns=""` esplicito.

Regola generale: **si rispecchia la forma della richiesta**, radice *e* figli, invece di dedurla.
Lo stesso «Err: NULL» era comparso una prima volta per un motivo diverso: l'endpoint instradava i
messaggi per *namespace + nome* e il namespace era stato indovinato male, quindi lo SdI riceveva in
pratica un 404. Instradare per **solo nome locale** dell'elemento radice e scrivere nel log il
namespace vero alla prima chiamata.

In C#, con LINQ to XML, un figlio creato senza namespace dentro una radice con namespace predefinito
viene serializzato con `xmlns=""`. **Verificarlo con un test** sull'output esatto:

```csharp
static string RispostaEr01(XElement richiesta)
{
    XNamespace nsRadice = richiesta.Name.Namespace;
    // i figli della richiesta dicono come il contratto qualifica gli elementi interni (di norma: nessun namespace)
    XNamespace nsFigli = richiesta.Elements().FirstOrDefault()?.Name.Namespace ?? nsRadice;
    XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";

    var busta = new XElement(soap + "Envelope",
        new XAttribute(XNamespace.Xmlns + "soapenv", soap),
        new XElement(soap + "Body",
            new XElement(nsRadice + "rispostaRiceviFatture",
                new XElement(nsFigli + "Esito", "ER01"))));
    return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + busta.ToString(SaveOptions.DisableFormatting);
}

// [Fact] l'output deve contenere: <Esito xmlns="">ER01</Esito>
```

**Quando NON rispondere `ER01`** (cioè: nessuna risposta, o un errore HTTP):

1. ⚠️ **Certificato client non verificato.** È il contrario di quel che si fa con le ricevute: là
   ignorare una chiamata non autenticata è innocuo, qui la presa in carico è **definitiva**. Detto
   `ER01`, per lo SdI la fattura è consegnata e non la rimanda più: rispondere e poi scartare il file
   perderebbe un documento vero in silenzio. Non rispondendo, lo SdI ritenta e il portale segna
   l'errore, cioè un guasto che **si vede**. Conseguenza pratica: una `curl` da `127.0.0.1` non
   ottiene risposta, perché non passa da Apache; per provare va aggiunta a mano l'intestazione
   `X-SdI-Client-Verified: SUCCESS`.
2. **La copia su disco non è riuscita** (§7.4).

In tutti gli altri casi, **anche se poi la lettura fallisce**, si risponde `ER01`: il file è
arrivato ed è stato conservato, il guasto è nostro e si risolve da noi.

### 7.4 L'ordine: disco, database, ER01

```
1. consegna già vista? (stesso IdentificativoSdI + stesso ambiente)  → ER01 senza rifare niente
2. copia su DISCO, fuori dal database (fattura + metadati + indice .json)
      └─ se fallisce → NIENTE ER01: lo SdI ritenterà
3. riga in sdi_received_files con i BYTE ORIGINALI
4. estrazione dell'XML (anche da .p7m e .zip), lettura, smistamento all'azienda
5. registrazione fra le fatture di acquisto + avviso in-app all'azienda
6. ER01
```

- **Registrare prima, capire poi.** Ogni consegna lascia una riga con i byte originali, anche
  quando non si legge o non si sa di chi sia. La fattura di acquisto nasce *da* quella riga.
- **La copia su disco** sta su un volume dedicato che il backup notturno include. Il database e il
  disco non sono un doppione: dal database l'applicazione serve i file, dal disco si ripristina con un
  `tar` **anche se l'applicazione non parte**. Nome del file costruito sull'`IdentificativoSdI`
  (la consegna ritentata riscrive lo stesso file invece di moltiplicarlo); scrittura su un file
  temporaneo, `Flush(true)` e **rinomina atomica**, perché un file mezzo scritto è peggio di un file
  assente: sembra a posto.
- **Consegne ripetute**: lo SdI ritenta se non riceve la risposta in tempo. Vincolo unico su
  `(identificativo_sdi, prova)`: la seconda consegna non produce una seconda fattura.
- **Collaudo**: sotto `/sdi/test/` la fattura si conserva e si mostra, ma **non** diventa un acquisto
  vero (sono documenti inventati). Tenere un pulsante «copia nel registro dell'azienda X» per vedere
  come apparirà.

### 7.5 Leggere le fatture ricevute

- **`.xml.p7m` (CAdES)**: il contenuto firmato sta **in chiaro e verbatim** dentro l'involucro DER.
  Basta ritagliare dal tag di apertura `<…FatturaElettronica` all'ultimo tag di chiusura, senza
  librerie crittografiche: la firma l'ha già verificata lo SdI. Tollerare anche i `.p7m` in base64
  («armored»). Gli `.zip` (scarichi dal portale) si aprono ed elaborano file per file.
- **Lettura per nome locale**, come le ricevute: questi file li scrive chiunque, in versioni diverse
  del tracciato e spesso firmati XAdES. Le classi generate dall'XSD usate per *emettere* farebbero
  fallire l'import al primo campo inatteso.
- ⚠️ **`FatturaElettronicaBody` è ripetibile** (fatturazione differita: un header, N documenti).
  Si legge una **lista**, e l'esito dell'import è per documento, non per file.
- **Importi mai ricalcolati**: imponibile e imposta sono quelli del fornitore, ed è su quelli che ha
  liquidato l'IVA.
- **Fornitore riconosciuto per numero fiscale, mai per nome**; creato se manca.
- **Doppioni**: per impronta del contenuto *e* per (P.IVA fornitore, numero, data). ⚠️ L'impronta
  deve includere il numero del documento, altrimenti la seconda fattura di un file multiplo risulta
  già importata per colpa della prima.
- Le fatture di acquisto **non caricano il magazzino** (se il software ne ha uno): la merce si carica
  con i documenti di carico, la fattura dice quanto si paga. Consegne parziali e fatture
  riepilogative rendono le due cose quasi mai coincidenti.
- **Ritenuta del fornitore**: da pagare al fornitore è `totale − ritenuta` (la ritenuta va
  all'Erario). Un solo metodo `NettoAPagare()` usato ovunque si calcoli un residuo: in ManageSuite
  cinque punti lo calcolavano ognuno per conto suo, ed erano divergiti.

---

## 8. Più aziende sullo stesso canale

### 8.1 In trasmissione: chi è il trasmittente

Il trasmittente è **chi spedisce davvero il file**, e da lui dipendono `IdTrasmittente`, prefisso
del nome file e contatore. Per ogni azienda cliente:

| azienda | IdTrasmittente e nome file | contatore |
|---|---|---|
| **sul canale** (spunta «trasmette la piattaforma») | CF del titolare del canale (`<CF>`, da configurazione) | **unico** del canale, riga singola con lock |
| che scarica l'XML e lo carica dal proprio portale | il **suo** CF | il **suo** |

- È una scelta **per azienda**, non un interruttore globale: le due situazioni convivono.
- Il CF della piattaforma sta nella **configurazione** (`SDI_TRASMITTENTE_CF`), non in anagrafica:
  è l'identità del canale, come i certificati.
- ⚠️ **Il contatore segue il soggetto, non la spunta.** Il titolare del canale è quasi sempre anche
  un'azienda cliente del software. Se il contatore dipende dalla spunta, quell'azienda ha **due
  contatori sullo stesso codice fiscale**: togliendo la spunta riparte da numeri già spesi, e ogni
  fattura torna indietro con lo scarto 00002. La regola: si usa il contatore del canale **se
  l'IdTrasmittente coincide con il CF del canale**, a prescindere dalla spunta.
- ⚠️ **Un'azienda messa sul canale dopo aver emesso** ha in archivio XML che dichiarano lei come
  trasmittente. Prima di spedire, controllare che esista una riga di trasmissione con il nome che il
  file avrebbe *oggi*. Se non c'è, chiedere di **rifare l'XML** invece di spedire un file il cui nome
  smentisce il contenuto.
- Se il CF della piattaforma non è configurato, un'azienda sul canale **non può emettere**: non si
  ripiega sul suo CF, perché il file uscirebbe dal canale dichiarando un trasmittente diverso da chi
  lo spedisce.

### 8.2 In ricezione: lo smistamento

Un solo `<CODICE_DEST>` per tutti i clienti. A smistare è il **documento**, non chi chiama:

1. dall'XML si legge l'identificativo del **cessionario** (P.IVA, altrimenti CF);
2. si cerca l'azienda con quell'identificativo, **normalizzando anche la colonna** (in anagrafica
   le P.IVA sono scritte a mano: con `IT`, punti, spazi);
3. **una sola** corrispondenza → si registra lì; **nessuna o più di una** → **quarantena**.

Nella quarantena (cliente appena entrato, dato scritto male, codice rimasto a chi non è più
cliente) la fattura **non si perde e non si butta**: resta in un pannello della piattaforma, con il
numero delle fatture in attesa ben visibile, e si attribuisce a mano. Con due corrispondenze non si
indovina: attribuire la fattura all'azienda sbagliata la mostrerebbe a chi non deve vederla.
L'attribuzione manuale **non ripete** il controllo sull'intestatario, che è proprio quello già
fallito.

Un codice per cliente non aggiungerebbe sicurezza e moltiplicherebbe le occasioni di comunicarne uno
sbagliato. Va mostrato nel pannello con un pulsante per **copiarlo** e uno per copiare le
**istruzioni da girare al cliente**: un carattere sbagliato manda le sue fatture su un altro canale.

Nel log del servizio non c'è nessun utente collegato, quindi nessun contesto di tenant: tutto ciò
che si scrive va attribuito esplicitamente all'azienda trovata.

---

## 9. Test di interoperabilità

Dal portale di accreditamento, sezione *Avanzamento*. Il piano è il PDF del kit
(`piano_test_interoperabilita_SDICoop.pdf`).

### 9.1 Trasmissione: tre esiti obbligatori

| esito da ottenere | come provocarlo |
|---|---|
| **ricevuta di scarto** | un dato invalido nell'XML: basta un `CodiceDestinatario` malformato |
| **ricevuta di consegna** | uno dei codici destinatario B2B fittizi della scheda *Codici Destinatario* del portale |
| **impossibilità di recapito** | `CodiceDestinatario 0000000` senza PEC: arriva in circa 15 secondi. Il canale inesistente `XS00001` funziona ma ci mette circa 20 minuti |

I tre *Ulteriori test per fatturaPA* (esito, decorrenza termini, attestazione) sono **facoltativi**
e riguardano la PA: lasciarli rossi **non impedisce** il passaggio in produzione.

⚠️ **Non emettere fatture vere sbagliate apposta**: ognuna brucerebbe un numero del registro fiscale
che non si potrebbe più cancellare. Serve un endpoint che manda allo SdI un file preparato a mano
**senza registrare nulla**, consentito **solo** verso l'endpoint di collaudo (verso la produzione
diventerebbe un modo per emettere documenti scavalcando ogni controllo), solo per amministratori. Il
progressivo lo consuma lo stesso.

Le ricevute di questi test arrivano su `/sdi/test/TrasmissioneFatture`: devono comparire nel
programma (§6.5), altrimenti non si sa se il giro è completo.

### 9.2 Ricezione: un test

**WSR01**, «Ricezione di un file Fattura»: si provoca dal *Simulatore invio* del portale scegliendo
un codice destinatario fittizio della sezione *Codici Destinatario*. Atteso: consegna e ricevuta di
consegna verdi. WSR02–WSR05 sono B2G e non si fanno.

---

## 10. Passaggio in produzione

1. Dal portale: richiesta di passaggio in produzione, dichiarando gli **endpoint senza `/test/`**.
   L'abilitazione arriva con una **PEC** al Sottoscrittore.
2. **Codice destinatario B2B**: *Ambiti → Sistema d'Interscambio* → menu **Operazione → Codici
   destinatari B2B**. Viene generato sul momento (fino a 300 per canale). Non arriva da solo.
3. L'interruttore lato software è **uno solo**: l'endpoint di invio nel `.env`, da
   `https://testservizi.fatturapa.it/ricevi_file` a `https://servizi.fatturapa.it/ricevi_file`.
   Finché contiene `testservizi.`, le fatture spedite non vengono segnate come inviate (§5.3).
4. ⚠️ Prima della prima fattura vera, **controllare il contatore** contro i progressivi già usati
   da quel codice fiscale (portale, software del commercialista, collaudi). Nel dubbio, alzarlo.
5. ⚠️ **La prima fattura vera è un collaudo a sé.** Va fatta su una fattura che si può permettere di
   sbagliare. In ManageSuite il primo invio reale ha rivelato il truststore (§5.2), la risposta MTOM
   (§5.1) e il fuso orario: codice che esisteva da settimane e non era mai stato esercitato. **Un
   pezzo mai eseguito non è funzionante, è solo non ancora provato.**
6. ⚠️ **Mai fatture di prova su un'azienda vera.** In ManageSuite le prove fatte in produzione ad
   agosto avevano consumato nove numeri del registro, che poi andavano ripuliti a mano. Meglio
   un'azienda marcata «di prova» che non consuma la numerazione.
7. Per la ricezione, ogni azienda cliente registra `<CODICE_DEST>` come indirizzo telematico su
   Fatture e Corrispettivi. Avvisare chi usava un altro indirizzo (per esempio il commercialista),
   perché le fatture smetteranno di arrivare a lui.

---

## 11. Sorveglianza del canale

Da quando un cliente registra il codice destinatario, un guasto silenzioso del canale diventa **una
sua fattura che non arriva**, e nessuno ha motivo di segnalarlo: il fornitore vede «consegnata», il
cliente non sa cosa aspettarsi. Due lavori distinti: l'**archivio** evita di *perdere*, la
**sorveglianza** evita di *non accorgersi*.

| controllo | frequenza | perché |
|---|---|---|
| **sonda da fuori**: chiamata a `https://<HOST_SDI>/sdi/stato` con il nostro certificato client | ogni 15 minuti | DNS, TLS, Apache e l'intestazione di verifica **da dentro non si vedono**, ed è la catena che si è rotta di più |
| **scadenza del certificato** client (e server) | ogni giorno, avvisi a 60/30/7 giorni | l'unico guasto prevenibile, e il più muto: il collegamento smette di aprirsi senza che nessuno abbia toccato niente |
| **spazio libero** sul disco dell'archivio | ogni giorno | il VPS può ospitare altro, il disco pieno può arrivare da fuori |
| **consegne irrisolte** (quarantena, illeggibili) | ogni giorno | |
| **riconciliazione** registro ↔ archivio su disco (30 giorni) | ogni notte | un file mancante **si ricostruisce** dai byte nel database; allarme solo se mancano anche quelli |

- La sonda risponde solo «la verifica del certificato è arrivata sì/no»: ce la fa un endpoint
  minimale, non i servizi veri.
- Avvisi via **email**, **uno per tipo al giorno**, con lo stato salvato in database (in memoria
  ripartirebbe a ogni deploy), e **un'email anche al rientro**: senza, chi ha ricevuto «il canale non
  risponde» dovrebbe controllare a mano proprio ciò che la sorveglianza esiste per non far
  controllare.
- Tutto visibile in un pannello della piattaforma, **anche quando è tutto verde**, con un pulsante
  «Manda un avviso di prova» **sincrono e che non ingoia l'errore**.

⚠️ **La posta.** In ManageSuite le email non sono mai partite per settimane senza nessun errore: un
blocco di configurazione indentato sotto la chiave sbagliata lasciava il mittente non configurato,
e ogni invio aveva un ramo «posta non configurata» che scriveva nel log e tornava. Un ripiego
silenzioso su una funzione che nessuno verifica equivale a non avere la funzione. Se le email partono
dal VPS:

- il nome host SMTP deve combaciare con il certificato TLS del server di posta;
- **SPF** con l'IPv4 **e** l'IPv6 da cui Postfix spedisce (oppure Postfix solo IPv4: via IPv6 Gmail
  pretende anche il PTR), **DKIM** pubblicato nel DNS del provider, **DMARC** almeno `p=none`.
  Senza, Gmail rifiuta con `550-5.7.26 unauthenticated`;
- ⚠️ mai `curl -v` su SMTP con credenziali: stampa l'`AUTH PLAIN` in base64, cioè la password.

---

## 12. Modello dati

Indicativo, da adattare al database del software (nomi in stile ManageSuite).

```sql
-- Fattura: i campi della trasmissione (oltre a righe, rate, riepiloghi…)
invoices (
  id, company_id, document_type, numbering_series,     -- VEN / AUT (autofatture: sezionale separato)
  year, invoice_number, full_number, document_date,
  status,              -- DRAFT, GENERATED, SENT, DELIVERED, NOT_DELIVERED, REJECTED
  xml, progressivo_invio,
  sdi_id, sdi_sent_at, sdi_outcome_at, sdi_note,       -- riassunto dell'ULTIMA trasmissione
  natura, riferimento_normativo,                       -- congelati all'emissione
  requested_number,                                    -- numero chiesto a mano, consumato all'emissione
  UNIQUE (company_id, year, numbering_series, invoice_number)
)

-- Una riga per ogni FILE generato: la ricevuta si aggancia al nome del file
invoice_transmissions (
  id, invoice_id, file_name UNIQUE, created_at,
  sdi_id, sent_at, outcome, outcome_at, note,
  receipt_type, receipt_name, receipt_xml, received_at,
  receipt_prova BOOLEAN                                -- ricevuta di collaudo: conservata, non applicata
)

-- Contatore del canale: una riga sola, presa con lock
sdi_transmitter ( id = 1, last_progressivo_invio )
-- e per le aziende che trasmettono per sé:
companies ( …, fiscal_code, vat_number, sdi_intermediato BOOLEAN, last_progressivo_invio )

-- Ogni consegna ricevuta, prima di capirla
sdi_received_files (
  id, identificativo_sdi, prova BOOLEAN, file_name, raw_file BYTEA,
  metadata_file_name, metadata_xml, xml,
  mittente, destinatario_id, company_id NULL,
  esito,               -- IMPORTATA, DUPLICATA, SENZA_DESTINATARIO, NON_LEGGIBILE, PROVA
  messaggio, purchase_invoice_id NULL, received_at,
  UNIQUE (identificativo_sdi, prova)
)

-- Fatture di acquisto: tabelle PROPRIE, non colonne in più su invoices
purchase_invoices ( id, company_id, supplier_id, number /* testo del fornitore */, date,
                    total, withholding_amount, xml, hash, … )

-- Stato degli avvisi di sorveglianza (uno per tipo al giorno, e il rientro)
sdi_monitor_state ( tipo PRIMARY KEY, in_errore BOOLEAN, ultimo_avviso_at, dettaglio )
```

Le fatture di acquisto hanno tabelle **proprie** perché il numero è del fornitore ed è testo libero,
non c'è una numerazione nostra né uno stato SdI da governare. Mescolarle costringerebbe ogni query del
registro di vendita a ricordarsi di escluderle.

⚠️ Due trappole di Postgres/ORM viste in ManageSuite: una colonna `CHAR(n)` diventa `bpchar` e un ORM
che valida lo schema all'avvio si rifiuta di partire (usare `VARCHAR`), e in una query nativa scritta
attraverso un ORM `::int` può arrivare al database come parametro `:int` (usare `CAST(… AS INTEGER)`).

---

## 13. Frontend React: cosa mostrare e cosa evitare

- ⚠️ **Due azioni diverse non devono somigliarsi.** «Invia allo SdI» (spedisce dal canale) e
  «Caricata a mano sul portale…» (annota un invio fatto fuori dal programma) vanno con nomi e icone
  distinti. Il modale della registrazione manuale, se il canale è attivo, apre con un **avviso rosso**
  e un bottone «Registra senza spedire». L'incidente del §5.3 è nato da qui.
- Il bottone di invio compare solo se **canale acceso, in produzione e azienda sul canale**. Le tre
  condizioni le dice il backend in un endpoint dei moduli, non il frontend.
- **Stati con il loro colore**: da inviare (grigio), inviata (blu), consegnata (verde), **non
  consegnata (ambra, non rosso)** con la spiegazione che è valida, scartata (rosso) con i codici di
  errore e il bottone **Rifai XML**.
- Colonna **ID SdI** nel registro, ricerca anche su quel campo (dallo SdI arriva l'identificativo,
  non il numero di fattura), filtro «emesse da inviare».
- **Storico delle trasmissioni** nel dettaglio della fattura: un file per riga, con la ricevuta
  scaricabile.
- **Avvisi in-app** (campanella con contatore interrogato ogni 30 secondi) per le ricevute e per le
  fatture di acquisto arrivate.
- Bottone **Aggiorna** sui registri di vendita e di acquisto: rilegge le query
  (`queryClient.invalidateQueries`) senza perdere filtri e pagina, e mostra l'ora dell'ultimo
  aggiornamento riuscito.
- **Errore di caricamento ≠ lista vuota.** `data ?? []` trasforma un errore in «nessuna fattura da
  incassare», cioè il messaggio più rassicurante nel momento in cui il programma non sa niente.
  Mostrare un riquadro d'errore con «Riprova».
- Pannello piattaforma **Canale SdI**: codice destinatario con copia, stato di tutti i controlli del
  §11, quarantena con attribuzione manuale, consegne di collaudo, avviso di prova.
- Se il frontend compone stringhe che il backend compone anche lui (il numero di fattura), farle
  arrivare dal backend.
- Menu ⋮ di riga in `position: fixed` calcolato con `getBoundingClientRect`, non `absolute`:
  dentro tabelle con `overflow` un menu assoluto viene tagliato.

---

## 14. Errori reali e loro causa

| sintomo | causa | rimedio |
|---|---|---|
| scarto **00002** «nome file duplicato» | progressivo fisso per fattura, o contatore riusato / doppio sullo stesso CF | contatore vero, per soggetto trasmittente, con lock, mai indietro (§5.4, §8.1) |
| scarto **00300** «identificativi fiscali non validi» | P.IVA in `IdTrasmittente` invece del codice fiscale (si vede solo con le ditte individuali) | CF in `IdTrasmittente` **e** nel nome file (§4.3) |
| scarto **00200** | Denominazione **e** Nome/Cognome insieme (es. nome commerciale finito nell'XML) | XOR in anagrafica (§4.4) |
| scarti **00421/00422/00423** | imposta sommata per riga, cassa fuori dal riepilogo, sconto unitario arrotondato a 2 decimali | §4.6 |
| scarto **00102** «file non firmato» | fattura verso la PA senza firma qualificata | fuori ambito (§15) |
| scarto per caratteri non ammessi | `—`, `“`, `€` nei testi | traslitterazione Latin-1 (§4.2) |
| «PKIX path building failed» / errore di catena al primo invio vero | truststore con la sola CA dell'Agenzia, il server di produzione è firmato da Sectigo | CA di sistema **oppure** CA dell'Agenzia (§5.2) |
| risposta all'invio «non interpretabile» | risposta MTOM multipart | estrarre la busta (§5.1) |
| `EI03` | canale non abilitato per l'ambiente chiamato | controllare accreditamento e endpoint |
| portale: «Err: NULL» in ricezione (1ª volta) | endpoint instradato per namespace indovinato: in pratica un 404 | instradare per nome locale (§7.3) |
| portale: «Err: NULL» in ricezione (2ª volta) | `Esito` qualificato nel namespace della radice | `<Esito xmlns="">` (§7.3) |
| Apache non riparte: `AH02562 no start line` | certificati in DER | convertirli in PEM (§2.6) |
| Apache cade al reload dopo «Syntax OK» | `configtest` non apre i certificati | attivazione con ripristino automatico (§3.1) |
| sonda del canale 403 | il vhost ammette solo il CN dello SdI | eccezione per `/sdi/stato` dopo il blocco `/sdi/` (§3.1) |
| uno scarto ignorato in silenzio | elenco fisso dei nomi delle ricevute, `RicevutaScarto` non previsto | togliere il prefisso Ricevuta/Notifica (§6.3) |
| sulla fattura compare la descrizione del canale invece dell'esito | `Descrizione` cercata ovunque, presa da `<Destinatario>` | figlia diretta della radice (§6.3) |
| «Ã¨» nelle note | ricevute del portale codificate due volte | riparazione a coppie (§6.3) |
| fatture «inviate» senza ID SdI, mai arrivate | registrazione manuale scambiata per invio | due azioni distinte nell'interfaccia (§13) |
| orari sbagliati di due ore | container in UTC | `TZ=Europe/Rome`, date con fuso (§3.3) |
| nessuna email partita, nessun errore | configurazione di posta non letta, ripiego silenzioso | avviso di prova sincrono; SPF/DKIM/DMARC (§11) |
| 500 su una lista che funzionava da vuota | DTO costruito fuori transazione da un oggetto caricato in modo pigro | caricare le relazioni nella query |

---

## 15. Fuori ambito, e perché

- **Fatture verso la Pubblica Amministrazione** (FPA12): vanno **firmate digitalmente** con firma
  qualificata (CAdES `.xml.p7m` o XAdES). L'obbligo è caduto nel 2019 per B2B e B2C, non per la PA.
  Il certificato del canale **non serve**: è TLS, non una firma. Strada semplice se servirà: scaricare
  l'XML, firmarlo col proprio dispositivo, ricaricare il `.p7m` e spedirlo dal canale. Il nome file va
  deciso guardando il **contenuto** (un PKCS#7 in DER comincia con il byte `0x30`), non il nome
  ricevuto. La firma automatica con un servizio remoto ha un vincolo che non è tecnico: **la firma è
  un atto personale**, e va inquadrata contrattualmente prima.
- **Scarico massivo (SMTS)**: recupererebbe lo storico delle fatture, ma la richiesta va **firmata con
  firma qualificata** dal titolare della P.IVA o da un intermediario delegato. Stesso muro della PA.
  Limiti da conoscere: 3 mesi per richiesta, 10 richieste al giorno per P.IVA, archivi disponibili 30
  giorni, e il primo scarico vale come **presa visione**.
- **Conservazione a norma**: non la fa il canale. Resta il servizio gratuito dell'Agenzia, con
  adesione a parte, e va scritto nel contratto con i clienti, insieme alla nomina a responsabile del
  trattamento.
- **Registri IVA e liquidazioni**: le fatture di acquisto entrano in un registro, ma il calcolo IVA
  periodico è un altro lavoro.

---

## 16. Checklist

**Accreditamento**
- [ ] sottodominio `<HOST_SDI>` con DNS che punta al VPS
- [ ] chiavi client e server generate sul server, `chmod 600`, backup cifrato fuori dal server
- [ ] CSR client `CN=SDI-<Codice Sottoscrittore>`, `C=IT`; CSR server `CN=<HOST_SDI>`
- [ ] certificati scaricati e convertiti in PEM; `.p12` del client per .NET; CA dell'Agenzia in un PEM
- [ ] endpoint di collaudo e produzione dichiarati (`/sdi/test/…` e `/sdi/…`)

**Infrastruttura**
- [ ] vhost `zz-sdi.conf`: `SSLVerifyClient require`, CN dello SdI, unset+set dell'intestazione,
      eccezione `/sdi/stato` per il nostro CN, tutto il resto chiuso
- [ ] attivazione con ripristino automatico; `curl` di prova sul sito principale
- [ ] Kestrel solo su `127.0.0.1`; middleware di verifica dell'intestazione; log di accesso `/sdi/*`
- [ ] fuso `Europe/Rome`; volume dell'archivio ricevuti incluso nel backup

**Codice**
- [ ] generatore FatturaPA 1.2.2 con validazione XSD (più `xmldsig`), traslitterazione Latin-1, calcoli §4.6
- [ ] validatore completo che blocca l'emissione con l'elenco dei problemi
- [ ] `IdTrasmittente` = CF; nome file = `IT<CF>_<progressivo>`; contatore con lock, mai indietro
- [ ] client `RiceviFile` con certificato client, fiducia CA di sistema **o** Agenzia, lettura MTOM
- [ ] in collaudo la fattura non diventa «inviata»; in produzione sì, solo con `IdentificativoSdI`
- [ ] servizio ricevute: sei operazioni, mai Fault, lettura per nome locale, prefisso tolto, storico per file
- [ ] servizio ricezione: niente `ER01` senza certificato verificato o senza copia su disco;
      `<Esito xmlns="">`; doppioni per identificativo; `.p7m`; più body; smistamento e quarantena
- [ ] endpoint di invio di prova solo verso il collaudo, senza registrare nulla
- [ ] sorveglianza: sonda, scadenze, disco, riconciliazione, email di guasto e di rientro

**Collaudo e produzione**
- [ ] scarto, consegna, impossibilità di recapito superati; WSR01 superato
- [ ] richiesta di produzione, PEC di conferma, `SDI_ENDPOINT` di produzione
- [ ] contatore controllato contro i progressivi già usati da quel CF
- [ ] codice destinatario B2B generato, mostrato nel pannello, comunicato ai clienti
- [ ] prima fattura vera su un documento che si può permettere di sbagliare, e verifica nel cassetto

**Configurazione** (variabili d'ambiente di ManageSuite, da rinominare a piacere)

| variabile | contenuto |
|---|---|
| `SDI_ENABLED` | `true` per accendere l'invio dal canale |
| `SDI_ENDPOINT` | `https://testservizi.fatturapa.it/ricevi_file` → `https://servizi.fatturapa.it/ricevi_file` |
| `SDI_KEYSTORE_PATH` / `SDI_KEYSTORE_PASSWORD` | `.p12` del certificato client e la sua password |
| `SDI_TRUSTSTORE_PATH` | PEM con le CA dell'Agenzia |
| `SDI_TRASMITTENTE_CF` | codice fiscale del titolare del canale |
| `SDI_CODICE_DESTINATARIO` | il codice B2B a 7 caratteri, da mostrare |
| `SDI_CLIENT_VERIFIED_HEADER` | `X-SdI-Client-Verified` (vuoto solo in sviluppo) |
| `SDI_ARCHIVIO_PATH` | cartella dell'archivio delle consegne ricevute |
| `SDI_STATO_URL` | `https://<HOST_SDI>/sdi/stato`, per la sonda |
| `SDI_ALERT_EMAIL` / `SDI_SPAZIO_MINIMO_GB` | destinatario degli avvisi, soglia del disco |

Riferimenti nel repository di ManageSuite: `docs/SDI/wsdl/` (contratti e note sulla forma dei
messaggi), `docs/SDI/apache/sdi-vhost.conf`, `docs/SDI/KitDiTest/`, `docs/ricezione/`, `docs/smts/`,
e il codice in `backend/src/main/java/com/managesuite/service/invoice/` (classi `Sdi*`,
`FatturaPaXmlGenerator`, `FiscalDataValidator`) e `service/purchase/` (`SdiFattureRicevuteService`,
`SdiArchivioRicevuti`, `FatturaPaXmlReader`, `FileFatturaExtractor`).
