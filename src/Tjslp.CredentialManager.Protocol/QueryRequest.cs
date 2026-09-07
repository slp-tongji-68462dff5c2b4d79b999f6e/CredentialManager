namespace Tjslp.CredentialManager.Protocol;

public sealed record QueryRequest(IReadOnlyList<string> CredentialIds);
