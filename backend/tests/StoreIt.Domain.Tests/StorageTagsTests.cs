namespace StoreIt.Domain.Tests;

/// <summary>
/// SPEC-011 — tags on items, derived from AC-01…AC-04 and EC-01/EC-03/EC-06/EC-08:
/// canonical spelling, case-insensitive identity, limits, and the "no item, no tag" rule.
/// </summary>
public class StorageTagsTests
{
    private static readonly DateOnly AnyDate = new(2026, 7, 13);
    private static readonly Guid AnyOwner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Storage Pantry() => Storage.Create("Pantry", AnyOwner);

    private static Item Add(Storage storage, string name, params string[] tags) =>
        storage.AddItem(name, 1m, Unit.Piece, AnyDate, null, tags);

    private static string[] Names(IEnumerable<Tag> tags) =>
        tags.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    [Fact]
    public void AddItem_WithTags_CreatesTheTagsOnTheStorage()
    {
        // AC-01 / AC-02: new tags are created with the typed spelling.
        var storage = Pantry();

        var item = Add(storage, "Beans", "Dosen", "homemade");

        Assert.Equal(["Dosen", "homemade"], Names(item.Tags));
        Assert.Equal(["Dosen", "homemade"], Names(storage.Tags));
    }

    [Fact]
    public void AddItem_WithExistingTagInOtherCase_ReusesTheFirstSpelling()
    {
        // EC-01 / D4: "Dosen" and "dosen" are one tag, the first spelling wins.
        var storage = Pantry();
        Add(storage, "Beans", "Dosen");

        var second = Add(storage, "Corn", " dosen ");

        Assert.Equal(["Dosen"], Names(second.Tags));
        Assert.Single(storage.Tags);
    }

    [Fact]
    public void AddItem_NormalisesWhitespaceAndKeepsAccents()
    {
        // D8 / EC-02: inner whitespace collapses, accents stay significant.
        var storage = Pantry();

        var item = Add(storage, "Cheese", "selbst   gemacht", "Käse", "Kase");

        // Ordinal order: "Kase" (a) sorts before "Käse" (ä).
        Assert.Equal(["Kase", "Käse", "selbst gemacht"], Names(item.Tags));
    }

    [Fact]
    public void AddItem_IgnoresBlankTagsAndCollapsesDuplicates()
    {
        // EC-06 / EC-08: blanks drop out, duplicates count once — ten distinct tags pass.
        var storage = Pantry();
        var tags = Enumerable
            .Range(1, 9)
            .Select(i => $"t{i}")
            .Append("T1")
            .Append("  ")
            .Append("t9");

        var item = Add(storage, "Many", tags.ToArray());

        Assert.Equal(9, item.Tags.Count);
    }

    [Fact]
    public void AddItem_WithMoreThanTenDistinctTags_Throws()
    {
        // AC-03
        var storage = Pantry();
        var tags = Enumerable.Range(1, 11).Select(i => $"t{i}").ToArray();

        var exception = Assert.Throws<DomainValidationException>(() => Add(storage, "Many", tags));

        Assert.Equal("item.tags.tooMany", exception.ErrorCode);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public void AddItem_WithATagLongerThanThirtyCharacters_Throws()
    {
        // AC-03: the limit applies after trimming.
        var storage = Pantry();

        var exception = Assert.Throws<DomainValidationException>(() =>
            Add(storage, "Long", "  " + new string('x', 31) + "  ")
        );

        Assert.Equal("item.tags.tooLong", exception.ErrorCode);
        Assert.Empty(storage.Tags);
    }

    [Fact]
    public void AddItem_WithExactlyThirtyCharactersAfterTrimming_Passes()
    {
        var storage = Pantry();

        var item = Add(storage, "Edge", " " + new string('x', 30) + " ");

        Assert.Equal(30, item.Tags.Single().Name.Length);
    }

    [Fact]
    public void UpdateItem_ReplacesTheTagsAndPrunesTheUnused()
    {
        // AC-04 / EC-03: the last item loses "homemade" → the tag is gone.
        var storage = Pantry();
        var item = Add(storage, "Beans", "Dosen", "homemade");
        Add(storage, "Corn", "Dosen");

        storage.UpdateItem(item.Id, "Beans", 1m, Unit.Piece, AnyDate, null, ["dosen", "bio"]);

        Assert.Equal(["bio", "Dosen"], Names(item.Tags));
        Assert.Equal(["bio", "Dosen"], Names(storage.Tags));
    }

    [Fact]
    public void UpdateItem_WithoutTags_ClearsThem()
    {
        // EC-07: a missing tags list means none.
        var storage = Pantry();
        var item = Add(storage, "Beans", "Dosen");

        storage.UpdateItem(item.Id, "Beans", 1m, Unit.Piece, AnyDate, null);

        Assert.Empty(item.Tags);
        Assert.Empty(storage.Tags);
    }

    [Fact]
    public void UpdateItem_ToAmountZero_RemovesTheItemAndItsOrphanedTags()
    {
        var storage = Pantry();
        var item = Add(storage, "Beans", "Dosen");

        var kept = storage.UpdateItem(item.Id, "Beans", 0m, Unit.Piece, AnyDate, null, ["Dosen"]);

        Assert.False(kept);
        Assert.Empty(storage.Tags);
    }

    [Fact]
    public void RemoveItem_PrunesTagsNoOtherItemCarries()
    {
        // AC-04: "homemade" was only on the removed item; "Dosen" survives on the other.
        var storage = Pantry();
        var beans = Add(storage, "Beans", "Dosen", "homemade");
        Add(storage, "Corn", "Dosen");

        storage.RemoveItem(beans.Id);

        Assert.Equal(["Dosen"], Names(storage.Tags));
    }

    [Fact]
    public void GetTagsWithCounts_CountsItemsPerTag_SortedCaseInsensitively()
    {
        // AC-05
        var storage = Pantry();
        Add(storage, "Beans", "Dosen", "homemade");
        Add(storage, "Corn", "dosen");
        Add(storage, "Jam", "Bio");

        var tags = storage.GetTagsWithCounts();

        Assert.Equal(
            [("Bio", 1), ("Dosen", 2), ("homemade", 1)],
            tags.Select(t => (t.Tag.Name, t.ItemCount)).ToArray()
        );
    }

    [Fact]
    public void Tag_Clean_TrimsCollapsesAndNormalisesUnicode()
    {
        Assert.Equal("selbst gemacht", Tag.Clean("  selbst \t gemacht  "));
        Assert.Equal("", Tag.Clean("   "));
        // NFC: a decomposed "è" (e + combining grave) becomes the precomposed character.
        Assert.Equal("Crème", Tag.Clean("Cre\u0300me"));
        Assert.Equal("crème", Tag.Normalize(Tag.Clean("CRÈME")));
    }
}
