using System;
using System.IO;
using System.Reflection;
using Hangfire.EntityFrameworkStorage.Entities;
using Moq;
using Snork.EntityFrameworkTools;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures
{
    public abstract class DatabaseFixtureBase : IDisposable
    {
        private bool _updateSchema = true; //only create schema on the first pass


        protected static string Instance => Guid.NewGuid().ToString();

        public abstract ProviderTypeEnum ProviderType { get; }


        public void Dispose()
        {
            OnDispose();
        }

        public abstract void Cleanup();

        public EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions options = null)
        {
            return new EntityFrameworkJobStorage(ProviderType, GetConnectionString(), ProgressOptions(options));
        }

        protected void DeleteFolder(DirectoryInfo directoryInfo)
        {
            foreach (var fileInfo in directoryInfo.GetFiles())
                try
                {
                    fileInfo.Delete();
                }
                catch
                {
                    // ignored
                }

            foreach (var info in directoryInfo.GetDirectories())
                try
                {
                    DeleteFolder(info);
                }
                catch
                {
                    // ignored
                }

            directoryInfo.Delete();
        }

        public abstract string GetConnectionString();


        public void CleanTables(StatelessSessionWrapper session)
        {
            session.DeleteAll<_JobState>();
            session.DeleteAll<_JobParameter>();
            session.DeleteAll<_JobQueue>();
            session.DeleteAll<_Job>();
            session.DeleteAll<_Hash>();
            session.DeleteAll<_Set>();
            session.DeleteAll<_List>();
            session.DeleteAll<_DistributedLock>();
            session.DeleteAll<_AggregatedCounter>();
            session.DeleteAll<_Counter>();
            session.DeleteAll<_Server>();
        }

        protected string GetTempPath()
        {
            return Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name, Instance);
        }

        public abstract void CreateDatabase();

        private EntityFrameworkStorageOptions ProgressOptions(EntityFrameworkStorageOptions options = null)
        {
            if (options == null) options = new EntityFrameworkStorageOptions();
            options.UpdateSchema = _updateSchema;
            _updateSchema = false;
            return options;
        }

        public EntityFrameworkJobStorage GetStorageMock(
            Func<Mock<EntityFrameworkJobStorage>, EntityFrameworkJobStorage> func,
            EntityFrameworkStorageOptions options = null)
        {
            var mock = new Mock<EntityFrameworkJobStorage>(ProviderType, GetConnectionString(),
                ProgressOptions(options));
            return func(mock);
        }

        public abstract void OnDispose();
    }
}