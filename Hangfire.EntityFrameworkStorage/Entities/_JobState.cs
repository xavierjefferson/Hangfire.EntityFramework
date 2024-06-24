using System;
using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _JobState : Int32IdBase, IJobChild, ICreatedAt
{
    public virtual string Name { get; set; }
    public virtual string Reason { get; set; }
    public virtual string Data { get; set; }
    public virtual long CreatedAt { get; set; }
    public virtual _Job Job { get; set; }
}