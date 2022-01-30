using Functions;
using Lib;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Sync.StravaSync;

// the event grid triggers are not working very well, so let's use http triggers instead; they work well
public sealed class ChallengeAggregation : IDisposable
{
    private readonly IMongoClient _mongoClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly StravaHttpClient _stravaClient;

    public ChallengeAggregation(IMongoClient mongoClient, IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _mongoClient = mongoClient;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _stravaClient = new StravaHttpClient(mongoClient, httpClientFactory, logger);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _stravaClient.Dispose();
    }

    [FunctionName(nameof(RecalculateChallengeWebhook))]
    public async Task<IActionResult> RecalculateChallengeWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request)
    {
        try
        {
            if (!request.IsAuthorized())
                return new UnauthorizedResult();

            var requestBody = await request.ReadRequestBody();
            if (WebhooksVerification.TryValidateWebhookSubscriptionRequest(request.Headers, requestBody, out var response))
                return response;

            if (string.Equals(request.Headers["aeg-event-type"], "Notification", StringComparison.OrdinalIgnoreCase))
            {
                var events = JsonConvert.DeserializeObject<EventGridWebhook<SyncAthleteEvent>[]>(requestBody)!
                    .Where(e => string.Equals(e.subject, Constants.Events.SyncRecalculateChallengeSubject, StringComparison.OrdinalIgnoreCase))
                    .Where(e => string.Equals(e.eventType, Constants.Events.SyncRecalculateChallengeType, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(e => e.data.AthleteId)
                    .SelectMany(grp => grp.DistinctBy(g => g.data.SyncId).Select(d => d.data))
                    .ToArray();

                await Task.WhenAll(events.Select(RecalculateChallengeInternal));
                return new OkResult();
            }

            _logger.LogWarning("Unknown event grid webhook event type: {0}", request.Headers["aeg-event-type"]);
            return new BadRequestResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(RecalculateChallengeWebhook)} failed");
            throw;
        }
    }

    private async Task RecalculateChallengeInternal(SyncAthleteEvent syncEvent)
    {
        // check if we can run the recalculation already
        var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
        var dbSync = database.GetCollection<Lib.DbDtos.Sync>(MongoDbClientFactory.CollectionSyncName);
        
        try
        {
            if (!string.Equals(syncEvent.SyncId, Constants.AlwaysRecalculate, StringComparison.OrdinalIgnoreCase))
            {
                var sync = await dbSync.Find(s => s._id == ObjectId.Parse(syncEvent.SyncId)).SingleOrDefaultAsync(CancellationToken.None);
                if (sync == null || !sync.AthletesToSync.ToHashSet().SetEquals(sync.SyncedAthletes))
                    return;
            }

            // perform the aggregation - let's be naive and sync everything at once...this could be certainly optimized as we could update only certain parts of certain challenges
            var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
            var challenges = await dbChallenges.Find(Builders<Lib.DbDtos.Challenge>.Filter.Empty).ToListAsync(CancellationToken.None);
            
            await Task.WhenAll(challenges.Select(ch => RecalculateOneChallenge(database, dbChallenges, ch)));
        }
        finally
        {
            if (!string.Equals(syncEvent.SyncId, Constants.AlwaysRecalculate, StringComparison.OrdinalIgnoreCase))
            {
                var result = await dbSync.DeleteOneAsync(s => s._id == ObjectId.Parse(syncEvent.SyncId), CancellationToken.None);
            }
        }
    }

    private async Task RecalculateOneChallenge(IMongoDatabase database, IMongoCollection<Lib.DbDtos.Challenge> dbChallenges, Lib.DbDtos.Challenge challenge)
    {
        var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);
        var participatingAthletesIds = challenge.AthletesStats.Select(a => a._id).ToHashSet();
        var athletes = (await dbAthletes.Find(a => participatingAthletesIds.Contains(a._id)).ToListAsync(CancellationToken.None)).ToDictionary(a => a._id, a => a);
        
        var challengeAthletesStats = await ChallengeAggreagation.CalculateChallengeAthletesStats(database, challenge, athletes);

        var athletesWithNoStats = athletes
            .Where(a => !challengeAthletesStats.ContainsKey(a.Value._id))
            .Select(a => new Lib.DbDtos.AthleteChallengeStats(a.Value._id, a.Value.Username, a.Value.AvatarUrl));

        var result = await dbChallenges.UpdateOneAsync(
            ch => ch._id == challenge._id,
            Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.AthletesStats, challengeAthletesStats.Values.Concat(athletesWithNoStats).ToArray()),
            new UpdateOptions() { IsUpsert = true },
            CancellationToken.None);
    }
}