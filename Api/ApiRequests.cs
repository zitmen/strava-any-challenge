using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using MongoDB.Driver;
using System.Threading;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Lib;
using System.Globalization;
using MongoDB.Bson;
using System.IO;

namespace Api
{
    public class ApiRequests
    {
        private static CultureInfo UsCulture = CultureInfo.CreateSpecificCulture("en-US");

        private static readonly HashSet<ulong> AllowedAthletes = Environment.GetEnvironmentVariable("AllowedAthletes")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ulong.Parse)
            .ToHashSet();

        private readonly IMongoClient _mongoClient;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public ApiRequests(IMongoClient mongoClient, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _mongoClient = mongoClient;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        [FunctionName(nameof(Authenticate))]
        public async Task<IActionResult> Authenticate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "authenticate")] HttpRequest request)
        {
            try
            {
                string requestBody = await request.ReadRequestBody();
                var loginDto = JsonConvert.DeserializeObject<LoginDto>(requestBody);
                var stravaAuthUri = $"https://www.strava.com/api/v3/oauth/token?client_id={Config.StravaAppClientId}&client_secret={Config.StravaAppClientSecret}&grant_type=authorization_code&code={loginDto.Code}";

                using var response = await _httpClient.PostAsync(stravaAuthUri, content: null, CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                    return new UnauthorizedObjectResult($"Authentication failed: {await response.Content.ReadAsStringAsync(CancellationToken.None)}");

                var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
                var responseObject = JsonConvert.DeserializeObject<StravaDto.Authenticated>(responseJson);

                if (!AllowedAthletes.Contains(responseObject.athlete.id))
                    return new UnauthorizedObjectResult("Sorry, but this application is currently targeted only to a close circle of friends. If you know me, you can ask me to add you to the list 😉");

                var athlete = new Lib.DbDtos.Athlete(responseObject.athlete.id, Guid.NewGuid().ToString(), responseObject.expires_at,
                    responseObject.refresh_token, responseObject.access_token, $"{responseObject.athlete.firstname} {responseObject.athlete.lastname}",
                    responseObject.athlete.profile_medium, responseObject.athlete.profile);

                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
                var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);

                var result = await dbAthletes.ReplaceOneAsync(
                    a => a._id == athlete._id, athlete,
                    new ReplaceOptions { IsUpsert = true },
                    CancellationToken.None);

                if (result.IsAcknowledged && result.MatchedCount == 0)
                {
                    _logger.LogInformation("A new user has logged in. Starting the sync...");

                    // sync
                    try
                    {
                        var syncRequest = new HttpRequestMessage(HttpMethod.Post, Environment.GetEnvironmentVariable("SyncServiceUrl"));
                        syncRequest.Headers.Add(Authentication.AuthorizationHeader, $"Bearer {athlete.SessionId}");    // the session ID identified the athlete as well as the athlete ID
                        using var syncResponse = await _httpClient.SendAsync(syncRequest, CancellationToken.None);
                        if (!syncResponse.IsSuccessStatusCode)
                            _logger.LogWarning($"Failed to trigger sync for athlete {athlete._id}: {await syncResponse.Content.ReadAsStringAsync(CancellationToken.None)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to trigger sync for athlete {athlete._id}");
                    }
                }

                if (athlete._id == Authentication.OwnerAthleteId)
                    return new OkObjectResult(new AuthenticatedDto(athlete._id, athlete.SessionId, isAdmin: true));

                return new OkObjectResult(new AuthenticatedDto(athlete._id, athlete.SessionId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(Authenticate)} failed");
                throw;
            }
        }

        [FunctionName(nameof(GetChallenges))]
        public async Task<IActionResult> GetChallenges(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "challenges")] HttpRequest request)
        {
            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                try
                {
                    await Authentication.AuthenticateRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var challenges = await dbChallenges.Find(Builders<Lib.DbDtos.Challenge>.Filter.Empty).ToListAsync();   // TODO: can be optimized by projecting only the summary

                var current = challenges
                    .Where(ch => ch.State == Lib.DbDtos.ChallengeState.Current)
                    .OrderBy(ch => ch.From).ThenBy(ch => ch.To)
                    .Select(ch => new ChallengeInfo(ch._id.ToString(), ch.Name, GetChallengeGoal(ch), ch.From, ch.To))
                    .ToArray();

                var upcoming = challenges
                    .Where(ch => ch.State == Lib.DbDtos.ChallengeState.Upcoming)
                    .OrderBy(ch => ch.From).ThenBy(ch => ch.To)
                    .Select(ch => new ChallengeInfo(ch._id.ToString(), ch.Name, GetChallengeGoal(ch), ch.From, ch.To))
                    .ToArray();

                var past = challenges
                    .Where(ch => ch.State == Lib.DbDtos.ChallengeState.Past)
                    .OrderByDescending(ch => ch.From).ThenByDescending(ch => ch.To)
                    .Select(ch => new ChallengeInfo(ch._id.ToString(), ch.Name, GetChallengeGoal(ch), ch.From, ch.To))
                    .ToArray();

                var output = new AllChallenges(current, upcoming, past);

                return new OkObjectResult(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetChallenge)} failed");
                throw;
            }
        }
        
        [FunctionName(nameof(JoinChallenge))]
        public async Task<IActionResult> JoinChallenge(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "challenges/{challengeId}/join")] HttpRequest request,
            string challengeId)
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

                if (!ObjectId.TryParse(challengeId, out var chId))
                    return new BadRequestResult();

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var challenge = await dbChallenges.Find(ch => ch._id == chId).SingleOrDefaultAsync(CancellationToken.None);
                if (challenge == null)
                    return new BadRequestResult();

                var stats = await ChallengeAggreagation.CalculateChallengeAthletesStats(database, challenge, new Dictionary<ulong, Lib.DbDtos.Athlete> { { athlete._id, athlete } });
                if (!stats.TryGetValue(athlete._id, out var athleteStats))
                    athleteStats = new Lib.DbDtos.AthleteChallengeStats(athlete._id, athlete.Username, athlete.AvatarSmallUrl);

                var result = await dbChallenges.UpdateOneAsync(
                    Builders<Lib.DbDtos.Challenge>.Filter.And(
                        Builders<Lib.DbDtos.Challenge>.Filter.Eq(ch => ch._id, chId),
                        Builders<Lib.DbDtos.Challenge>.Filter.Not(
                            Builders<Lib.DbDtos.Challenge>.Filter.ElemMatch(
                                ch => ch.AthletesStats,
                                Builders<Lib.DbDtos.AthleteChallengeStats>.Filter.Eq(a => a._id, athlete._id)))),
                    Builders<Lib.DbDtos.Challenge>.Update.AddToSet(ch => ch.AthletesStats, athleteStats),
                    new UpdateOptions { IsUpsert = false },
                    CancellationToken.None);

                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(JoinChallenge)} failed");
                throw;
            }
        }

        [FunctionName(nameof(LeaveChallenge))]
        public async Task<IActionResult> LeaveChallenge(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "challenges/{challengeId}/leave")] HttpRequest request,
            string challengeId)
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

                if (!ObjectId.TryParse(challengeId, out var chId))
                    return new BadRequestResult();

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var result = await dbChallenges.UpdateOneAsync(
                    ch => ch._id == chId,
                    Builders<Lib.DbDtos.Challenge>.Update.PullFilter(ch => ch.AthletesStats, builder => builder._id == athlete._id),
                    new UpdateOptions { IsUpsert = false },
                    CancellationToken.None);

                return new OkResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(LeaveChallenge)} failed");
                throw;
            }
        }

        [FunctionName(nameof(CreateChallenge))]
        public async Task<IActionResult> CreateChallenge(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "challenges")] HttpRequest request)
        {
            try
            {
                var mongoClient = MongoDbClientFactory.Create();
                var database = mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                try
                {
                    await Authentication.AuthenticateAdminRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                var now = DateTime.UtcNow;
                var (challenge, goalType, timeGoal, numericGoal, activityTypes, from, to) = await ParseChallengeFromUser(request);
                var isTimeGoal = goalType == Lib.DbDtos.ChallengeType.TotalTime || goalType == Lib.DbDtos.ChallengeType.TotalMovingTime;

                var challengeId = ObjectId.GenerateNewId();
                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                await dbChallenges.InsertOneAsync(
                    new Lib.DbDtos.Challenge(challengeId, challenge.Name, from, to, goalType)
                    {
                        NumericGoal = isTimeGoal ? null : numericGoal,
                        TimeGoal = isTimeGoal ? timeGoal : null,
                        ActivityTypes = activityTypes,
                        State = ChallengeAggreagation.GetChallengeState(now, from, to),
                    },
                    new InsertOneOptions() { BypassDocumentValidation = false },
                    CancellationToken.None);

                return new OkObjectResult(challengeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(CreateChallenge)} failed");
                throw;
            }
        }

        [FunctionName(nameof(EditChallenge))]
        public async Task<IActionResult> EditChallenge(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "challenges/{challengeId}")] HttpRequest request,
            string challengeId)
        {
            try
            {
                var mongoClient = MongoDbClientFactory.Create();
                var database = mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                try
                {
                    await Authentication.AuthenticateAdminRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                if (!ObjectId.TryParse(challengeId, out var chId))
                    return new NotFoundResult();

                var now = DateTime.UtcNow;
                var (challenge, goalType, timeGoal, numericGoal, activityTypes, from, to) = await ParseChallengeFromUser(request);
                var isTimeGoal = goalType == Lib.DbDtos.ChallengeType.TotalTime || goalType == Lib.DbDtos.ChallengeType.TotalMovingTime;

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var result = await dbChallenges.UpdateOneAsync(
                    ch => ch._id == chId,
                    Builders<Lib.DbDtos.Challenge>.Update.Combine(
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.Name, challenge.Name),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.From, challenge.From),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.To, challenge.To),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.GoalType, goalType),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.NumericGoal, numericGoal),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.TimeGoal, timeGoal),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.State, ChallengeAggreagation.GetChallengeState(now, from, to)),
                        Builders<Lib.DbDtos.Challenge>.Update.Set(ch => ch.ActivityTypes, activityTypes)),
                    new UpdateOptions { IsUpsert = false },
                    CancellationToken.None);

                if (result.MatchedCount != 1)
                    return new NotFoundResult();

                if (!result.IsAcknowledged || !result.IsModifiedCountAvailable || result.ModifiedCount != 1)
                    throw new InvalidOperationException("Update failed");

                return new NoContentResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(EditChallenge)} failed");
                throw;
            }
        }

        [FunctionName(nameof(DeleteChallenge))]
        public async Task<IActionResult> DeleteChallenge(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "challenges/{challengeId}")] HttpRequest request,
            string challengeId)
        {
            try
            {
                var mongoClient = MongoDbClientFactory.Create();
                var database = mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                try
                {
                    await Authentication.AuthenticateAdminRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                if (!ObjectId.TryParse(challengeId, out var chId))
                    return new NotFoundResult();

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var result = await dbChallenges.DeleteOneAsync(ch => ch._id == chId);
                if (!result.IsAcknowledged)
                    throw new InvalidOperationException("The deletion failed");

                return new NoContentResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(DeleteChallenge)} failed");
                throw;
            }
        }
        
        [FunctionName(nameof(GetChallenge))]
        public async Task<IActionResult> GetChallenge(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "challenges/{challengeId}")] HttpRequest request,
            string challengeId)
        {
            if (!ObjectId.TryParse(challengeId, out var chId))
                return new BadRequestObjectResult("Invalid challenge ID format");

            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                try
                {
                    await Authentication.AuthenticateRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var challenge = await dbChallenges.Find(ch => ch._id == chId).SingleOrDefaultAsync();
                if (challenge == null)
                    return new BadRequestObjectResult($"Challenge ID {chId} was not found");

                var allowedSports = challenge.ActivityTypes.Select(a => a.ToString()).ToArray();
                var allowedSportsIcons = string.Join(' ', challenge.ActivityTypes.Select(a => ActivityIcons[a]).Where(a => a != null).Distinct().ToArray());
                var (goal, getTotalScore, getPercentOfGoal) = GetChallengeDataProvider(challenge);
                var goalType = char.ToLowerInvariant(challenge.GoalType.ToString()[0]) + challenge.GoalType.ToString().Substring(1);
                var goalRaw = GetChallengeGoalRaw(challenge);
                var output = new Challenge(challengeId, challenge.Name, GetTotalScoreTitle(challenge), goalType, goalRaw, goal, allowedSports, allowedSportsIcons, challenge.From, challenge.To, challenge.AthletesStats
                    .OrderByDescending(a => GetTotalScore(challenge, a))
                    .Select(a => new AthleteChallengeStatus(a._id, a.AvatarUrl, a.Name, getTotalScore(a), getPercentOfGoal(a)))
                    .ToArray());

                return new OkObjectResult(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetChallenge)} failed");
                throw;
            }
        }

        [FunctionName(nameof(GetAthleteChallengeInfo))]
        public async Task<IActionResult> GetAthleteChallengeInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "challenges/{challengeId}/athletes/{athleteId}")] HttpRequest request,
            string challengeId,
            ulong athleteId)
        {
            if (!ObjectId.TryParse(challengeId, out var chId))
                return new BadRequestObjectResult("Invalid challenge ID format");

            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);

                try
                {
                    await Authentication.AuthenticateRequest(database, request);
                }
                catch (NotAuthenticated ex)
                {
                    return new UnauthorizedObjectResult(ex.Message);
                }

                var dbChallenges = database.GetCollection<Lib.DbDtos.Challenge>(MongoDbClientFactory.CollectionChallengesName);
                var challenge = await dbChallenges.Find(ch => ch._id == chId).SingleOrDefaultAsync();   // TODO: can be optimized by projecting only the athlete summary
                if (challenge == null)
                    return new BadRequestObjectResult($"Challenge ID {chId} was not found");

                var athleteSummary = challenge.AthletesStats.Single(a => a._id == athleteId);

                var output = new AthleteChallengeInfo(athleteSummary._id, athleteSummary.AvatarUrl,
                    athleteSummary.Name, athleteSummary.TotalDistance, athleteSummary.ActivityCount,
                    athleteSummary.Activities
                        .OrderByDescending(a => a.StartDateLocal)
                        .Select(a => new ActivityInfo(a._id, a.Name, a.Type, ToClientString(a.StartDateLocal), $"{ToClientString0(a.Distance)}m", ToClientString(a.MovingTime), $"{ToClientString0(a.KiloCalories)}Cal"))
                        .ToArray());
                
                return new OkObjectResult(output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{nameof(GetChallenge)} failed");
                throw;
            }
        }

        private static async Task<(ChallengeFromUser challenge, Lib.DbDtos.ChallengeType goalType, TimeSpan timeGoal, double numericGoal, Lib.DbDtos.ActivityType[] activityTypes, DateTime from, DateTime to)> ParseChallengeFromUser(HttpRequest request)
        {
            ChallengeFromUser challenge;
            try
            {
                using (var streamReader = new StreamReader(request.Body))
                {
                    var body = await streamReader.ReadToEndAsync();
                    challenge = JsonConvert.DeserializeObject<ChallengeFromUser>(body)
                         ?? throw new NullReferenceException();
                }
            }
            catch
            {
                throw new InvalidDataException("Cannot deserialize the challenge payload!");
            }

            if (!Enum.TryParse<Lib.DbDtos.ChallengeType>(challenge.GoalType, ignoreCase: true, out var goalType))
                throw new InvalidDataException("Unknown goal type!");

            var isTimeGoal = goalType == Lib.DbDtos.ChallengeType.TotalTime || goalType == Lib.DbDtos.ChallengeType.TotalMovingTime;
            TimeSpan timeGoal = TimeSpan.Zero;
            double numericGoal = 0.0;
            if (isTimeGoal)
            {
                if (!TimeSpan.TryParse(challenge.Goal, CultureInfo.InvariantCulture, out timeGoal))
                    throw new InvalidDataException("Time goal cannot be parsed!");
            }
            else
            {
                if (!double.TryParse(challenge.Goal, NumberStyles.Any, CultureInfo.InvariantCulture, out numericGoal))
                    throw new InvalidDataException("Numeric goal cannot be parsed!");
            }

            var from = challenge.From.ToUniversalTime();
            var to = challenge.To.ToUniversalTime();
            if (from > to)
                throw new InvalidDataException("Date range must be from earlier date to later date!");

            Lib.DbDtos.ActivityType[] activityTypes;
            if (challenge.AllowedSports?.Length > 0)
            {
                activityTypes = new Lib.DbDtos.ActivityType[challenge.AllowedSports.Length];
                for (int i = 0; i < challenge.AllowedSports.Length; i++)
                {
                    if (!Enum.TryParse(challenge.AllowedSports[i], ignoreCase: true, out activityTypes[i]))
                        throw new InvalidDataException($"Unknown activity type {challenge.AllowedSports[i]}!");
                }
            }
            else
            {
                throw new InvalidDataException("Missing allowed sports!");
            }

            return (challenge, goalType, timeGoal, numericGoal, activityTypes, from, to);
        }

        private static Dictionary<Lib.DbDtos.ActivityType, string> ActivityIcons = new()
        {
            { Lib.DbDtos.ActivityType.AlpineSki, "⛷️" },
            { Lib.DbDtos.ActivityType.BackcountrySki, "⛷️" },
            { Lib.DbDtos.ActivityType.Canoeing, "🛶" },
            { Lib.DbDtos.ActivityType.Crossfit, "🏋" },
            { Lib.DbDtos.ActivityType.EBikeRide, "🚴" },
            { Lib.DbDtos.ActivityType.Elliptical, null },
            { Lib.DbDtos.ActivityType.Golf, "🏌️" },
            { Lib.DbDtos.ActivityType.Handcycle, null },
            { Lib.DbDtos.ActivityType.Hike, "🚶" },
            { Lib.DbDtos.ActivityType.IceSkate, "⛸️" },
            { Lib.DbDtos.ActivityType.InlineSkate, null },
            { Lib.DbDtos.ActivityType.Kayaking, null },
            { Lib.DbDtos.ActivityType.Kitesurf, null },
            { Lib.DbDtos.ActivityType.NordicSki, "⛷️" },
            { Lib.DbDtos.ActivityType.Ride, "🚴" },
            { Lib.DbDtos.ActivityType.RockClimbing, null },
            { Lib.DbDtos.ActivityType.RollerSki, "⛷️" },
            { Lib.DbDtos.ActivityType.Rowing, "🚣" },
            { Lib.DbDtos.ActivityType.Run, "🏃" },
            { Lib.DbDtos.ActivityType.Sail, null },
            { Lib.DbDtos.ActivityType.Skateboard, "🛹" },
            { Lib.DbDtos.ActivityType.Snowboard, "🏂" },
            { Lib.DbDtos.ActivityType.Snowshoe, null },
            { Lib.DbDtos.ActivityType.Soccer, "⚽" },
            { Lib.DbDtos.ActivityType.StairStepper, null },
            { Lib.DbDtos.ActivityType.StandUpPaddling, null },
            { Lib.DbDtos.ActivityType.Surfing, "🏄" },
            { Lib.DbDtos.ActivityType.Swim, "🏊" },
            { Lib.DbDtos.ActivityType.Velomobile, null },
            { Lib.DbDtos.ActivityType.VirtualRide, "🚴" },
            { Lib.DbDtos.ActivityType.VirtualRun, "🏃" },
            { Lib.DbDtos.ActivityType.Walk, "🚶" },
            { Lib.DbDtos.ActivityType.WeightTraining, "🏋" },
            { Lib.DbDtos.ActivityType.Wheelchair, "👨‍🦽" },
            { Lib.DbDtos.ActivityType.Windsurf, null },
            { Lib.DbDtos.ActivityType.Workout, "🏋" },
            { Lib.DbDtos.ActivityType.Yoga, "🧘" },
        };

        private static double GetTotalScore(Lib.DbDtos.Challenge challenge, Lib.DbDtos.AthleteChallengeStats stats)
        {
            return challenge.GoalType switch
            {
                Lib.DbDtos.ChallengeType.TotalDistance => stats.TotalDistance,
                Lib.DbDtos.ChallengeType.TotalKiloJoules => stats.TotalKiloJoules,
                Lib.DbDtos.ChallengeType.TotalKiloCalories => stats.TotalKiloCalories,
                Lib.DbDtos.ChallengeType.TotalTime => stats.TotalTime.TotalSeconds,
                Lib.DbDtos.ChallengeType.TotalMovingTime => stats.TotalMovingTime.TotalSeconds,
                _ => throw new NotImplementedException("Unsupported goal type"),
            };
        }

        private static string GetTotalScoreTitle(Lib.DbDtos.Challenge challenge)
        {
            return challenge.GoalType switch
            {
                Lib.DbDtos.ChallengeType.TotalDistance => "Distance (km)",
                Lib.DbDtos.ChallengeType.TotalKiloJoules => "Energy (kJ)",
                Lib.DbDtos.ChallengeType.TotalKiloCalories => "Energy (Cal)",
                Lib.DbDtos.ChallengeType.TotalTime => "Time",
                Lib.DbDtos.ChallengeType.TotalMovingTime => "Moving Time",
                _ => throw new NotImplementedException("Unsupported goal type"),
            };
        }

        private static string GetChallengeGoal(Lib.DbDtos.Challenge challenge)
        {
            var culture = CultureInfo.CreateSpecificCulture("en-US");
            return challenge.GoalType switch
            {
                Lib.DbDtos.ChallengeType.TotalDistance => $"{((int)(challenge.NumericGoal / 1000.0)).ToString(culture)}km",
                Lib.DbDtos.ChallengeType.TotalKiloJoules => $"{((int)challenge.NumericGoal).ToString(culture)}kJ",
                Lib.DbDtos.ChallengeType.TotalKiloCalories => $"{((int)challenge.NumericGoal).ToString(culture)}Cal",
                Lib.DbDtos.ChallengeType.TotalTime => challenge.TimeGoal.Value.ToString("c", CultureInfo.InvariantCulture),
                Lib.DbDtos.ChallengeType.TotalMovingTime => challenge.TimeGoal.Value.ToString("c", CultureInfo.InvariantCulture),
                _ => throw new NotImplementedException("Unsupported goal type"),
            };
        }

        private static string GetChallengeGoalRaw(Lib.DbDtos.Challenge challenge)
        {
            var culture = CultureInfo.CreateSpecificCulture("en-US");
            return challenge.GoalType switch
            {
                Lib.DbDtos.ChallengeType.TotalDistance or
                Lib.DbDtos.ChallengeType.TotalKiloJoules or
                Lib.DbDtos.ChallengeType.TotalKiloCalories => ((int)challenge.NumericGoal).ToString(culture),
                Lib.DbDtos.ChallengeType.TotalTime or
                Lib.DbDtos.ChallengeType.TotalMovingTime => challenge.TimeGoal.Value.ToString("c", CultureInfo.InvariantCulture),
                _ => throw new NotImplementedException("Unsupported goal type"),
            };
        }

        private static (string Goal, Func<Lib.DbDtos.AthleteChallengeStats, string> GetTotalScore, Func<Lib.DbDtos.AthleteChallengeStats, int> GetPercentOfGoal) GetChallengeDataProvider(Lib.DbDtos.Challenge challenge)
        {
            Func<Lib.DbDtos.AthleteChallengeStats, string> getTotalScore = _ => "-";
            Func<Lib.DbDtos.AthleteChallengeStats, int> getPercentOfGoal = _ => 0;
            if (challenge.GoalType == Lib.DbDtos.ChallengeType.TotalDistance)
            {
                getTotalScore = a => ToClientString2(a.TotalDistance / 1000.0);
                getPercentOfGoal = a => (int)Math.Round(a.TotalDistance / challenge.NumericGoal.Value * 100.0);
            }
            else if (challenge.GoalType == Lib.DbDtos.ChallengeType.TotalKiloJoules)
            {
                getTotalScore = a => ToClientString1(a.TotalKiloJoules);
                getPercentOfGoal = a => (int)Math.Round(a.TotalKiloJoules / challenge.NumericGoal.Value * 100.0);
            }
            else if (challenge.GoalType == Lib.DbDtos.ChallengeType.TotalKiloCalories)
            {
                getTotalScore = a => ToClientString0(a.TotalKiloCalories);
                getPercentOfGoal = a => (int)Math.Round(a.TotalKiloCalories / challenge.NumericGoal.Value * 100.0);
            }
            else if (challenge.GoalType == Lib.DbDtos.ChallengeType.TotalTime)
            {
                getTotalScore = a => a.TotalTime.ToString("c", CultureInfo.InvariantCulture);
                getPercentOfGoal = a => (int)Math.Round(a.TotalTime.TotalSeconds / challenge.TimeGoal.Value.TotalSeconds * 100.0);
            }
            else if (challenge.GoalType == Lib.DbDtos.ChallengeType.TotalMovingTime)
            {
                getTotalScore = a => a.TotalMovingTime.ToString("c", CultureInfo.InvariantCulture);
                getPercentOfGoal = a => (int)Math.Round(a.TotalMovingTime.TotalSeconds / challenge.TimeGoal.Value.TotalSeconds * 100.0);
            }
            else
            {
                throw new NotImplementedException("Unsupported goal type");
            }

            return (GetChallengeGoal(challenge), getTotalScore, getPercentOfGoal);
        }

        private static string ToClientString0(double value)
            => ((int)value).ToString(UsCulture);

        private static string ToClientString1(double value)
            => value.ToString("0.0", UsCulture);

        private static string ToClientString2(double value)
            => value.ToString("0.00", UsCulture);

        private static string ToClientString(int movingTime)
            => TimeSpan.FromSeconds(movingTime).ToString("c", CultureInfo.InvariantCulture);

        private static string ToClientString(DateTime startDateLocal)
            => startDateLocal.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);
    }

    public record LoginDto(string Code);
    public record AuthenticatedDto(ulong AthleteId, string AccessToken, bool? isAdmin = null);
    public record AllChallenges(IReadOnlyCollection<ChallengeInfo> Current, IReadOnlyCollection<ChallengeInfo> Upcoming, IReadOnlyCollection<ChallengeInfo> Past);
    public record ChallengeInfo(string Id, string Name, string Goal, DateTime From, DateTime To);
    public record Challenge(string Id, string Name, string TotalScoreTitle, string GoalType, string GoalRaw, string Goal, string[] AllowedSports, string AllowedSportsIcons, DateTime From, DateTime To, IReadOnlyCollection<AthleteChallengeStatus> athletes);
    public record ChallengeFromUser(string Name, string Goal, string GoalType, string[] AllowedSports, DateTime From, DateTime To);
    public record AthleteChallengeStatus(ulong AthleteId, string AvatarUrl, string Username, string TotalScore, int? PercentOfGoal = null);
    public record AthleteChallengeInfo(ulong AthleteId, string AvatarUrl, string Username, double TotalDistance, int ActivitiesCount, IReadOnlyCollection<ActivityInfo> Activities);
    public record ActivityInfo(ulong Id, string Name, string Type, string StartDateLocal, string Distance, string MovingTime, string KiloJoules);

    public class UnauthorizedObjectResult : ObjectResult
    {
        public UnauthorizedObjectResult(object value) : base(value)
        {
            StatusCode = 401;
        }
    }
}
