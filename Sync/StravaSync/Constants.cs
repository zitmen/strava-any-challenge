namespace Sync.StravaSync
{
    internal static class Constants
    {
        public const string AlwaysRecalculate = "always";

        public static class Events
        {
            public const string SyncActivitySubject = "SyncActivity";
            public const string SyncActivityType = "activitySync";
            public const string SyncActivityVersion = "1.0";

            public const string SyncAthleteActivitiesSubject = "SyncAthleteActivities";
            public const string SyncAthleteActivitiesType = "athleteSync";
            public const string SyncAthleteActivitiesVersion = "1.0";

            public const string SyncRecalculateChallengeSubject = "RecalculateChallenge";
            public const string SyncRecalculateChallengeType = "recalculate";
            public const string SyncRecalculateChallengeVersion = "1.0";
        }
    }
}
