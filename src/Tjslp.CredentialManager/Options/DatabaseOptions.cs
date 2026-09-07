namespace Tjslp.CredentialManager.Options;

public sealed record DatabaseOptions(string Path)
{
    public string ConnectionString => $"Data Source={Path}";
}
