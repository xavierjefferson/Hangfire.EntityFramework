using System;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Moq;

namespace Hangfire.EntityFrameworkStorage.Tests.Base
{
    public abstract class TestBase : IDisposable
    {
        private bool _disposedValue;
        private EntityFrameworkJobStorage _storage;

        protected TestBase(DatabaseFixtureBase fixture)
        {
            Fixture = fixture;
        }

        protected DatabaseFixtureBase Fixture { get; }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TestBase()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public virtual EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions options = null)
        {
            if (_storage == null)
            {
                _storage = Fixture.GetStorage(options);

                return _storage;
            }

            return _storage;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // CleanTables(GetStorage());}
                    // TODO: dispose managed state (managed objects)
                }


                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }


        protected void UseJobStorageConnectionWithSession(
            Action<StatelessSessionWrapper, EntityFrameworkJobStorageConnection> action)
        {
            UseJobStorageConnection(jobStorageConnection =>
            {
                jobStorageConnection.Storage.UseStatelessSession(s => action(s, jobStorageConnection));
            });
        }

        protected void UseJobStorageConnection(Action<EntityFrameworkJobStorageConnection> action,
            bool cleanTables = true, EntityFrameworkStorageOptions options = null)
        {
            var fluentNHibernateJobStorage = GetStorage(options);
            if (cleanTables)
                Fixture.CleanTables(fluentNHibernateJobStorage.GetStatelessSession());
            using (var jobStorage = new EntityFrameworkJobStorageConnection(fluentNHibernateJobStorage))
            {
                action(jobStorage);
            }
        }

        protected EntityFrameworkJobStorage GetStorageMock(
            Func<Mock<EntityFrameworkJobStorage>, EntityFrameworkJobStorage> func,
            EntityFrameworkStorageOptions options = null)
        {
            return Fixture.GetStorageMock(func, options);
        }

        public static _Job InsertNewJob(StatelessSessionWrapper session, Action<_Job> action = null)
        {
            var newJob = new _Job
            {
                InvocationData = string.Empty,
                Arguments = string.Empty,
                CreatedAt = session.Storage.UtcNow
            };
            action?.Invoke(newJob);
            session.Insert(newJob);

            return newJob;
        }
    }
}