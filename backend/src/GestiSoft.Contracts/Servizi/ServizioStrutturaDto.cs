using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Servizi;

/// <summary>Servizio extra del listino della struttura (escursione, parcheggio, transfer…).</summary>
/// AliquotaIva e Natura entrambe null = quella predefinita della struttura.
public record ServizioStrutturaDto(Guid Id, string Nome, decimal Prezzo, ModalitaPrezzoServizio Modalita, bool Attivo, AliquotaIva? AliquotaIva = null, NaturaIva? Natura = null);

public record SalvaServizioRequest(string Nome, decimal Prezzo, ModalitaPrezzoServizio Modalita, bool Attivo, AliquotaIva? AliquotaIva = null, NaturaIva? Natura = null);

/// <summary>
/// Servizio extra venduto con una prenotazione, con nome e prezzo copiati al momento dell'aggiunta.
/// Al solo per i servizi a notte (giorno dopo l'ultima notte, come il check-out).
/// </summary>
public record ServizioPrenotazioneDto(
    Guid Id,
    Guid ServizioId,
    string Nome,
    ModalitaPrezzoServizio Modalita,
    decimal PrezzoUnitario,
    int Quantita,
    DateOnly Dal,
    DateOnly? Al,
    OrigineServizio Origine,
    string? AggiuntoDa,
    DateTime AggiuntoIlUtc);

/// <summary>
/// Riga di servizio da mettere sulla prenotazione. RigaId per una riga che la prenotazione ha già
/// (tiene il prezzo con cui è stata venduta), null per una nuova. Quantità: persone per i servizi "a
/// persona", altrimenti unità. Al solo per i servizi a notte.
/// </summary>
public record ServizioPrenotazioneRichiesta(Guid? RigaId, Guid ServizioId, int Quantita, DateOnly Dal, DateOnly? Al = null);
