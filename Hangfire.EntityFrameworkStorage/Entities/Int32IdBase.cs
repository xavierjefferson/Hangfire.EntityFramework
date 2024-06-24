namespace Hangfire.EntityFrameworkStorage.Entities;

public abstract class Int32IdBase : HFEntity, IInt32Id
{
    public virtual int Id { get; set; }
}