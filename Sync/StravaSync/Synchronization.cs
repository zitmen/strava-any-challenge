using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading;
using MongoDB.Driver;
using System.Linq;
using MongoDB.Bson;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Azure.Messaging.EventGrid;
using Lib;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sync.StravaSync;
using Sync.StravaDto;
using System.Collections.Generic;

namespace Functions
{
    // the event grid triggers are not working very well, so let's use http triggers instead; they work well
    public sealed class Synchronization : IDisposable
    {
        private readonly IMongoClient _mongoClient;
        private readonly ILogger _logger;
        private readonly StravaHttpClient _stravaClient;

        public Synchronization(IMongoClient mongoClient, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _mongoClient = mongoClient;
            _logger = logger;
            _stravaClient = new StravaHttpClient(mongoClient, httpClientFactory, logger);
        }

        public void Dispose()
        {
            _stravaClient.Dispose();
        }

        [FunctionName(nameof(SyncActivityWebhook))]
        public async Task<IActionResult> SyncActivityWebhook(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            [EventGrid(TopicEndpointUri = "EventGridRecalculateTopicUriSetting", TopicKeySetting = "EventGridRecalculateTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
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
                    var events = JsonConvert.DeserializeObject<EventGridWebhook<SyncActivityEvent>[]>(requestBody)!
                        .Where(e => string.Equals(e.subject, Constants.Events.SyncActivitySubject, StringComparison.OrdinalIgnoreCase))
                        .Where(e => string.Equals(e.eventType, Constants.Events.SyncActivityType, StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    try
                    {
                        await Task.WhenAll(events
                            .Where(e => e.data.SyncType == ActivitySyncType.delete)
                            .Select(e => DeleteActivityInternal(e.data)));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Deleting events failed");
                    }

                    try
                    {
                        await Task.WhenAll(events
                            .Where(e => e.data.SyncType != ActivitySyncType.delete)
                            .Select(e => FetchActivityInternal(e.data, outputEvents)));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fetching events failed");
                    }

                    try
                    {
                        await Task.WhenAll(events
                            .GroupBy(e => e.data.AthleteId)
                            .Select(e => new SyncAthleteEvent(e.Key, Constants.AlwaysRecalculate))
                            .Select(e => new EventGridEvent(Constants.Events.SyncRecalculateChallengeSubject, Constants.Events.SyncRecalculateChallengeType, Constants.Events.SyncRecalculateChallengeVersion, e))
                            .Select(e => outputEvents.AddAsync(e, CancellationToken.None)));
                    }
                    finally
                    {
                        await outputEvents.FlushAsync(CancellationToken.None);
                    }

                    return new OkResult();
                }

                _logger.LogWarning("Unknown event grid webhook event type: {0}", request.Headers["aeg-event-type"]);
                return new BadRequestResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(SyncActivityWebhook)} failed");
                throw;
            }
        }

        [FunctionName(nameof(SyncAthleteActivitiesWebhook))]
        public async Task<IActionResult> SyncAthleteActivitiesWebhook(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            [EventGrid(TopicEndpointUri = "EventGridRecalculateTopicUriSetting", TopicKeySetting = "EventGridRecalculateTopicKeySetting")] IAsyncCollector<EventGridEvent> outputEvents)
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
                        .Where(e => string.Equals(e.subject, Constants.Events.SyncAthleteActivitiesSubject, StringComparison.OrdinalIgnoreCase))
                        .Where(e => string.Equals(e.eventType, Constants.Events.SyncAthleteActivitiesType, StringComparison.OrdinalIgnoreCase))
                        .GroupBy(e => e.data.AthleteId)
                        .SelectMany(grp => grp.DistinctBy(g => g.data.SyncId).Select(d => d.data))
                        .ToArray();

                    await Task.WhenAll(events.Select(e => SyncAthleteActivitiesInternal(e, outputEvents)));
                    return new OkResult();
                }

