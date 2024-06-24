using System.Data.SQLite;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkStorage.Tests.InMemory.Fixtures
{
    public class InMemoryTestDatabaseFixture : DatabaseFixtureBase
    {
        private static readonly object GlobalLock = new object();

        public InMemoryTestDatabaseFixture()
        {
            Monitor.Enter(GlobalLock);
            CreateDatabase();
        }

        public override Action<DbContextOptionsBuilder> DbContextOptionsBuilderAction
        {
            get { return i => i.UseInMemoryDatabase(databaseName: "ForTesting"); }
        }

        public override void Cleanup()
        {
           
        }

        public override string GetConnectionString()
        {
            return null;
        }

        public override void OnDispose()
        {
            Monitor.Exit(GlobalLock);
            Cleanup();
        }

        public override void CreateDatabase()
        {
        }


    }
}