using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Transactions;
using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.Logging;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkStorage;

internal static class SqlUtil
{
    

    private const int DeleteBatchSize = 250;
    private static readonly ILog Logger = LogProvider.GetLogger(typeof(SqlUtil));


#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif
#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif


    /// <summary>
    ///     do an upsert into a table
    /// </summary>
    /// <typeparam name="T">The entity type to upsert</typeparam>
    /// <param name="dbContext"></param>
    /// <param name="matchFunc">A function that returns a single instance of T</param>
    /// <param name="changeAction">A delegate that changes specified properties of instance of T </param>
    /// <param name="keysetAction">A delegate that sets the primary key properties of instance of T if we have to do an upsert</param>
    public static void UpsertEntity<T>(this DbContext dbContext, Expression<Func<T, bool>> matchFunc,
        Action<T> changeAction,
        Action<T> keysetAction) where T : HFEntity, new()
    {
        var entity = dbContext.Set<T>().SingleOrDefault(matchFunc);
        if (entity == null)
        {
            entity = new T();
            keysetAction(entity);
            changeAction(entity);
            dbContext.Add(entity);
            dbContext.SaveChanges();
        }
        else
        {
            changeAction(entity);
            dbContext.Update(entity);
            dbContext.SaveChanges();
        }
    }

#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif
    /// <summary>
    ///     delete entities that implement IInt32Id, by using the value stored in their Id property.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dbContext">Sessionwrapper instance to act upon</param>
    /// <param name="ids">Collection of ids to delete</param>
    /// <returns>the number of rows deleted</returns>
    public static long DeleteById<T, U>(this HangfireContext dbContext, ICollection<U> ids)
        where T : HFEntity, IWithID<U>
    {
        if (!ids.Any())
            return 0;

        var count = 0;
        for (var i = 0; i < ids.Count; i += DeleteBatchSize)
        {
            var batch = ids.Skip(i).Take(DeleteBatchSize).ToList();
            dbContext.RemoveRange(dbContext.Set<T>().Where(j => batch.Contains(j.Id)));
            count += dbContext.SaveChanges();
        }

        return count;
    }

#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif


#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif
    public static T WrapForTransaction<T>(Func<T> safeFunc)
    {
        try
        {
            return safeFunc();
        }
        catch (DBConcurrencyException)
        {
            //do nothing
        }
        catch (TransactionException)
        {
            //do nothing
        }

        return default;
    }

#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif


    public static T WrapForDeadlock<T>(CancellationToken cancellationToken, Func<T> safeAction,
        EntityFrameworkStorageOptions options)
    {
        while (true)
            try
            {
                return safeAction();
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("deadlock", StringComparison.InvariantCultureIgnoreCase) < 0)
                    throw;

                cancellationToken.PollForCancellation(options.DeadlockRetryInterval);
            }
    }
#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif

    public static void WrapForDeadlock(CancellationToken cancellationToken, Action safeAction,
        EntityFrameworkStorageOptions options)
    {
        WrapForDeadlock(cancellationToken, () =>
        {
            safeAction();
            return true;
        }, options);
    }

#if !DEBUG
[System.Diagnostics.DebuggerHidden]
#endif
    public static void WrapForTransaction(Action safeAction)
    {
        WrapForTransaction(() =>
        {
            safeAction();
            return true;
        });
    }
}