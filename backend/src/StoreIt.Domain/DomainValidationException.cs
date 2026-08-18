namespace StoreIt.Domain;

/// <summary>
/// Violation of a domain invariant (SPEC-001 AC-02/AC-06). Carries a locale-neutral
/// error code — translation happens exclusively in the clients (arc42 §8 i18n).
/// </summary>
public sealed class DomainValidationException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
