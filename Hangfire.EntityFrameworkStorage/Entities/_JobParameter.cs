using Hangfire.EntityFrameworkStorage.Interfaces;

namespace Hangfire.EntityFrameworkStorage.Entities;

public class _JobParameter : Int32IdBase, IJobChild
{
    public virtual string Name { get; set; }
    public virtual string Value { get; set; }
    public virtual _Job Job { get; set; }
}