namespace Hangfire.EntityFrameworkStorage.Entities
{
    public interface IJobChild
    {
        _Job Job { get; }
    }
}