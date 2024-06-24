using Hangfire.EntityFrameworkStorage.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hangfire.EntityFrameworkStorage.Tests;

public static class DbContextExtensions
{
    public static async Task DeleteAllAsync<T>(this DbContext context) where T : EntityBase
    {
        context.RemoveRange(context.Set<T>());
        await context.SaveChangesAsync();
    }

}