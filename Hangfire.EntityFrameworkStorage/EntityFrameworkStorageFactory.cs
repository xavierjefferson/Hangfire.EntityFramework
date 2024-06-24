using System;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkStorage;

public sealed class EntityFrameworkStorageFactory
{
    /// <summary>
    ///     Factory method.  Return a job storage provider based on the given provider type, connection string, and options
    /// </summary>
    /// <param name="nameOrConnectionString">Connection string or its name</param>
    /// <param name="providerType">Provider type from enumeration</param>
    /// <param name="options">Advanced options</param>
    public static EntityFrameworkJobStorage For(Action<DbContextOptionsBuilder> action,
        EntityFrameworkStorageOptions options = null)
    {
        return new EntityFrameworkJobStorage(action,
            options ?? new EntityFrameworkStorageOptions());
    }


    ///// <summary>
    /////     Return an NHibernate persistence configurerTells the bootstrapper to use a EntityFramework provider as a job
    /////     storage,
    /////     that can be accessed using the given connection string or
    /////     its name.
    ///// </summary>
    ///// <param name="nameOrConnectionString">Connection string or its name</param>
    ///// <param name="providerType">Provider type from enumeration</param>
    ///// <param name="options">Advanced options</param>
    //public static IPersistenceConfigurer GetPersistenceConfigurer(ProviderTypeEnum providerType,
    //    string nameOrConnectionString,
    //    EntityFrameworkStorageOptions options = null)
    //{
    //    return EntityFrameworkPersistenceBuilder.GetPersistenceConfigurer(providerType,
    //        nameOrConnectionString, options ?? new EntityFrameworkStorageOptions());
    //}
}