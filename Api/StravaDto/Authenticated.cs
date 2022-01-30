#nullable enable

using System;

namespace Api.StravaDto
{
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
        int? resource_state,
        string? firstname,
        string? lastname,
        string? bio,
        string? city,
        string? state,
        string? country,
        string? sex,
        bool? premium,
        bool? summit,
        DateTime? created_at,
        DateTime? updated_at,
        int? badge_type_id,
        float? weight,
        string? profile_medium,
        string? profile,
        object? friend,
        object? follower);
}

#nullable restore
