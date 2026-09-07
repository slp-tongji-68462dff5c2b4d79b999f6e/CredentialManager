using LiteDB;

namespace Tjslp.CredentialManager.Services;

public sealed class CredentialRepository
{
    private readonly ILiteCollection<CredentialOwnership> collection;

    public CredentialRepository(LiteDatabase database)
    {
        this.collection = database.GetCollection<CredentialOwnership>();
        this.collection.EnsureIndex(ownership => ownership.Owner);
    }

    public void Insert(CredentialOwnership ownership) => collection.Insert(ownership);

    public CredentialOwnership? FindById(string id) => collection.FindById(id);

    public IEnumerable<CredentialOwnership> FindByOwner(string owner) =>
        collection.Query().Where(ownership => ownership.Owner == owner).ToEnumerable();

    public IEnumerable<CredentialOwnership> FindAll() => collection.FindAll();

    public bool Delete(string id) => collection.Delete(id);
}
