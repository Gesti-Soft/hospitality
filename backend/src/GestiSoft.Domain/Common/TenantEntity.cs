namespace GestiSoft.Domain.Common;

public abstract class TenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StrutturaId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
