namespace GestiSoft.Application.Assistenza;

/// <summary>Archivio su disco delle foto allegate ai ticket (vedi Domain.Entities.TicketAllegato).</summary>
public interface IAllegatiStorage
{
    /// <summary>Salva il file e restituisce il percorso relativo da conservare sulla riga dell'allegato.</summary>
    Task<string> SalvaAsync(Guid strutturaId, byte[] contenuto, string estensione, CancellationToken cancellationToken);

    /// <summary>Null se il file non esiste più.</summary>
    Stream? Apri(string percorso);

    /// <summary>False se il file c'era e non si è riuscito a cancellarlo; un file già assente conta come cancellato.</summary>
    bool Elimina(string percorso);
}
