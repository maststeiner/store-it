namespace StoreIt.Domain;

/// <summary>
/// Fixed list of measurement units (SPEC-001). The API exposes enum codes;
/// display names are translated in the clients (i18n).
/// </summary>
public enum Unit
{
    Piece,
    Gram,
    Kilogram,
    Milliliter,
    Liter,
    Pack,
}
