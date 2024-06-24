using System;

namespace Hangfire.EntityFrameworkStorage.Entities
{
    public class _Server : HFEntity
    {
        public virtual string Id { get; set; }
        public virtual string Data { get; set; } = string.Empty;
        public virtual DateTime? LastHeartbeat { get; set; }
    }
}