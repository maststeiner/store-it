using StoreIt.Domain;

namespace StoreIt.Domain.Tests;

/// <summary>
/// Derived from SPEC-003 acceptance criteria and edge cases (EC-01, EC-02) — not from the implementation.
/// </summary>
public class UserTests
{
    private static readonly DateTimeOffset AnyDate = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    // --- Create: happy paths ---

    [Fact]
    public void Create_WithAllFields_SetsFieldsAndGeneratesId()
    {
        var user = User.Create("https://issuer.example", "sub-123", "alice@example.com", "Alice", AnyDate);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("https://issuer.example", user.Issuer);
        Assert.Equal("sub-123", user.Subject);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("Alice", user.DisplayName);
        Assert.Equal(AnyDate, user.CreatedAt);
    }

    [Fact]
    public void Create_WithNoEmailOrName_UsesDisplayNameFallback()
    {
        // EC-02: no displayName, no email → "user-<sub≤8>"
        var user = User.Create("https://issuer.example", "sub-123456789", null, null, AnyDate);

        Assert.Equal("user-sub-1234", user.DisplayName);
    }

    [Fact]
    public void Create_WithShortSubjectAndNoEmailOrName_UsesEntireSubject()
    {
        // EC-02: subject shorter than 8 chars → use full subject
        var user = User.Create("https://issuer.example", "abc", null, null, AnyDate);

        Assert.Equal("user-abc", user.DisplayName);
    }

    [Fact]
    public void Create_WithNoName_FallsBackToEmail()
    {
        // EC-02: no displayName, has email → email used as display name
        var user = User.Create("https://issuer.example", "sub-123", "alice@example.com", null, AnyDate);

        Assert.Equal("alice@example.com", user.DisplayName);
    }

    [Fact]
    public void Create_WithNameAndEmail_UsesDisplayName()
    {
        // displayName takes precedence over email
        var user = User.Create("https://issuer.example", "sub-123", "alice@example.com", "Alice", AnyDate);

        Assert.Equal("Alice", user.DisplayName);
    }

    // --- Create: validation ---

    [Fact]
    public void Create_WithEmptyIssuer_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            User.Create("", "sub-123", null, null, AnyDate)
        );

        Assert.Equal("user.issuer.empty", exception.ErrorCode);
    }

    [Fact]
    public void Create_WithWhitespaceIssuer_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            User.Create("   ", "sub-123", null, null, AnyDate)
        );

        Assert.Equal("user.issuer.empty", exception.ErrorCode);
    }

    [Fact]
    public void Create_WithEmptySubject_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            User.Create("https://issuer.example", "", null, null, AnyDate)
        );

        Assert.Equal("user.subject.empty", exception.ErrorCode);
    }

    [Fact]
    public void Create_WithWhitespaceSubject_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            User.Create("https://issuer.example", "   ", null, null, AnyDate)
        );

        Assert.Equal("user.subject.empty", exception.ErrorCode);
    }

    // --- UpdateProfile ---

    [Fact]
    public void UpdateProfile_ChangesEmailAndName()
    {
        var user = User.Create("https://issuer.example", "sub-123", "alice@example.com", "Alice", AnyDate);

        user.UpdateProfile("bob@example.com", "Bob");

        Assert.Equal("bob@example.com", user.Email);
        Assert.Equal("Bob", user.DisplayName);
    }

    [Fact]
    public void UpdateProfile_WithNullName_FallsBackToEmail()
    {
        var user = User.Create("https://issuer.example", "sub-123", "alice@example.com", "Alice", AnyDate);

        user.UpdateProfile("newemail@example.com", null);

        Assert.Equal("newemail@example.com", user.DisplayName);
    }

    [Fact]
    public void UpdateProfile_WithNoEmailOrName_FallsBackToSubjectPrefix()
    {
        var user = User.Create("https://issuer.example", "sub-123456789", "alice@example.com", "Alice", AnyDate);

        user.UpdateProfile(null, null);

        Assert.Null(user.Email);
        Assert.Equal("user-sub-1234", user.DisplayName);
    }
}
