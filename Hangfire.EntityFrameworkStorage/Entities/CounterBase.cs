using System;

namespace Hangfire.EntityFrameworkStorage.Entities
{
    public abstract class HFEntity
    {

    }
    public abstract class CounterBase : HFEntity, IExpirableWithId, IInt32Id, IStringKey, IIntValue
    {
        public virtual DateTime? ExpireAt { get; set; }
        public virtual int Id { get; set; }
        public virtual int Value { get; set; }
        public virtual string Key { get; set; }
    }
}