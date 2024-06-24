namespace Hangfire.EntityFrameworkStorage.SampleStuff
{
    public interface ISqliteTempFileService
    {
        string GetConnectionString();
        void CreateDatabase();
        string GetDatabaseFileName();
    }
}