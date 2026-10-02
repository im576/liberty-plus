using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Gunplay.Spread;

namespace LibertyFramework.Gunplay.Recoil
{
    internal static class RecoilMultiplier
    {
        internal static double For(RecoilProfile profile, MovementSettings movement, ShooterState state)
        {
            double multiplier = 1.0;
            if (state.InVehicle) { multiplier *= profile.VehicleMultiplier; }
            else if (state.InCover) { multiplier *= profile.CoverMultiplier; }
            else if (state.Crouched) { multiplier *= profile.CrouchedMultiplier; }
            if (!state.Aiming && !state.InVehicle) { multiplier *= profile.HipFireMultiplier; }
            double moving = SpreadModel.MovementFactor(movement, state.SpeedMetersPerSecond);
            multiplier *= 1.0 + (profile.MovingMultiplier - 1.0) * moving;
            return multiplier;
        }
    }
}