                _logger.LogWarning("Unknown event grid webhook event type: {0}", request.Headers["aeg-event-type"]);
                return new BadRequestResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(SyncAthleteActivitiesWebhook)} failed");
                throw;
            }
        }

        private async Task FetchActivityInternal(SyncActivityEvent syncEvent, IAsyncCollector<EventGridEvent> outputEvents)
        {
            var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

            try
            {
                var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);

                var athleteCursors = await dbAthletes.FindAsync(a => a._id == syncEvent.AthleteId, new FindOptions<Lib.DbDtos.Athlete, Lib.DbDtos.Athlete>(), CancellationToken.None);
                var athlete = await athleteCursors.SingleAsync(CancellationToken.None);

                var detail = await GetActivityDetail(athlete.AccessToken, syncEvent.ActivityId);
                var activity = new Lib.DbDtos.Activity(
                    detail.id, detail.athlete.id, detail.name, detail.distance, detail.moving_time, detail.elapsed_time, detail.total_elevation_gain,
                    detail.type, detail.start_date, detail.start_date_local, detail.timezone, detail.utc_offset, detail.manual, detail._private,
                    detail.flagged, detail.average_speed, detail.max_speed, detail.elev_high, detail.elev_low, detail.workout_type,
                    detail.average_temp, detail.average_watts, detail.kilojoules, detail.calories, detail.device_watts, detail.average_cadence,
                    extendedInfo: true);

                var dbActivities = database.GetCollection<Lib.DbDtos.Activity>(MongoDbClientFactory.CollectionActivitiesName);
                var result = await dbActivities.ReplaceOneAsync(
                    a => a._id == activity._id, activity,
                    new ReplaceOptions { IsUpsert = true },
                    CancellationToken.None);
            }
            finally
            {
                var outEvent = new EventGridEvent(Constants.Events.SyncRecalculateChallengeSubject, Constants.Events.SyncRecalculateChallengeType, Constants.Events.SyncRecalculateChallengeVersion, new SyncAthleteEvent(syncEvent.AthleteId, Constants.AlwaysRecalculate));
                await outputEvents.AddAsync(outEvent, CancellationToken.None);
                await outputEvents.FlushAsync(CancellationToken.None);
            }
        }

        private async Task DeleteActivityInternal(SyncActivityEvent syncEvent)
        {
            var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

            var dbActivities = database.GetCollection<Lib.DbDtos.Activity>(MongoDbClientFactory.CollectionActivitiesName);
            var result = await dbActivities.DeleteOneAsync(a => a._id == syncEvent.ActivityId, CancellationToken.None);
        }

        // sync only last month or so as the longest supported challenge is 1 month
        private async Task SyncAthleteActivitiesInternal(SyncAthleteEvent syncEvent, IAsyncCollector<EventGridEvent> outputEvents)
        {
            var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

            try
            {
                // perform the sync
                var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);

                var athleteCursors = await dbAthletes.FindAsync(a => a._id == syncEvent.AthleteId, new FindOptions<Lib.DbDtos.Athlete, Lib.DbDtos.Athlete>(), CancellationToken.None);
                var athlete = await athleteCursors.SingleAsync(CancellationToken.None);

                var activitiesList = await ListActivitiesOrderedByStartDate(athlete.AccessToken);
                if (activitiesList.Length == 0) return;

                var firstDate = activitiesList[0].start_date;
                var lastDate = activitiesList[activitiesList.Length - 1].start_date;
                var activitiesDictionary = activitiesList.ToDictionary(a => a.id, a => a);

                var dbActivities = database.GetCollection<Lib.DbDtos.Activity>(MongoDbClientFactory.CollectionActivitiesName);
                var storedActivities = await dbActivities.Find(a => a.AthleteId == syncEvent.AthleteId && a.StartDate >= firstDate && a.StartDate <= lastDate).ToListAsync();
                var (activitiesToFetch, activitiesToDelete) = FindDifferences(activitiesDictionary, storedActivities);
                var toDeleteIds = activitiesToDelete.Select(a => a._id).ToHashSet();
                var result = await dbActivities.DeleteManyAsync(a => toDeleteIds.Contains(a._id), CancellationToken.None);

                var activitiesDetails = await Task.WhenAll(activitiesToFetch.Select(a => GetActivityDetailSafe(athlete.AccessToken, a)));

                var activities = activitiesDetails
                    .Select(ad =>
                    {
                        var (a, s) = ad;
                        return new Lib.DbDtos.Activity(
                            a.id, a.athlete.id, a.name, a.distance, a.moving_time, a.elapsed_time, a.total_elevation_gain,
                            a.type, a.start_date, a.start_date_local, a.timezone, a.utc_offset, a.manual, a._private,
                            a.flagged, a.average_speed, a.max_speed, a.elev_high, a.elev_low, a.workout_type,
                            a.average_temp, a.average_watts, a.kilojoules, a.calories, a.device_watts, a.average_cadence,
                            extendedInfo: s);
                    })
                    .ToArray();

                var results = await Task.WhenAll(activities.Select(activity =>
                    dbActivities.ReplaceOneAsync(
                        a => a._id == activity._id, activity,
                        new ReplaceOptions { IsUpsert = true },
                        CancellationToken.None)));
            }
            finally
            {
                var dbSync = database.GetCollection<Lib.DbDtos.Sync>(MongoDbClientFactory.CollectionSyncName);
                var result = await dbSync.UpdateOneAsync(
                    s => s._id == ObjectId.Parse(syncEvent.SyncId),
                    Builders<Lib.DbDtos.Sync>.Update.AddToSet(a => a.SyncedAthletes, syncEvent.AthleteId),
                    new UpdateOptions { IsUpsert = false },
                    CancellationToken.None);

                var outEvent = new EventGridEvent(Constants.Events.SyncRecalculateChallengeSubject, Constants.Events.SyncRecalculateChallengeType, Constants.Events.SyncRecalculateChallengeVersion, syncEvent);
                await outputEvents.AddAsync(outEvent, CancellationToken.None);
                await outputEvents.FlushAsync(CancellationToken.None);
            }
        }

        private (StravaDto.Activity[] ToFetch, Lib.DbDtos.Activity[] ToDelete) FindDifferences(Dictionary<ulong, StravaDto.Activity> fetchedActivities, List<Lib.DbDtos.Activity> storedActivities)
        {
            // we are going to just find out whether there is a newly created or a deleted activity...updates are ignored for simplicity unless the details were never synced before
            var storedIds = storedActivities.Select(a => a._id).ToHashSet();
            var newlyCreated = fetchedActivities.Values.Where(fa => !storedIds.Contains(fa.id)).ToArray();

            var notSyncedWithExtendedInfo = storedActivities.Where(a => a.extendedInfo == false).Select(a => a._id).ToHashSet();
            var existing = fetchedActivities.Values.Where(fa => notSyncedWithExtendedInfo.Contains(fa.id)).ToArray();

            var deleted = storedActivities.Where(sa => !fetchedActivities.ContainsKey(sa._id)).ToArray();

            _logger.LogInformation("Newly created activities to fetch: {0}", string.Join(", ", newlyCreated.Select(a => a.id)));
            _logger.LogInformation("Existing activities without extended info to fetch: {0}", string.Join(", ", existing.Select(a => a.id)));
            _logger.LogInformation("Missing activities to delete: {0}", string.Join(", ", deleted.Select(a => a._id)));

            return (newlyCreated.Concat(existing).DistinctBy(a => a.id).ToArray(), deleted);
        }

        public async Task<StravaDto.ActivityDetail> GetActivityDetail(string accessToken, ulong activityId)
        {
            var getActivityUrl = $"https://www.strava.com/api/v3/activities/{activityId}";
            using var stravaRequest = new HttpRequestMessage(HttpMethod.Get, getActivityUrl);
            stravaRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            stravaRequest.Headers.Add("Accept", "application/json");

            var response = await _stravaClient.SendRequestAsync(stravaRequest, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Synchronization of activities failed: {await response.Content.ReadAsStringAsync(CancellationToken.None)}");

            var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
            return JsonConvert.DeserializeObject<StravaDto.ActivityDetail>(responseJson)!;
        }

        private async Task<ActivityDetailResult> GetActivityDetailSafe(string accessToken, StravaDto.Activity activity)
        {
            try
            {
                return new ActivityDetailResult(
                    await GetActivityDetail(accessToken, activity.id),
                    success: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get detail for activity ID {0}", activity.id);
                return new ActivityDetailResult(
                    new StravaDto.ActivityDetail(activity.id, activity.athlete, activity.resource_state, activity.external_id,
                        activity.upload_id, activity.name, activity.distance, activity.moving_time, activity.elapsed_time,
                        activity.total_elevation_gain, activity.type, activity.start_date, activity.start_date_local, activity.timezone,
                        activity.utc_offset, activity.start_latlng, activity.end_latlng, activity.achievement_count, activity.kudos_count,
                        activity.comment_count, activity.athlete_count, activity.photo_count, activity.trainer, activity.commute,
                        activity.manual, activity._private, activity.flagged, activity.gear_id, activity.from_accepted_tag,
                        activity.average_speed, activity.max_speed, activity.average_cadence, activity.average_temp, activity.average_watts,
                        weighted_average_watts: 0, activity.kilojoules, activity.device_watts, activity.has_heartrate, max_watts: 0,
                        activity.elev_high, activity.elev_low, activity.pr_count, activity.total_photo_count, activity.has_kudoed,
                        activity.workout_type, description: string.Empty, calories: 0, hide_from_home: false, device_name: string.Empty,
                        embed_token: string.Empty, segment_leaderboard_opt_out: false, leaderboard_opt_out: false),
                    success: false);
            }
        }

        private async Task<StravaDto.Activity[]> ListActivitiesOrderedByStartDate(string accessToken)
        {
            var to = DateTimeOffset.UtcNow.AddDays(1);
            var from = to.AddDays(-35);  // yes, it's a bit more than a month, but whatever

            var getActivitiesUrl = $"https://www.strava.com/api/v3/athlete/activities?per_page=200&page=1&after={from.ToUnixTimeSeconds()}&before={to.ToUnixTimeSeconds()}";
            using var stravaRequest = new HttpRequestMessage(HttpMethod.Get, getActivitiesUrl);
            stravaRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            stravaRequest.Headers.Add("Accept", "application/json");

            var response = await _stravaClient.SendRequestAsync(stravaRequest, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Synchronization of activities failed: {await response.Content.ReadAsStringAsync(CancellationToken.None)}");

            var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
            var activities = JsonConvert.DeserializeObject<StravaDto.Activity[]>(responseJson)!;
            return activities.OrderBy(a => a.start_date).ToArray();
        }
    }

    public record SyncAthleteEvent(ulong AthleteId, string SyncId);
    public record SyncActivityEvent(ulong AthleteId, ulong ActivityId, ActivitySyncType SyncType);
    
    public record ActivityDetailResult(StravaDto.ActivityDetail activity, bool success);
}
