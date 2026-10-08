namespace StoreIt.Application;

/// <summary>
/// SPEC-011: two members changed the same storage's tags at the same time — one created a
/// tag the other also created (unique index), or one pruned a tag the other was assigning
/// (foreign key). Raised by Infrastructure on the constraint violation → 409; the client
/// simply retries the save, the storage's tags are reloaded with it.
/// </summary>
public sealed class TagConflictException()
    : Exception("The storage's tags changed concurrently; retry the operation.");
