using System;
using System.Collections.Generic;
using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _Job : EntityBase, ICreatedAt, IExpirable, IExpirableWithStringId
{
    public virtual string InvocationData { get; set; }

    public virtual string Arguments { get; set; }

    public virtual ICollection<_JobParameter> Parameters { get; set; } = new HashSet<_JobParameter>();
    public virtual ICollection<_JobState> History { get; set; } = new HashSet<_JobState>();
    public virtual string StateName { get; set; }
    public virtual string StateReason { get; set; }
    public virtual long? LastStateChangedAt { get; set; }
    public virtual string StateData { get; set; }
    public virtual long CreatedAt { get; set; }
    public virtual long? ExpireAt { get; set; }


    public virtual string Id { get; set; } = Guid.NewGuid().ToString();
}