namespace GestiSoft.Domain.Common;

/// <summary>
/// Base per entità globali, non legate a una Struttura (es. tabelle di riferimento condivise
/// come Comuni/Stati/Documenti/TipoAlloggiato). Le entità operative usano invece TenantEntity.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
