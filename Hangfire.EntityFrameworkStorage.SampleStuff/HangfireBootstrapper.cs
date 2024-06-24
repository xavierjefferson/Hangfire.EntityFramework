using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Hangfire.EntityFrameworkStorage.SampleStuff;

public static class HangfireBootstrapper
{
    public static IGlobalConfiguration SetupJobStorage(this IGlobalConfiguration globalConfiguration,
        ISqliteTempFileService sqliteTempFileService)
    {
        return globalConfiguration.UseEntityFrameworkJobStorage(i =>
        {
            i.UseSqlite(sqliteTempFileService.GetConnectionString());
            i.ConfigureWarnings(x => x.Ignore(RelationalEventId.AmbientTransactionWarning));
        });
    }

    public static IGlobalConfiguration SetupActivator(
        this IGlobalConfiguration globalConfiguration, IServiceProvider serviceProvider)
    {
        return globalConfiguration.UseActivator(new HangfireActivator(serviceProvider));
    }
}