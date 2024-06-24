﻿using System.Reflection;
using Hangfire.EntityFrameworkStorage.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.Fixtures;

public abstract class DatabaseFixtureBase : IDisposable
{
    private bool _updateSchema = true; //only create schema on the first pass


    protected static string Instance => Guid.NewGuid().ToString();

    public abstract Action<DbContextOptionsBuilder> DbContextOptionsBuilderAction { get; }


    public void Dispose()
    {
        OnDispose();
    }

    public abstract void Cleanup();

    public EntityFrameworkJobStorage GetStorage(Action<DbContextOptionsBuilder> action,
        EntityFrameworkStorageOptions? options = null)
    {
        return new EntityFrameworkJobStorage(action, ProgressOptions(options));
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


    public async Task CleanTables(DbContext dbContext)
    {
        await dbContext.DeleteAllAsync<_JobState>();
        await dbContext.DeleteAllAsync<_JobParameter>();
        await dbContext.DeleteAllAsync<_JobQueue>();
        await dbContext.DeleteAllAsync<_Job>();
        await dbContext.DeleteAllAsync<_Hash>();
        await dbContext.DeleteAllAsync<_Set>();
        await dbContext.DeleteAllAsync<_List>();
        await dbContext.DeleteAllAsync<_DistributedLock>();
        await dbContext.DeleteAllAsync<_AggregatedCounter>();
        await dbContext.DeleteAllAsync<_Counter>();
        await dbContext.DeleteAllAsync<_Server>();
    }

    protected string GetTempPath()
    {
        return Path.Combine(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name, Instance);
    }

    public abstract void CreateDatabase();

    private EntityFrameworkStorageOptions ProgressOptions(EntityFrameworkStorageOptions? options = null)
    {
        if (options == null) options = new EntityFrameworkStorageOptions();
        options.UpdateSchema = _updateSchema;
        _updateSchema = false;
        return options;
    }

    public EntityFrameworkJobStorage GetStorageMock(
        Func<Mock<EntityFrameworkJobStorage>, EntityFrameworkJobStorage> func,
        EntityFrameworkStorageOptions? options = null)
    {
        var mock = new Mock<EntityFrameworkJobStorage>(DbContextOptionsBuilderAction,
            ProgressOptions(options));
        return func(mock);
    }

    public abstract void OnDispose();
}