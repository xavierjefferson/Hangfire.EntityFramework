using System;
using Hangfire.EntityFrameworkStorage.Extensions;
using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _DistributedLock : Int32IdBase, ICreatedAt
{
    public _DistributedLock()
    {
        CreatedAt = DateTime.UtcNow.ToEpochDate();
        ExpireAt = DateTime.UtcNow.ToEpochDate();
    }

    /// <summary>
    ///     This is a long integer because some db engines' default storage for dates
    ///     doesn't have accuracy smaller than 1 second.
    /// </summary>
    public virtual long ExpireAt { get; set; }

    //this column is just for debugging

    public virtual string Resource { get; set; }
    public virtual long CreatedAt { get; set; }
}