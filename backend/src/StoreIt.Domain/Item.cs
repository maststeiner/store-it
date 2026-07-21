namespace StoreIt.Domain;

/// <summary>
/// An entry in a storage (SPEC-001): name · amount (max. one decimal place, > 0) ·
/// unit (fixed list) · at least one of expiry/production date.
/// </summary>
public class Item
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public Unit Unit { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public DateOnly? ProductionDate { get; private set; }

    private Item() { } // EF Core

    internal Item(
        string name,
        decimal amount,
        Unit unit,
        DateOnly? expiryDate,
        DateOnly? productionDate
    )
    {
        Id = Guid.NewGuid();
        Rename(name);
        ChangeAmount(amount);
        SetUnit(unit);
        SetDates(expiryDate, productionDate);
    }

    public ExpiryStatus GetExpiryStatus(DateOnly today) => ExpiryRules.GetStatus(ExpiryDate, today);

    /// <summary>AC-07: edit name, amount, unit and dates (same validation as AC-06).</summary>
    public void Update(
        string name,
        decimal amount,
        Unit unit,
        DateOnly? expiryDate,
        DateOnly? productionDate
    )
    {
        Rename(name);
        ChangeAmount(amount);
        SetUnit(unit);
        SetDates(expiryDate, productionDate);
    }

    private void SetUnit(Unit unit)
    {
        // AC-06: unit must be from the fixed list. JsonStringEnumConverter accepts
        // integer tokens by default, so an out-of-range value (e.g. 999) can reach
        // here as an undefined enum — reject it.
        if (!Enum.IsDefined(unit))
        {
            throw new DomainValidationException("item.unit.invalid", "Unit is not a valid value.");
        }

        Unit = unit;
    }

    private void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("item.name.empty", "Item name must not be empty.");
        }

        Name = name.Trim();
    }

    private void ChangeAmount(decimal amount)
    {
        // AC-06: amount > 0 with at most one decimal place; EC-04: no silent rounding.
        if (amount <= 0)
        {
            throw new DomainValidationException(
                "item.amount.notPositive",
                "Item amount must be greater than 0."
            );
        }

        if (decimal.Round(amount, 1) != amount)
        {
            throw new DomainValidationException(
                "item.amount.tooManyDecimals",
                "Item amount must have at most one decimal place."
            );
        }

        Amount = amount;
    }

    private void SetDates(DateOnly? expiryDate, DateOnly? productionDate)
    {
        // AC-06: at least one of expiry/production date is required (both allowed).
        if (expiryDate is null && productionDate is null)
        {
            throw new DomainValidationException(
                "item.dates.missing",
                "An item requires an expiry date or a production date."
            );
        }

        ExpiryDate = expiryDate;
        ProductionDate = productionDate;
    }
}
