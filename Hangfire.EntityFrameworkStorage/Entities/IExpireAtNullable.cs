using System;

namespace Hangfire.EntityFrameworkStorage.Entities;

public interface IExpirable
{
    DateTime? ExpireAt { get; set; }
}