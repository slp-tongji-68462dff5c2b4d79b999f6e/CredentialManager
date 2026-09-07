namespace Tjslp.CredentialManager.Protocol;

public sealed record QueryResponse(IReadOnlyList<QueryItem> Credentials);
