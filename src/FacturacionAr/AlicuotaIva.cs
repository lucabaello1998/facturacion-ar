namespace FacturacionAr;

public sealed record AlicuotaIva(int Id, decimal BaseImponible, decimal Importe)
{
    // Códigos de alícuota de AFIP/ARCA.
    private static readonly HashSet<int> IdsValidos =
        [3, 4, 5, 6, 8, 9];

    public void Validar()
    {
        if (!IdsValidos.Contains(Id))
            throw new ArgumentException($"El código de alícuota de IVA {Id} no es válido.", nameof(Id));

        if (BaseImponible < 0)
            throw new ArgumentException("La base imponible no puede ser negativa.", nameof(BaseImponible));

        if (Importe < 0)
            throw new ArgumentException("El importe de IVA no puede ser negativo.", nameof(Importe));
    }
}
