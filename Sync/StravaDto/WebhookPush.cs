namespace Sync.StravaDto
{
    // ref: https://developers.strava.com/docs/webhooks/
    public record WebhookPush(
        string aspect_type, // Always "create," "update," or "delete."
        ulong event_time,   // he time that the event occurred.
        ulong object_id,    // For activity events, the activity's ID. For athlete events, the athlete's ID.
        string object_type, // Always either "activity" or "athlete."
        ulong owner_id,     // The athlete's ID.
        ulong subscription_id); // The push subscription ID that is receiving this event.

    public enum ActivitySyncType
    {
        create,
        update,
        delete,
    }
}
