using System;
using Hangfire.EntityFrameworkStorage;
using Snork.EntityFrameworkTools;

namespace Hangfire.EntityFrameworkStorage.SampleStuff
{
    public static class HangfireBootstrapper
    {
        public static IGlobalConfiguration SetupJobStorage(this IGlobalConfiguration globalConfiguration,
            ISqliteTempFileService sqliteTempFileService)
        {
            return globalConfiguration.UseEntityFrameworkJobStorage(
                sqliteTempFileService.GetConnectionString(),
                ProviderTypeEnum.SQLite);
        }

        public static IGlobalConfiguration SetupActivator(
            this IGlobalConfiguration globalConfiguration, IServiceProvider serviceProvider)
        {
            return globalConfiguration.UseActivator(new HangfireActivator(serviceProvider));
        }
    }
}