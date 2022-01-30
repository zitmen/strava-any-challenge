using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System.Globalization;

namespace Lib
{
    public static class Authentication
    {
        public static readonly ulong OwnerAthleteId = ulong.Parse(
            Environment.GetEnvironmentVariable("OwnerAthlete") ?? throw new InvalidDataException("Owner athlete ID is not set!"),
            NumberStyles.Any, CultureInfo.InvariantCulture);

        // We cannot use the standard Authorization header in the static web app: https://github.com/Azure/static-web-apps/issues/34
        public const string AuthorizationHeader = "X-Custom-Authorization";

        public static async Task<DbDtos.Athlete> AuthenticateRequest(IMongoDatabase database, HttpRequest request)
        {
            if (!request.Headers.TryGetValue(AuthorizationHeader, out var authorization))
                throw new NotAuthenticated($"Missing {AuthorizationHeader} header");

            var tokens = authorization.Single().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length != 2 || !tokens[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
                throw new NotAuthenticated($"Missing Bearer token in {AuthorizationHeader} header: {authorization}");

            var accessToken = tokens[1];

            var dbAthletes = database.GetCollection<DbDtos.Athlete>(MongoDbClientFactory.CollectionAthletesName);
            var athlete = await dbAthletes.Find(a => a.SessionId == accessToken).SingleOrDefaultAsync(CancellationToken.None);
            if (athlete == null)
                throw new NotAuthenticated($"Invalid token: {accessToken}");

            return athlete;
        }

        public static async Task AuthenticateAdminRequest(IMongoDatabase database, HttpRequest request)
        {
            var athlete = await AuthenticateRequest(database, request);
            if (athlete._id != OwnerAthleteId)
                throw new NotAuthenticated("You are not an admin!");
        }
    }

    public class NotAuthenticated : Exception
    {
        public NotAuthenticated(string message) : base(message)
        { }
    }
}
