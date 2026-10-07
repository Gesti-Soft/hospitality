using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Un file XML generato per una fattura: una riga per file, mai sovrascritta. Il nome cambia a ogni
/// rigenerazione (dopo uno scarto, o dopo una modifica prima dell'invio), e le ricevute dello SdI si
/// agganciano per nome: senza lo storico, una ricevuta in ritardo sul file vecchio non troverebbe
/// più la fattura. La fattura riassume l'ultima trasmissione (DatiFattura.StatoSdi), questa le tiene tutte.
/// </summary>
public class TrasmissioneFattura : TenantEntity
{
    public Guid DatiFatturaId { get; set; }

    /// <summary>"IT" + codice del trasmittente + "_" + progressivo + ".xml". Unico in tutto il sistema.</summary>
    public string NomeFile { get; set; } = string.Empty;

    /// <summary>Il progressivo dentro il file (ProgressivoInvio), uguale a quello del nome.</summary>
    public string ProgressivoInvio { get; set; } = string.Empty;

    /// <summary>
    /// Codice fiscale di chi spedisce il file (IdTrasmittente): quello del titolare del canale se
    /// parte dal canale, quello della struttura se lo carica lei dal portale. Un file non si rispedisce
    /// da un trasmittente diverso da quello che dichiara.
    /// </summary>
    public string CodiceTrasmittente { get; set; } = string.Empty;

    /// <summary>Preparato per partire dal canale (true) o scaricato per il caricamento a mano (false).</summary>
    public bool ViaCanale { get; set; }

    /// <summary>
    /// Spedito allo SdI di collaudo: il giro è vero, ma il documento non ha valore. Si conserva e si
    /// mostra, e non tocca mai lo stato della fattura.
    /// </summary>
    public bool Prova { get; set; }

    /// <summary>I byte esatti del file: quello che è partito è quello che resta, anche se poi la fattura cambia.</summary>
    public byte[] Xml { get; set; } = [];

    public EsitoTrasmissioneSdi Esito { get; set; } = EsitoTrasmissioneSdi.Generato;

    /// <summary>Numero che lo SdI assegna al file alla presa in carico.</summary>
    public string? IdentificativoSdi { get; set; }

    public DateTime? InviataAtUtc { get; set; }

    public DateTime? EsitoAtUtc { get; set; }

    /// <summary>Errore dell'invio (EI0x), o descrizione e codici dello scarto.</summary>
    public string? Nota { get; set; }

    /// <summary>Chi ha spedito il file (email dell'operatore).</summary>
    public string? InviataDa { get; set; }
}
