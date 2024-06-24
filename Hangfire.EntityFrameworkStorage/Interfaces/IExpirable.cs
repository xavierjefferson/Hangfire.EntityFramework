using System;

namespace Hangfire.EntityFrameworkStorage.Interfaces;

public interface IExpirable
{
    long? ExpireAt { get; set; }
}