using System;
using System.Collections.Generic;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _Job : HFEntity, ICreatedAt, IExpirable, IExpirableWithStringId
{
    public _Job()
    {
        Parameters = new List<_JobParameter>();
        History = new List<_JobState>();
    }

    // public virtual _JobState CurrentState { get; set; }
    public virtual string InvocationData { get; set; }

    public virtual string Arguments { get; set; }

    public virtual IList<_JobParameter> Parameters { get; set; }
    public virtual IList<_JobState> History { get; set; }
    public virtual string StateName { get; set; }
    public virtual string StateReason { get; set; }
    public virtual DateTime? LastStateChangedAt { get; set; }
    public virtual string StateData { get; set; }
    public virtual DateTime CreatedAt { get; set; }


    public virtual string Id { get; set; } = Guid.NewGuid().ToString();
    public virtual DateTime? ExpireAt { get; set; }
}