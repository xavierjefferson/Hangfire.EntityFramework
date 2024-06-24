using Hangfire.EntityFrameworkStorage.Entities;

namespace Hangfire.EntityFrameworkStorage.Interfaces;

public interface IJobChild
{
    _Job Job { get; }
}