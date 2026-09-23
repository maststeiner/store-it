namespace StoreIt.Application;

/// <summary>SPEC-007 AC-04: an owner-only operation was attempted by a member → 403.</summary>
public sealed class StorageOwnerOnlyException(Guid storageId)
    : Exception($"Only the owner may perform this operation on storage {storageId}.");

/// <summary>SPEC-007 AC-08/AC-10: unknown, expired or deactivated invitation token → 404.</summary>
public sealed class InvitationInvalidException()
    : Exception("The invitation link is invalid or has expired.");

/// <summary>SPEC-007 AC-14: the owner cannot leave — hand over or delete instead → 409.</summary>
public sealed class OwnerCannotLeaveException(Guid storageId)
    : Exception($"The owner cannot leave storage {storageId}; hand over ownership or delete it.");
