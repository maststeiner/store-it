using System.Text;
using System.Text.RegularExpressions;

namespace StoreIt.Domain;

/// <summary>
/// SPEC-011: a tag of a storage (D1), reachable only through items (D2). <see cref="Name"/>
/// is the spelling of the first assignment, <see cref="NormalizedName"/> the comparison key
/// (D4/D8: case-insensitive, whitespace trimmed and collapsed, Unicode NFC — accents are kept).
/// </summary>
public class Tag
{
    public const int MaxLength = 30;
    public const int MaxPerItem = 10;

    private static readonly Regex InnerWhitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;

    private Tag() { } // EF Core

    internal Tag(string cleanedName)
    {
        Id = Guid.NewGuid();
        Name = cleanedName;
        NormalizedName = Normalize(cleanedName);
    }

    /// <summary>
    /// The display form of a typed tag: trimmed, inner whitespace collapsed to one space,
    /// Unicode NFC. Empty for blank input (EC-08). Validates the length (AC-03).
    /// </summary>
    public static string Clean(string? raw)
    {
        // A null element (`"tags": [null]` from JSON) is blank input, not a server error.
        if (raw is null)
        {
            return string.Empty;
        }

        var cleaned = InnerWhitespace.Replace(raw.Trim(), " ").Normalize(NormalizationForm.FormC);
        if (cleaned.Length > MaxLength)
        {
            throw new DomainValidationException(
                "item.tags.tooLong",
                $"A tag may have at most {MaxLength} characters."
            );
        }

        return cleaned;
    }

    /// <summary>The comparison key of a cleaned name (D8).</summary>
    public static string Normalize(string cleanedName) => cleanedName.ToLowerInvariant();
}
