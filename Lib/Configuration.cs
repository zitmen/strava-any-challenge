using Microsoft.Extensions.Configuration;

namespace Lib
{
    public static class Configuration
    {
        public static void OverrideConfiguration(string environmentName, string applicationRootPath)
        {
            // this is a hack, but it works; it could be achieved slightly cleaner with the Options pattern, it that would be a hack too
            // https://github.com/Azure/azure-functions-core-tools/issues/2070
            if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                var configurationOverrides = new ConfigurationBuilder()
                    .AddJsonFile(Path.Combine(applicationRootPath, "local.settings.overrides.json"), optional: true, reloadOnChange: false)
                    .Build();

                var values = configurationOverrides.GetSection("Values").AsEnumerable(makePathsRelative: true).ToArray();
                foreach (var value in values)
                {
                    Environment.SetEnvironmentVariable(value.Key, value.Value);
                }
            }
        }
    }
}
