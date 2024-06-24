using System;
using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _JobQueue : Int32IdBase, IJobChild, IFetchedAtNullable
{
    public virtual string Queue { get; set; }
    public virtual string FetchToken { get; set; }
    public virtual long? FetchedAt { get; set; }
    public virtual _Job Job { get; set; }
}