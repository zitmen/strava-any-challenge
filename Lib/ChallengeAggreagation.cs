using MongoDB.Bson;
using MongoDB.Driver;

namespace Lib
{
    public static class ChallengeAggreagation
    {
        public static DbDtos.ChallengeState GetChallengeState(DateTime now, DateTime from, DateTime to)
            => now > to.AddDays(+1)
                ? DbDtos.ChallengeState.Past
                : now < from.AddDays(-1)
                    ? DbDtos.ChallengeState.Upcoming
                    : DbDtos.ChallengeState.Current;

        public static async Task<Dictionary<ulong, DbDtos.AthleteChallengeStats>> CalculateChallengeAthletesStats(IMongoDatabase database, DbDtos.Challenge challenge, Dictionary<ulong, DbDtos.Athlete> athletes)
        {
            var dbAthletes = database.GetCollection<DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);
            var dbActivities = database.GetCollection<DbDtos.Activity>(MongoDbClientFactory.CollectionActivitiesName);

            var activities = await dbActivities
                .Aggregate(CreateChallengeActivitiesAggregationPipeline(challenge, athletes.Keys), new AggregateOptions(), CancellationToken.None)
                .ToListAsync(CancellationToken.None);

            var challengeAthletesStats = activities
                .GroupBy(a => a.AthleteId)
                .Select(grp => new { AthleteId = grp.Key, Items = grp.ToArray() })
                .Select(grp => new DbDtos.AthleteChallengeStats(
                    grp.AthleteId, athletes[grp.AthleteId].Username, athletes[grp.AthleteId].AvatarSmallUrl, grp.Items.Length, TimeSpan.FromSeconds(grp.Items.Sum(a => (long)a.ElapsedTime)),
                    TimeSpan.FromSeconds(grp.Items.Sum(a => (long)a.MovingTime)), grp.Items.Sum(a => a.KiloJoules), grp.Items.Sum(a => a.KiloCalories), grp.Items.Sum(a => a.Distance), grp.Items))
                .ToDictionary(a => a._id, a => a);

            return challengeAthletesStats;
        }

        private static PipelineDefinition<DbDtos.Activity, DbDtos.ActivitySummary> CreateChallengeActivitiesAggregationPipeline(DbDtos.Challenge challenge, IReadOnlyCollection<ulong> participatingAthletesIds)
        {
            return PipelineDefinition<DbDtos.Activity, DbDtos.ActivitySummary>.Create(
                new BsonDocument("$match",
                    new BsonDocument
                    {
                    {
                        "startDateLocal",
                        new BsonDocument
                        {
                            { "$gte", challenge.From },
                            { "$lt", challenge.To.AddDays(1).Date } // midnight of the next day
                        }
                    },
                    {
                        "athleteId",
                        new BsonDocument("$in", new BsonArray(participatingAthletesIds))
                    },
                    {
                        "type",
                        new BsonDocument("$in", new BsonArray(challenge.ActivityTypes.Select(at => at.ToString())))
                    },
                    }),
                new BsonDocument("$project",
                    new BsonDocument
                    {
                    { "_id", "$_id" },
                    { "athleteId", "$athleteId" },
                    { "name", "$name" },
                    { "distance", "$distance" },
                    { "movingTime", "$movingTime" },
                    { "elapsedTime", "$elapsedTime" },
                    { "type", "$type" },
                    { "startDate", "$startDate" },
                    { "startDateLocal", "$startDateLocal" },
                    { "timezone", "$timezone" },
                    { "utcOffset", "$utcOffset" },
                    { "kiloJoules", "$kiloJoules" },
                    { "kiloCalories", "$kiloCalories" },
                    }));
        }
    }
}
