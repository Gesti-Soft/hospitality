using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

/// <summary>
/// Il file XML di una fattura da spedire o da scaricare. Ogni file generato consuma un progressivo
/// del suo trasmittente e resta in TrasmissioneFattura con i suoi byte: quello che parte è quello
/// che si conserva.
/// <para>
/// Un file già generato si riusa finché è ancora buono: stesso trasmittente, stesso modo di invio,
/// fattura non più modificata, e mai entrato nello SdI. Rispedire lo stesso file dopo una chiamata
/// caduta è proprio ciò che serve: se era arrivato, lo SdI scarta il doppione per nome e non nasce
/// una seconda fattura. Dopo uno scarto, invece, il file è bruciato e se ne genera uno nuovo con
/// progressivo nuovo, tenendo numero e data della fattura (per lo SdI non è mai esistita).
/// </para>
/// </summary>
public class FileSdiService(
    IDatiFatturaRepository fatture,
    IDatiClienteRepository clienti,
    IDatiAziendaliRepository aziende,
    ITrasmissioneFatturaRepository trasmissioni,
    IFatturaDocumentGenerator generatore,
    IConfigurazioneCanaleSdi canale)
{
    /// <summary>Il nome file ha 5 caratteri per il progressivo: in decimale, fino a 99999.</summary>
    public const int ProgressivoMassimo = 99999;

    /// <param name="viaCanale">True: lo spedisce il canale GestiSoft. False: la struttura lo carica da sé sul portale.</param>
    public async Task<TrasmissioneFattura> PreparaAsync(DatiFattura fattura, bool viaCanale, CancellationToken cancellationToken)
    {
        if (fattura.TipoEmissione == TipoEmissioneDocumento.Ricevuta)
        {
            throw new ConflictException("Una ricevuta di locazione breve non ha una fattura elettronica: l'operazione è fuori dal campo IVA e non passa dallo SDI. Resta scaricabile il PDF.");
        }

        var ultima = await trasmissioni.GetUltimaAsync(fattura.Id, cancellationToken);

        // Già nello SdI: si restituisce il file partito davvero, mai uno rigenerato.
        if (fattura.StatoSdi is StatoSdiFattura.Inviata or StatoSdiFattura.Consegnata or StatoSdiFattura.NonConsegnata)
        {
            return ultima is { Prova: false } && ultima.IdentificativoSdi == fattura.IdentificativoSdi
                ? ultima
                : throw new ConflictException("Il file inviato allo SdI per questa fattura non è stato trovato. Contatta l'assistenza.");
        }

        var azienda = await aziende.GetByStrutturaIdAsync(fattura.StrutturaId, cancellationToken);
        var (idPaese, codiceTrasmittente) = Trasmittente(azienda, viaCanale);

        if (ultima is not null && Riusabile(ultima, fattura, codiceTrasmittente, viaCanale))
        {
            return ultima;
        }

        var cliente = fattura.DatiClienteId is { } clienteId ? await clienti.GetAsync(clienteId, cancellationToken) : null;
        var stornata = fattura.FatturaStornataId is { } stornataId ? await fatture.GetAsync(stornataId, cancellationToken) : null;

        // Un dato obbligatorio mancante diventerebbe un elemento vuoto e lo SDI scarterebbe il file
        // giorni dopo, quando nessuno ricorda più quella fattura: meglio non generarlo e dire cosa
        // manca. Il PDF resta scaricabile comunque, non ha vincoli di tracciato.
        var motivi = generatore.ValidaPerSdi(fattura, cliente, azienda, stornata);
        if (motivi.Count > 0)
        {
            throw new ConflictException($"Fattura elettronica non generabile: {string.Join("; ", motivi)}.");
        }

        var numero = await trasmissioni.ProssimoProgressivoInvioAsync(codiceTrasmittente, canale.ProgressivoIniziale, cancellationToken);
        if (numero > ProgressivoMassimo)
        {
            throw new ConflictException("I progressivi dei file per lo SdI sono esauriti. Contatta l'assistenza.");
        }

        var progressivo = numero.ToString("00000", System.Globalization.CultureInfo.InvariantCulture);
        var xml = generatore.GeneraXmlSdi(fattura, cliente, azienda, new IntestazioneTrasmissione(idPaese, codiceTrasmittente, progressivo), stornata);

        // Il progressivo è già speso anche se lo schema boccia il file: un buco non costa niente.
        var errori = generatore.ValidaSchema(xml);
        if (errori.Count > 0)
        {
            throw new ConflictException($"Il file della fattura elettronica non rispetta lo schema dell'Agenzia delle Entrate: {string.Join("; ", errori.Take(5))}");
        }

        var trasmissione = new TrasmissioneFattura
        {
            StrutturaId = fattura.StrutturaId,
            DatiFatturaId = fattura.Id,
            NomeFile = $"{idPaese}{codiceTrasmittente}_{progressivo}.xml",
            ProgressivoInvio = progressivo,
            CodiceTrasmittente = codiceTrasmittente,
            ViaCanale = viaCanale,
            Xml = xml,
        };

        await trasmissioni.AddAsync(trasmissione, cancellationToken);
        return trasmissione;
    }

    /// <summary>
    /// Il trasmittente è chi spedisce davvero il file. Dal canale: il codice fiscale del titolare del
    /// canale, mai quello della struttura (il file uscirebbe dichiarando un trasmittente diverso da
    /// chi lo spedisce). A mano: la struttura, con il suo codice fiscale o, se manca, la partita IVA.
    /// </summary>
    private (string IdPaese, string Codice) Trasmittente(DatiAziendali? azienda, bool viaCanale)
    {
        if (viaCanale)
        {
            return Normalizza(canale.CodiceFiscaleTrasmittente) is { } codiceCanale
                ? ("IT", codiceCanale)
                : throw new ConflictException("Il canale di invio allo SdI non è ancora configurato. Contatta l'assistenza.");
        }

        var codice = Normalizza(azienda?.CodiceFiscale) ?? Normalizza(azienda?.PIva)
            ?? throw new ConflictException("Fattura elettronica non generabile: manca il codice fiscale o la partita IVA della struttura.");
        var paese = string.IsNullOrWhiteSpace(azienda?.Iso2) ? "IT" : azienda.Iso2.Trim().ToUpperInvariant();
        return (paese, codice);
    }

    private static bool Riusabile(TrasmissioneFattura ultima, DatiFattura fattura, string codiceTrasmittente, bool viaCanale) =>
        ultima.Esito is EsitoTrasmissioneSdi.Generato or EsitoTrasmissioneSdi.Rifiutato or EsitoTrasmissioneSdi.EsitoIgnoto
        && ultima.CodiceTrasmittente == codiceTrasmittente
        && ultima.ViaCanale == viaCanale
        // Modificata dopo la generazione: il file non dice più quello che dice la fattura. La
        // modifica è bloccata quando l'esito è ignoto, quindi qui non si butta mai un file forse partito.
        && (fattura.UpdatedAtUtc ?? fattura.CreatedAtUtc) <= ultima.CreatedAtUtc;

    /// <summary>
    /// Maiuscolo, senza spazi né separatori, senza il prefisso "IT" davanti a una partita IVA: in
    /// anagrafica i codici sono scritti a mano, e il paese nel nome file c'è già.
    /// </summary>
    public static string? Normalizza(string? codice)
    {
        var pulito = new string((codice ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (pulito.Length == 13 && pulito.StartsWith("IT", StringComparison.Ordinal) && pulito[2..].All(char.IsAsciiDigit))
        {
            pulito = pulito[2..];
        }

        return pulito.Length == 0 ? null : pulito;
    }
}
