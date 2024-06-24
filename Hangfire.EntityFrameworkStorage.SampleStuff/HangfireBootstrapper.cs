using System;
using Hangfire.EntityFrameworkStorage;
using Microsoft.EntityFrameworkCore;


namespace Hangfire.EntityFrameworkStorage.SampleStuff
{
    public static class HangfireBootstrapper
    {
        public static IGlobalConfiguration SetupJobStorage(this IGlobalConfiguration globalConfiguration,
            ISqliteTempFileService sqliteTempFileService)
        {
            return globalConfiguration.UseEntityFrameworkJobStorage(i =>
            {
                i.UseSqlite(sqliteTempFileService.GetConnectionString());
            });
        }

        public static IGlobalConfiguration SetupActivator(
            this IGlobalConfiguration globalConfiguration, IServiceProvider serviceProvider)
        {
            return globalConfiguration.UseActivator(new HangfireActivator(serviceProvider));
        }
    }
}