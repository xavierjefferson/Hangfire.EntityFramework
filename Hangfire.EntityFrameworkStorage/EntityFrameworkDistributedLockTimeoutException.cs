using System;

namespace Hangfire.EntityFrameworkStorage;

public class EntityFrameworkDistributedLockTimeoutException : Exception
{
    public EntityFrameworkDistributedLockTimeoutException(string message) : base(message)
    {
    }
}