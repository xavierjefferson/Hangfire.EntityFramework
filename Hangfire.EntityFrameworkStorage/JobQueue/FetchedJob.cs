namespace Hangfire.EntityFrameworkStorage.JobQueue;

public class FetchedJob
{
    public long Id { get; set; }
    public string JobId { get; set; }
    public string Queue { get; set; }
}