namespace Lib
{
    public static class Config
    {
        public static readonly ulong StravaAppClientId = ulong.Parse(Environment.GetEnvironmentVariable(nameof(StravaAppClientId))!);
        public static readonly string StravaAppClientSecret = Environment.GetEnvironmentVariable(nameof(StravaAppClientSecret))!;
        public static readonly string StravaWebhookSubscriptionToken = Environment.GetEnvironmentVariable(nameof(StravaWebhookSubscriptionToken))!;
    }
}
