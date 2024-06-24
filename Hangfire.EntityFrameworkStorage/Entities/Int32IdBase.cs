using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public abstract class Int32IdBase : EntityBase, IInt32Id
{
    public virtual int Id { get; set; }
}