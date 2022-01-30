using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Lib;

public static class MongoDbClientFactory
{
    public const string DbName = "anychallenge";
    public const string CollectionSyncName = "sync";
    public const string CollectionAthletesName = "athletes";
    public const string CollectionActivitiesName = "activities";
    public const string CollectionChallengesName = "challenges";
    public const string CollectionWebhooksName = "webhooks";

    static MongoDbClientFactory()
    {
        var conventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register(
            name: "CustomConventionPack",
            conventions: conventionPack,
            filter: t => true);
    }

    public static MongoClient Create()
    {
        var client = new MongoClient(Environment.GetEnvironmentVariable("DocumentDBConnection"));
        return client;
    }
}
