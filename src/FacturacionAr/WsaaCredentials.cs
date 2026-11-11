namespace FacturacionAr;

public sealed record WsaaCredentials(
    string Token,
    string Sign,
    DateTime Expiration);
