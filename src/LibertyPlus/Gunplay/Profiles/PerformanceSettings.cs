using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // T-026: how often GunplayController re-asks the engine for slow-changing camera values. Each script native
    // call measured ~1-3 ms of wall time in the owner's runs, so values that rarely change are cached.
    // Absent section = refresh every tick (the old behaviour).
    [DataContract]
    internal sealed class PerformanceSettings
    {
        // Game camera handle (GET_GAME_CAM); its pool slot is re-validated from memory every tick in between.
        [DataMember(Name = "gameCameraRefreshMilliseconds", IsRequired = true)] internal double GameCameraRefreshMilliseconds;
        // Camera FOV (GET_CAM_FOV) while not aiming; while aiming it is read every tick.
        [DataMember(Name = "fovRefreshMilliseconds", IsRequired = true)] internal double FovRefreshMilliseconds;
    }
}
