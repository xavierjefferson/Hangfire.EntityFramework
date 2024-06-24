using Snork.EntityFrameworkTools;

namespace Hangfire.EntityFrameworkStorage
{
    public static class EntityFrameworkStorageBootstrapperConfigurationExtensions
    {
        /// <summary>
        ///     Tells the bootstrapper to use EntityFramework provider as a job storage,
        ///     that can be accessed using the given connection string or
        ///     its name.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="nameOrConnectionString">Connection string or its name</param>
        /// <param name="providerType">Provider type from enumeration</param>
        /// <param name="options">Advanced options</param>
        public static IGlobalConfiguration UseEntityFrameworkJobStorage(
            this IGlobalConfiguration configuration,
            string nameOrConnectionString, ProviderTypeEnum providerType, EntityFrameworkStorageOptions options = null)
        {
            var storage = EntityFrameworkStorageFactory.For(providerType, nameOrConnectionString,
                options ?? new EntityFrameworkStorageOptions());
            configuration.UseStorage(storage);
            return configuration;
        }
    }
}