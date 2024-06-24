using System;
using Hangfire.EntityFrameworkStorage.Entities;

namespace Hangfire.EntityFrameworkStorage.Tests
{
    internal static class JobInsertionHelper
    {
        public static _Job InsertNewJob(StatelessSessionWrapper session, Action<_Job> action = null)
        {
            var newJob = new _Job
            {
                InvocationData = string.Empty,
                Arguments = string.Empty,
                CreatedAt = session.Storage.UtcNow
            };
            action?.Invoke(newJob);
            session.Insert(newJob);

            return newJob;
        }
    }
}