namespace GestiSoft.Contracts.Wubook;

/// <summary>Una camera già presente su Wubook (fetch_rooms), per la select di associazione manuale.</summary>
public record CameraWubookRemoteDto(int Id, string Nome, string? ShortName, int Occupancy, decimal Prezzo, int Disponibilita);
