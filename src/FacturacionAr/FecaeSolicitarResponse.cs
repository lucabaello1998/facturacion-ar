namespace FacturacionAr;

public sealed record ErrorItem(int Code, string Message);

public sealed record ObservationItem(int Code, string Message);

public sealed record FecaeSolicitarResponse(
    string Resultado,
    string? CAE,
    DateTime? CAEVencimiento,
    IReadOnlyList<ErrorItem> Errores,
    IReadOnlyList<ObservationItem> Observaciones)
{
    public bool Aprobado => Resultado == "A" && !string.IsNullOrEmpty(CAE);
}
