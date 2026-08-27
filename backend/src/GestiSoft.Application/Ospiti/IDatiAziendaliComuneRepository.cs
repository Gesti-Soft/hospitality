namespace GestiSoft.Application.Ospiti;

/// <summary>
/// Lettura minima e mirata del Comune della struttura (da DatiAziendali, popolato in Fase 4) —
/// serve solo per l'esenzione tassa di soggiorno per residenza (vedi OspitiService). Non è il
/// repository completo di DatiAziendali, che arriverà con la Fase 4 Finanze &amp; Fatturazione.
/// </summary>
public interface IDatiAziendaliComuneRepository
{
    Task<string?> GetComuneAsync(Guid strutturaId, CancellationToken cancellationToken);
}
