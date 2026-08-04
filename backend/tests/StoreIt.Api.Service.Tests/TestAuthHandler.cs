using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StoreIt.Api;
using StoreIt.Application;

namespace StoreIt.Api.Service.Tests;

/// <summary>
/// Test-only authentication scheme that mints a principal from request headers
/// (<c>X-Test-Subject/Issuer/Email/Name</c>) and, like production's
/// <c>OnTokenValidated</c>, provisions the user via <see cref="ProvisionUserUseCase"/>
/// and stamps the internal id as the <c>sub_local</c> claim. This lets service tests
/// exercise real provisioning without a live IdP.
/// Absent <c>X-Test-Subject</c> → <see cref="AuthenticateResult.NoResult"/> (anonymous).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ProvisionUserUseCase provision
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    private const string SubjectHeader = "X-Test-Subject";
    private const string IssuerHeader = "X-Test-Issuer";
    private const string EmailHeader = "X-Test-Email";
    private const string NameHeader = "X-Test-Name";

    private const string DefaultIssuer = "https://test.local";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubjectHeader, out var subjectValues))
        {
            return AuthenticateResult.NoResult();
        }

        var subject = subjectValues.ToString();
        if (string.IsNullOrEmpty(subject))
        {
            return AuthenticateResult.NoResult();
        }

        var issuer = Header(IssuerHeader) ?? DefaultIssuer;
        var email = Header(EmailHeader);
        var name = Header(NameHeader);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new("iss", issuer),
        };
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }
        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);

        // Mirror OnTokenValidated: provision the local user and stamp its id.
        var user = await provision.ExecuteAsync(
            issuer,
            subject,
            email,
            name,
            Context.RequestAborted
        );
        identity.AddClaim(new Claim(CurrentUser.LocalIdClaim, user.Id.ToString()));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    private string? Header(string name) =>
        Request.Headers.TryGetValue(name, out var values) && !string.IsNullOrEmpty(values)
            ? values.ToString()
            : null;
}
