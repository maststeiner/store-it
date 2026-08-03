namespace StoreIt.Domain;

/// <summary>
/// Account aggregate root (SPEC-003): an identity keyed by (Issuer, Subject).
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public string Issuer { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string? Email { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { } // EF Core

    private User(string issuer, string subject, string? email, string? displayName, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            throw new DomainValidationException("user.issuer.empty", "Issuer must not be empty.");

        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainValidationException("user.subject.empty", "Subject must not be empty.");

        Id = Guid.NewGuid();
        Issuer = issuer;
        Subject = subject;
        Email = email;
        DisplayName = ResolveDisplayName(displayName, email, subject);
        CreatedAt = createdAt;
    }

    /// <summary>Create a user account keyed by (issuer, subject).</summary>
    public static User Create(
        string issuer,
        string subject,
        string? email,
        string? displayName,
        DateTimeOffset createdAt
    ) => new(issuer, subject, email, displayName, createdAt);

    /// <summary>Update mutable profile fields. DisplayName is never left empty (EC-02).</summary>
    public void UpdateProfile(string? email, string? displayName)
    {
        Email = email;
        DisplayName = ResolveDisplayName(displayName, email, Subject);
    }

    /// <summary>
    /// EC-02: DisplayName fallback chain — displayName → email → "user-&lt;sub≤8&gt;".
    /// </summary>
    private static string ResolveDisplayName(string? displayName, string? email, string subject) =>
        !string.IsNullOrWhiteSpace(displayName) ? displayName!
        : !string.IsNullOrWhiteSpace(email)     ? email!
        : $"user-{subject[..Math.Min(8, subject.Length)]}";
}
