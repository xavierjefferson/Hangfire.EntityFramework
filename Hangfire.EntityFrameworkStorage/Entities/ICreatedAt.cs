using System;

namespace Hangfire.EntityFrameworkStorage.Entities;

public interface ICreatedAt
{
    DateTime CreatedAt { get; set; }
}