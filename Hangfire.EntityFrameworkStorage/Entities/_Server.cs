using System;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _Server : EntityBase
{
    public virtual string Id { get; set; }
    public virtual string Data { get; set; } = string.Empty;
    public virtual long? LastHeartbeat { get; set; }
}