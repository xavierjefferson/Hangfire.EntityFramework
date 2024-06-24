using System;

namespace Hangfire.EntityFrameworkStorage.Interfaces;

public interface ICreatedAt
{
    long CreatedAt { get; set; }
}