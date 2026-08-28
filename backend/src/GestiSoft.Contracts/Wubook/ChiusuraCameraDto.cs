namespace GestiSoft.Contracts.Wubook;

/// <summary>Id null e Origine="Prenotazione" per le righe derivate da una prenotazione reale (mai eliminabili, lette al volo, non un record persistito) — v. WubookChiusureService.ListaAsync.</summary>
public record ChiusuraCameraDto(Guid? Id, Guid CameraId, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita, string Origine);

public record CreaChiusuraCameraRequestDto(Guid CameraId, DateTime DataInizio, DateTime DataFine, string? Motivo, int? Quantita);
