namespace StoreIt.Application;

/// <summary>
/// Port: the currently authenticated user (null UserId when anonymous).
/// The implementation lives in the API layer (Task 8) and is registered there.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The internal user id, or <c>null</c> when the request is unauthenticated.</summary>
    Guid? UserId { get; }
}
