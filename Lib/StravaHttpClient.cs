using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using System.Net;

namespace Lib
{
    public sealed class StravaHttpClient : IDisposable
    {
        private readonly IMongoClient _mongoClient;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public StravaHttpClient(IMongoClient mongoClient, IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _mongoClient = mongoClient;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
                return response;

            var bearer = authHeaders.SingleOrDefault();
            if (bearer == null)
                return response;

            var accessToken = bearer.Split(' ')[1];

            var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
            var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);
            var athlete = await dbAthletes.Find(a => a.AccessToken == accessToken).SingleOrDefaultAsync(cancellationToken);
            if (athlete == null)
                return response;

            accessToken = await RefreshAccessToken(athlete._id, athlete.RefreshToken);

            var newRequest = await request.CloneAsync(cancellationToken);
            newRequest.Headers.Remove("Authorization");
            newRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            var newResponse = await _httpClient.SendAsync(newRequest, cancellationToken);

            // if the refresh of the token did not help, let's force the user to relogin completely by generating a new session ID
            if (newResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                var result = await dbAthletes.UpdateOneAsync(
                    a => a.AccessToken == accessToken,
                    Builders<Lib.DbDtos.Athlete>.Update.Set(a => a.SessionId, Guid.NewGuid().ToString()),
                    new UpdateOptions { IsUpsert = false },
                    CancellationToken.None);
            }

            return newResponse;
        }

        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private async Task<string> RefreshAccessToken(ulong athleteId, string refreshToken)
        {
            var refreshedByAnotherThread = false;
            if (!await _semaphore.WaitAsync(TimeSpan.Zero, CancellationToken.None))
            {
                refreshedByAnotherThread = true;
                await _semaphore.WaitAsync(CancellationToken.None);
            }

            try
            {
                var database = _mongoClient.GetDatabase(MongoDbClientFactory.DbName);
                var dbAthletes = database.GetCollection<Lib.DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);

                if (refreshedByAnotherThread)
                {
                    _logger.LogInformation("Strava access token has been refreshed by another thread. Reloading it from the DB...");
                    var athlete = await dbAthletes.Find(a => a._id == athleteId).SingleAsync(CancellationToken.None);
                    return athlete.AccessToken;
                }

                _logger.LogInformation("Refreshing the Strava access token...");

                var stravaAuthUri = $"https://www.strava.com/api/v3/oauth/token?client_id={Config.StravaAppClientId}&client_secret={Config.StravaAppClientSecret}&grant_type=refresh_token&refresh_token={refreshToken}";
                using var response = await _httpClient.PostAsync(stravaAuthUri, content: null, CancellationToken.None);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Authentication failed: {await response.Content.ReadAsStringAsync(CancellationToken.None)}");

                var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
                var responseObject = JsonConvert.DeserializeObject<Authenticated>(responseJson)!;

                var result = await dbAthletes.UpdateOneAsync(
                    a => a.RefreshToken == refreshToken,
                    Builders<DbDtos.Athlete>.Update.Combine(
                        Builders<DbDtos.Athlete>.Update.Set(a => a.AccessToken, responseObject.access_token),
                        Builders<DbDtos.Athlete>.Update.Set(a => a.RefreshToken, responseObject.refresh_token),
                        Builders<DbDtos.Athlete>.Update.Set(a => a.ExpiresAt, responseObject.expires_at)),
                    new UpdateOptions { IsUpsert = false },
                    CancellationToken.None);

                return responseObject.access_token;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    public record Authenticated(
        string token_type,
        int expires_at,
        int expires_in,
        string refresh_token,
        string access_token,
        Athlete athlete);

    public record Athlete(
        ulong id,
        string username,
        int resource_state,
        string firstname,
        string lastname,
        string bio,
        string city,
        string state,
        string country,
        string sex,
        bool premium,
        bool summit,
        DateTime created_at,
        DateTime updated_at,
        int badge_type_id,
        float weight,
        string profile_medium,
        string profile,
        object friend,
        object follower);
}
