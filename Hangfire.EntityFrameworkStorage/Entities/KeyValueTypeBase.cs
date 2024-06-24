using System;
using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public abstract class KeyValueTypeBase<T> : Int32IdBase, IExpirableWithKey, IExpirableWithId, IExpirable,
    IStringKey
{
    public virtual T Value { get; set; }
    public virtual string Key { get; set; }
    public virtual long? ExpireAt { get; set; }
}