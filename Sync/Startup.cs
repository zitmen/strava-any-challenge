using Lib;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Net;
using System.Net.Http;

[assembly: FunctionsStartup(typeof(Functions.Startup))]
namespace Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var context = builder.GetContext();
            Configuration.OverrideConfiguration(context.EnvironmentName, context.ApplicationRootPath);

            //builder.Services.AddLogging(); -> this should be already registered out of the box
            builder.Services.AddSingleton<ILogger>(provider => provider.GetRequiredService<ILoggerFactory>().CreateLogger(string.Empty));
            builder.Services.AddSingleton<IMongoClient>(_ => MongoDbClientFactory.Create());

            builder.Services
                .AddHttpClient(string.Empty)
                .ConfigurePrimaryHttpMessageHandler(provider =>
                    new SocketsHttpHandler
                    {
                        UseCookies = false,
                        MaxConnectionsPerServer = 10,
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                        AutomaticDecompression = DecompressionMethods.All,
                    });
        }
    }
}