namespace Tjslp.CredentialManager.Protocol;

public sealed record CreateResponse(string CredentialId, string Credential, DateTimeOffset? Expire);
