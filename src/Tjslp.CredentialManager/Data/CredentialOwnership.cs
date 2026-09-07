namespace Tjslp.CredentialManager.Data;

public sealed class CredentialOwnership
{
    public required string Id { get; set; }

    public required string Owner { get; set; }

    public required string Alias { get; set; }
}
