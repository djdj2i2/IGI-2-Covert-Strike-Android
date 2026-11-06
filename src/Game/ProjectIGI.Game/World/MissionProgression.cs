using ProjectIGI.Game.Config;

using ProjectIGI.Engine.Titles;

namespace ProjectIGI.Game.World;

/// <summary>Retail campaign-unlock semantics from Config_UpdateActiveMissionID (IGI1 0x406340).</summary>
public static class MissionProgression
{
    /// <summary>Advances the profile to <paramref name="nextMissionId"/> only when it lies ahead of its frontier.</summary>
    public static bool UnlockNext(PlayerProfile profile, IMissionCatalogue<MissionDescriptor> missions, int nextMissionId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(missions);
        if (nextMissionId == MissionDescriptor.NextMissionUndefined)
        {
            return false;
        }

        // The next mission must itself be on the campaign, after the active one. Unlocking on merely passing
        // the active mission unlocked a mission the catalogue does not offer (the browser build's
        // six-mission limit let the sixth unlock the seventh). A profile whose active mission is on no
        // mission of this campaign — a fresh I.G.I. 2 profile's shared ActiveMission is 1, which is no
        // mission of the sequel's — counts as standing on the first mission: without that, the first
        // completion could never unlock the second, because the walk would never pass the active one.
        // The same reading as <see cref="Status"/> gives an unknown frontier: the first mission.
        int frontier = 0;
        int wanted = -1;
        int index = 0;
        foreach (MissionDescriptor mission in missions.InProgressionOrder())
        {
            if (mission.Id == profile.ActiveMission)
            {
                frontier = index;
            }

            if (mission.Id == nextMissionId)
            {
                wanted = index;
            }

            index++;
        }

        if (wanted < 0 || wanted <= frontier)
        {
            return false;
        }

        profile.ActiveMission = nextMissionId;
        return true;
    }

    /// <summary>Gets a mission's campaign status when ActiveMission means the furthest unlocked mission.</summary>
    public static MissionCampaignStatus Status(IMissionCatalogue<MissionDescriptor> missions, int activeMissionId, int missionId)
    {
        int frontier = -1;
        int wanted = -1;
        int index = 0;
        foreach (MissionDescriptor mission in missions.InProgressionOrder())
        {
            if (mission.Id == activeMissionId)
            {
                frontier = index;
            }
            if (mission.Id == missionId)
            {
                wanted = index;
            }
            index++;
        }

        if (frontier < 0)
        {
            frontier = 0;
        }
        if (wanted < 0)
        {
            return MissionCampaignStatus.Locked;
        }
        return wanted < frontier ? MissionCampaignStatus.Completed
            : wanted == frontier ? MissionCampaignStatus.Available
            : MissionCampaignStatus.Locked;
    }
}

/// <summary>A mission's state relative to the profile's furthest unlocked mission.</summary>
public enum MissionCampaignStatus
{
    /// <summary>The profile has not reached this mission.</summary>
    Locked,
    /// <summary>This is the profile's current furthest unlocked mission.</summary>
    Available,
    /// <summary>This mission precedes the profile's current frontier.</summary>
    Completed,
}
