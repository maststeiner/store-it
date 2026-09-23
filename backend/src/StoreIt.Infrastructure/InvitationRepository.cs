using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StoreIt.Application;
using StoreIt.Domain;

namespace StoreIt.Infrastructure;

public sealed class InvitationRepository(StoreItDbContext dbContext) : IInvitationRepository
{
    public Task<StorageInvitation?> GetByStorageIdAsync(
        Guid storageId,
        CancellationToken cancellationToken
    ) =>
        dbContext.StorageInvitations.FirstOrDefaultAsync(
            i => i.StorageId == storageId,
            cancellationToken
        );

    public Task<StorageInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken
    ) =>
        dbContext.StorageInvitations.FirstOrDefaultAsync(
            i => i.TokenHash == tokenHash,
            cancellationToken
        );

    public void Add(StorageInvitation invitation) => dbContext.StorageInvitations.Add(invitation);

    public void Remove(StorageInvitation invitation) =>
        dbContext.StorageInvitations.Remove(invitation);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// ADR-008 decision 3: 256 random bits, URL-safe base64 without padding (43 characters); stored
/// and looked up as lower-case SHA-256 hex. A database dump is therefore not a list of links.
/// </summary>
public sealed class InvitationTokens : IInvitationTokens
{
    public string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
