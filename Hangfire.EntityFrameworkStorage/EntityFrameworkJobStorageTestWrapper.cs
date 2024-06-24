namespace Hangfire.EntityFrameworkStorage;

public class EntityFrameworkJobStorageTestWrapper
{
    private readonly EntityFrameworkJobStorage _storage;

    public EntityFrameworkJobStorageTestWrapper(EntityFrameworkJobStorage storage)
    {
        _storage = storage;
    }

    public int ExecuteHqlQuery(string query)
    {
        return
            0; // _storage.UseStatelessSession(dbContext => { return dbContext.CreateQuery(query).ExecuteUpdate(); });
    }
}