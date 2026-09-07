namespace Tjslp.CredentialManager.Options;

public sealed record OidcOptions(
    string Authority,
    string ClientId,
    string ClientSecret,
    string? TrustedCa);
