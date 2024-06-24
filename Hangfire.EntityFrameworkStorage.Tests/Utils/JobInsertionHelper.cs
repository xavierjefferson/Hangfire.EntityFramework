using Hangfire.EntityFrameworkStorage.Entities;
using Hangfire.EntityFrameworkStorage.Extensions;

namespace Hangfire.EntityFrameworkStorage.Tests;

internal static class JobInsertionHelper
{
    public static _Job InsertNewJob(HangfireContext dbContext, Action<_Job>? action = null)
    {
        var newJob = new _Job
        {
            InvocationData = string.Empty,
            Arguments = string.Empty,
            CreatedAt = DateTime.UtcNow.ToEpochDate()
        };
        action?.Invoke(newJob);
        dbContext.Add(newJob);
        dbContext.SaveChanges();

        return newJob;
    }
}