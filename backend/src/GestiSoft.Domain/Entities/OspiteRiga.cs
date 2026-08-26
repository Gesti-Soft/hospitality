using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Riga ospite (una persona) dentro una scheda Ospite. Porta 1:1 da
/// OrderManagement.Model.BusinesObject.OspitiRow del sistema legacy. Il campo di testo libero
/// "CameraOccupata" del legacy (ridondante con IdCamera, che nel legacy non era nemmeno una FK
/// vera) non viene riportato: qui CameraId è una FK vera verso SettingRoom.
/// </summary>
public class OspiteRiga : TenantEntity
{
    public Guid? OspiteId { get; set; }

    public Ospite? Ospite { get; set; }

    public Guid? CameraId { get; set; }

    public SettingRoom? Camera { get; set; }

    public int? Permanenza { get; set; }

    public DateTime? DataNascita { get; set; }

    public Sesso? Sesso { get; set; }

    public string? Cognome { get; set; }

    public string? Nome { get; set; }

    public string? Cittadinanza { get; set; }

    public string? LuogoNascita { get; set; }

    public string? StatoNascita { get; set; }

    public string? LuogoResidenza { get; set; }

    public bool? PostoLetto { get; set; }

    public bool EsenteDaTassa { get; set; }
}
