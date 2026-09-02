using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Osservatorio;

public interface IOsservatorioAppartamentoRepository
{
    Task<IReadOnlyList<OsservatorioAppartamento>> ListByStrutturaAsync(Guid strutturaId, CancellationToken cancellationToken);

    Task<OsservatorioAppartamento?> GetAsync(Guid strutturaId, Guid appartamentoId, CancellationToken cancellationToken);

    Task AddAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken);

    Task UpdateAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken);

    Task DeleteAsync(OsservatorioAppartamento entity, CancellationToken cancellationToken);

    void RimuoviTipologia(OsservatorioAppartamentoTipologia riga);

    /// <summary>
    /// Aggiunge esplicitamente al DbContext prima di agganciarla via navigazione (entity.Tipologie.Add) —
    /// necessario perché la riga ha già una chiave Guid valorizzata lato client (costruttore di
    /// TenantEntity) e il genitore è già in stato Modified: senza questo, EF la classifica per
    /// convenzione come Modified invece che Added e genera un UPDATE su una riga che non esiste
    /// ancora (stesso bug già corretto in OspiteRepository.AddMembro).
    /// </summary>
    void AggiungiTipologia(OsservatorioAppartamentoTipologia riga);
}
