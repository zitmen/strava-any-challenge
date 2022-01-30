using Azure.Messaging.EventGrid;
using Functions;
using Lib;
using Lib.DbDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Sync.StravaDto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Sync.StravaSync
{
    public sealed class EntryPoints : IDisposable
    {
        private readonly IMongoClient _mongoClient;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public EntryPoints(IMongoClient mongoClient, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _mongoClient = mongoClient;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        [FunctionName(nameof(KeepAlive))]
        public async Task KeepAlive([TimerTrigger("0 */10 * * * *")] TimerInfo timer)
        {
            // ref: https://github.com/Azure/azure-functions-host/issues/3965#issuecomment-706377613
            //
            // the function app is decomissioned after 20 minutes of inactivity and then it has to be c\old-started
            // the cold start takes ~10s, which is a problem because the strava webhook expects toget a response within 2s
            // interestingly enough, even with a warn start, the response time is quite random, some times it takes 2s,
            // other times 500ms, other times 150ms....weird...more often it is called, the better the response time
            // by simple measurement, the response time is 500ms and the actual function execution time is just 100ms
            //
            // a similar issue might be happening with the event grid subscriptions, which are failing to deliver messages,
            // likely because of this; afaik, event grid expects delivery time of max. 30s; then it exponentially backs off
            // but overall, it's pretty weird because even with warming the function app up, the event grid triggers fail
            // the first delivery and succeed on the second one a minute later
            //
            // calling an empty function seems not to help as the initial webhook call still takes ~5s and some of the event grid triggers
            // take few retries to start the functions; so let's trigger the whole processing chain from here

            // get the strava webhook subscription, if it has been registered
            var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
            var syncIds = database.GetCollection<WebhookSubscriptionDto>(MongoDbClientFactory.CollectionWebhooksName);
            var syncId = await syncIds.Find(Builders<WebhookSubscriptionDto>.Filter.Empty).FirstOrDefaultAsync();
            if (syncId == null) return;

            // trigger a fake sync to warm-up the processing chain
            var push = new WebhookPush("update", 0UL, 0UL, "activity", 0L, syncId._id);
            await _httpClient.PostAsJsonAsync($"https://{Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")}/api/Webhook", push);
        }

        [FunctionName(nameof(UpdateChallengesStates))]
        public async Task UpdateChallengesStates([TimerTrigger("0 0 0 * * *")] TimerInfo timer) // every day at midnight
        {
            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
                var dbChallenges = database.GetCollection<Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var challenges = await dbChallenges.Find(Builders<Challenge>.Filter.Empty).ToListAsync(CancellationToken.None);

                var now = DateTime.UtcNow;
                var stateUpdates = challenges
                    .Select(ch => (Id: ch._id, CurrentState: ch.State, CalculatedState: ChallengeAggreagation.GetChallengeState(now, ch.From, ch.To)))
                    .Where(ch => ch.CurrentState != ch.CalculatedState)
                    .ToArray();

                var setToCurrent = stateUpdates.Where(ch => ch.CalculatedState == ChallengeState.Current).Select(ch => ch.Id).ToHashSet();
                var setToPast = stateUpdates.Where(ch => ch.CalculatedState == ChallengeState.Past).Select(ch => ch.Id).ToHashSet();
                var setToUpcoming = stateUpdates.Where(ch => ch.CalculatedState == ChallengeState.Upcoming).Select(ch => ch.Id).ToHashSet();

                await Task.WhenAll(
                    UpdateChallengesStates(dbChallenges, setToPast, ChallengeState.Past),
                    UpdateChallengesStates(dbChallenges, setToCurrent, ChallengeState.Current),
                    UpdateChallengesStates(dbChallenges, setToUpcoming, ChallengeState.Upcoming));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(UpdateChallengesStates)} failed");
                throw;
            }
        }

        [FunctionName(nameof(SyncAllAthletesActivities) + "TimeTrigger")]
        public async Task SyncAllAthletesActivities(
            [TimerTrigger("0 0 0 1 * *")] TimerInfo timer,  // every first day of a month at midnight
            [EventGrid(TopicEndpointUri = "EventGridSyncTopicUriSetting", TopicKeySetting = "EventGridSyncTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
        {
            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
                await SyncAllAthletesActivitiesInternal(database, outputEvents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(SyncAllAthletesActivities)} failed");
                throw;
            }
        }

        [FunctionName(nameof(SyncAllAthletesActivities))]
        public async Task<IActionResult> SyncAllAthletesActivities(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest request,
            [EventGrid(TopicEndpointUri = "EventGridSyncTopicUriSetting", TopicKeySetting = "EventGridSyncTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
        {
            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
                
                try
                {
                    await Authentication.AuthenticateAdminRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                await SyncAllAthletesActivitiesInternal(database, outputEvents);
                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(SyncAllAthletesActivities)} failed");
                throw;
            }
        }

        // From strava docs: https://developers.strava.com/docs/webhooks/
        // The subscription callback endpoint must acknowledge the POST of each new event with a status code of 200 OK
        // within two seconds. Event pushes are retried (up to a total of three attempts) if a 200 is not returned.
        // If your application needs to do more processing of the received information, it should do so asynchronously.
        [FunctionName(nameof(WebhookSync))]
        public async Task<IActionResult> WebhookSync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Webhook")] HttpRequest request,
            [EventGrid(TopicEndpointUri = "EventGridSyncTopicUriSetting", TopicKeySetting = "EventGridSyncTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
        {
            try
            {
                var requestBody = await request.ReadRequestBody();
                var pushNotification = JsonConvert.DeserializeObject<WebhookPush>(requestBody)!;
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
                // we sync only when the subscription ID is correct...a kind of a primitive authentication
                var syncIds = database.GetCollection<WebhookSubscriptionDto>(MongoDbClientFactory.CollectionWebhooksName);
                if (!await syncIds.Find(ws => ws._id == pushNotification.subscription_id).AnyAsync(CancellationToken.None))
                    return new UnauthorizedResult();

                // we sync only when an activity in pushed
                if (pushNotification.object_type != "activity")
                    return new OkResult();

                await SyncActivityInternal(pushNotification.owner_id, pushNotification.object_id, pushNotification.aspect_type, outputEvents);

                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(WebhookSync)} failed");
                throw;
            }
        }

        [FunctionName(nameof(Sync))]
        public async Task<IActionResult> Sync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            [EventGrid(TopicEndpointUri = "EventGridSyncTopicUriSetting", TopicKeySetting = "EventGridSyncTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
        {
            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                Lib.DbDtos.Athlete athlete;
                try
                {
                    athlete = await Authentication.AuthenticateRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                await SyncAthleteInternal(athlete._id, database, outputEvents);

                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(Sync)} failed");
                throw;
            }
        }

        [FunctionName(nameof(Recalc))]
        public async Task<IActionResult> Recalc(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            [EventGrid(TopicEndpointUri = "EventGridSyncTopicUriSetting", TopicKeySetting = "EventGridSyncTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
        {
            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                Lib.DbDtos.Athlete athlete;
                try
                {
                    athlete = await Authentication.AuthenticateRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                var @event = new EventGridEvent(Constants.Events.SyncRecalculateChallengeSubject, Constants.Events.SyncRecalculateChallengeType, Constants.Events.SyncRecalculateChallengeVersion, new SyncAthleteEvent(athlete._id, Constants.AlwaysRecalculate));
                await outputEvents.AddAsync(@event);
                await outputEvents.FlushAsync(CancellationToken.None);

                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(Sync)} failed");
                throw;
            }
        }

        private static Task UpdateChallengesStates(IMongoCollection<Challenge> dbChallenges, IReadOnlyCollection<ObjectId> ids, ChallengeState state)
            => ids.Count == 0
                ? Task.CompletedTask
                : dbChallenges.UpdateOneAsync(
                    ch => ids.Contains(ch._id),
                    Builders<Challenge>.Update.Set(ch => ch.State, state),
                    new UpdateOptions() { IsUpsert = false },
                    CancellationToken.None);

        private async Task SyncAllAthletesActivitiesInternal(IMongoDatabase database, IAsyncCollector<EventGridEvent> outputEvents)
        {
            try
            {
                var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);
                var athletes = await dbAthletes.Find(Builders<Lib.DbDtos.Athlete>.Filter.Empty).ToListAsync(CancellationToken.None);

                var syncId = ObjectId.GenerateNewId();
                var dbSync = database.GetCollection<Lib.DbDtos.Sync>(MongoDbClientFactory.CollectionSyncName);
                await dbSync.InsertOneAsync(new Lib.DbDtos.Sync(syncId, athletes.Select(a => a._id).ToArray(), Array.Empty<ulong>(), DateTime.UtcNow), new InsertOneOptions(), CancellationToken.None);

                var events = athletes.Select((a, i) => new EventGridEvent(Constants.Events.SyncAthleteActivitiesSubject, Constants.Events.SyncAthleteActivitiesType, Constants.Events.SyncAthleteActivitiesVersion, new SyncAthleteEvent(a._id, syncId.ToString())));
                try
                {
                    await Task.WhenAll(events.Select(e => outputEvents.AddAsync(e)));
                }
                finally
                {
                    await outputEvents.FlushAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(SyncAllAthletesActivitiesInternal)} failed");
                throw;
            }
        }

        private static async Task SyncAthleteInternal(ulong athleteId, IMongoDatabase database, IAsyncCollector<EventGridEvent> outputEvents)
        {
            var syncId = ObjectId.GenerateNewId();
            var dbSync = database.GetCollection<Lib.DbDtos.Sync>(MongoDbClientFactory.CollectionSyncName);
            await dbSync.InsertOneAsync(new Lib.DbDtos.Sync(syncId, new ulong[] { athleteId }, Array.Empty<ulong>(), DateTime.UtcNow), new InsertOneOptions(), CancellationToken.None);

            var @event = new EventGridEvent(Constants.Events.SyncAthleteActivitiesSubject, Constants.Events.SyncAthleteActivitiesType, Constants.Events.SyncAthleteActivitiesVersion, new SyncAthleteEvent(athleteId, syncId.ToString()));
            await outputEvents.AddAsync(@event);
            await outputEvents.FlushAsync(CancellationToken.None);
        }

        private static async Task SyncActivityInternal(ulong athleteId, ulong activityId, string operationType, IAsyncCollector<EventGridEvent> outputEvents)
        {
            if (athleteId == 0UL) return; // the warm-up trigger

            var eventData = new SyncActivityEvent(athleteId, activityId, Enum.Parse<ActivitySyncType>(operationType));
            var @event = new EventGridEvent(Constants.Events.SyncActivitySubject, Constants.Events.SyncActivityType, Constants.Events.SyncActivityVersion, eventData);
            await outputEvents.AddAsync(@event);
            await outputEvents.FlushAsync(CancellationToken.None);
        }
    }
}