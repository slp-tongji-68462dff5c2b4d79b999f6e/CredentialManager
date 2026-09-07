namespace Tjslp.CredentialManager.Protocol;

public sealed record QueryItem(string CredentialId, DateTimeOffset? Expire);
