using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;
using Moq;
using Nito.AsyncEx.Synchronous;

namespace Hangfire.EntityFrameworkStorage.Tests.Base;

public abstract class TestBase : IDisposable
{
    private bool _disposedValue;
    private EntityFrameworkJobStorage? _storage;

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


    public virtual EntityFrameworkJobStorage GetStorage(EntityFrameworkStorageOptions? options = null)
    {
        if (_storage == null)
        {
            _storage = Fixture.GetStorage(Fixture.DbContextOptionsBuilderAction, options);

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


    protected async Task UseJobStorageConnectionWithDbContext(
        Func<HangfireContext, EntityFrameworkJobStorageConnection, Task> action)
    {
        await UseJobStorageConnection(async jobStorageConnection =>
        {
            await Task.CompletedTask;
            jobStorageConnection.Storage.UseDbContext(dbContext =>
                action(dbContext, jobStorageConnection).WaitAndUnwrapException());
        });
    }
    protected async Task UseDbContext(
        Func<HangfireContext, Task> action)
    {
        await UseJobStorageConnection(async jobStorageConnection =>
        {
            await Task.CompletedTask;
            jobStorageConnection.Storage.UseDbContext(dbContext =>
                action(dbContext).WaitAndUnwrapException());
        });
    }

    protected async Task UseJobStorageConnection(Func<EntityFrameworkJobStorageConnection, Task> action,
        bool cleanTables = true, EntityFrameworkStorageOptions? options = null)
    {
        var storage = GetStorage(options);
        if (cleanTables)
        {
            storage.UseDbContext((context) => {   Fixture.CleanTables(context).WaitAndUnwrapException(); });
        }
        using (var jobStorage = new EntityFrameworkJobStorageConnection(storage))
        {
            await action(jobStorage);
        }
    }

    protected EntityFrameworkJobStorage GetStorageMock(
        Func<Mock<EntityFrameworkJobStorage>, EntityFrameworkJobStorage> func,
        EntityFrameworkStorageOptions? options = null)
    {
        return Fixture.GetStorageMock(func, options);
    }

    public static async Task<_Job> InsertNewJob(HangfireContext dbContext, Action<_Job>? action = null)
    {
        var newJob = new _Job
        {
            InvocationData = string.Empty,
            Arguments = string.Empty,
            CreatedAt = DateTime.UtcNow.ToEpochDate()
        };
        action?.Invoke(newJob);
        dbContext.Add(newJob);
        await dbContext.SaveChangesAsync();

        return newJob;
    }
}