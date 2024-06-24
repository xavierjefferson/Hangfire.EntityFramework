using System;
using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public abstract class CounterBase : EntityBase, IExpirableWithId, IInt32Id, IStringKey, IIntValue
{
    public virtual long? ExpireAt { get; set; }
    public virtual int Id { get; set; }
    public virtual int Value { get; set; }
    public virtual string Key { get; set; }
}