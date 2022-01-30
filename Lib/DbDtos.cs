using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Lib.DbDtos;

public record Athlete(
    ulong _id,
    string SessionId,
    int ExpiresAt,
    string RefreshToken,
    string AccessToken,
    string Username,
    string AvatarSmallUrl,
    string AvatarUrl);

public record Activity(
    ulong _id,
    ulong AthleteId,
    string Name,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    double TotalElevationGain,
    string Type,
    DateTime StartDate,
    DateTime StartDateLocal,
    string Timezone,
    double UtcOffset,
    bool Manual,
    bool Private,
    bool Flagged,
    double AverageSpeed,
    double MaxSpeed,
    double ElevHigh,
    double ElevLow,
    int? WorkoutType,
    int AverageTemp,
    double AverageWatts,
    double KiloJoules,
    double KiloCalories,
    bool DeviceWatts,
    double AverageCadence,
    bool? extendedInfo);    // is true when the activity was extended by additional info provided by the GET https://www.strava.com/api/v3/activities/{activityId}
                            // is false if the activity contains only the info provided by the GET https://www.strava.com/api/v3/activities
                            // is null if the activity was synced by the previous version of the app, which did not have this property

public record ActivityAggregation(
    ulong _id,
    double TotalDistance);

// ref: https://developers.strava.com/docs/reference/#api-models-ActivityType
public enum ActivityType
{
    AlpineSki, BackcountrySki, Canoeing, Crossfit, EBikeRide, Elliptical, Golf, Handcycle, Hike, IceSkate, InlineSkate,
    Kayaking, Kitesurf, NordicSki, Ride, RockClimbing, RollerSki, Rowing, Run, Sail, Skateboard, Snowboard, Snowshoe,
    Soccer, StairStepper, StandUpPaddling, Surfing, Swim, Velomobile, VirtualRide, VirtualRun, Walk, WeightTraining,
    Wheelchair, Windsurf, Workout, Yoga
}

public enum ChallengeType
{
    TotalDistance,
    TotalTime,
    TotalMovingTime,
    TotalKiloJoules,
    TotalKiloCalories,
}

public enum ChallengeState
{
    Current,
    Upcoming,
    Past,
}

public class Challenge
{
    public ObjectId _id { get; init; }
    public string Name { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public double? NumericGoal { get; init; }
    public TimeSpan? TimeGoal { get; init; }
    public IReadOnlyCollection<ulong> ParticipatingAthletesIds { get; init; } = Array.Empty<ulong>();
    public IReadOnlyCollection<AthleteChallengeStats> AthletesStats { get; init; } = Array.Empty<AthleteChallengeStats>();

    [BsonRepresentation(BsonType.String)]
    public ActivityType[] ActivityTypes { get; init; } = Array.Empty<ActivityType>();

    [BsonRepresentation(BsonType.String)]
    public ChallengeType GoalType { get; init; }

    [BsonRepresentation(BsonType.String)]
    public ChallengeState State { get; init; }

    public Challenge(ObjectId id, string name, DateTime from, DateTime to, ChallengeType goalType)
    {
        _id = id;
        Name = name;
        From = from;
        To = to;
        GoalType = goalType;
        State = GetState(from, to);
    }

    public static ChallengeState GetState(DateTime from, DateTime to)
    {
        var today_00_00 = DateTime.UtcNow.Date;
        return today_00_00.AddDays(-1) < from
            ? ChallengeState.Upcoming
            : today_00_00.AddDays(+2) > to
                ? ChallengeState.Past
                : ChallengeState.Current;
    }
}

public record AthleteChallengeStats(
    ulong _id,  // AthleteId
    string Name,
    string AvatarUrl,
    int ActivityCount = 0,
    TimeSpan TotalTime = default,
    TimeSpan TotalMovingTime = default,
    double TotalKiloJoules = 0.0,
    double TotalKiloCalories = 0.0,
    double TotalDistance = 0.0,
    IReadOnlyCollection<ActivitySummary>? Activities = null)
{
    public IReadOnlyCollection<ActivitySummary>? Activities { get; init; } = Activities ?? Array.Empty<ActivitySummary>();
}

public record ActivitySummary(
    ulong _id,  // ActivityId
    ulong AthleteId,
    string Name,
    double Distance,
    int MovingTime,
    int ElapsedTime,
    string Type,
    DateTime StartDate,
    DateTime StartDateLocal,
    string Timezone,
    double UtcOffset,
    double KiloJoules,
    double KiloCalories);

public record Sync(
    ObjectId _id,
    ulong[] AthletesToSync,
    ulong[] SyncedAthletes,
    DateTime Timestamp);

public record WebhookSubscriptionDto(
    ulong _id);