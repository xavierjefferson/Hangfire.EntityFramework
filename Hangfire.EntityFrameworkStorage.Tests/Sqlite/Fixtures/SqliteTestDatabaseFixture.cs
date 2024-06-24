using System.Data.SQLite;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hangfire.EntityFrameworkStorage.Tests.Sqlite.Fixtures;

public class SqliteTestDatabaseFixture : DatabaseFixtureBase
{
    private static readonly object GlobalLock = new();
    private static DirectoryInfo _testFolder;

    public SqliteTestDatabaseFixture()
    {
        _testFolder = new DirectoryInfo(GetTempPath());
        _testFolder.Create();
        Monitor.Enter(GlobalLock);
        CreateDatabase();
    }

    public override Action<DbContextOptionsBuilder> DbContextOptionsBuilderAction
    {
        get { return i =>
        {
            i.UseSqlite(GetConnectionString());
            i.ConfigureWarnings(x => x.Ignore(RelationalEventId.AmbientTransactionWarning));
        }; }
    }

    public override void Cleanup()
    {
        try

        {
            DeleteFolder(_testFolder);
        }
        catch
        {
        }
    }

    public override string GetConnectionString()
    {
        var databaseFileName = GetDatabaseFileName();
        return $"Data Source={databaseFileName};";
    }

    public override void OnDispose()
    {
        Monitor.Exit(GlobalLock);
        Cleanup();
    }

    public override void CreateDatabase()
    {
        var databaseFileName = GetDatabaseFileName();
        if (!File.Exists(databaseFileName)) SQLiteConnection.CreateFile(databaseFileName);
    }


    private string GetDatabaseFileName()
    {
        return Path.Combine(_testFolder.FullName, "database.sqlite");
    }
}