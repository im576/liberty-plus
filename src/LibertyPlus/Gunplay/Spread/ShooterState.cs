namespace LibertyFramework.Gunplay.Spread
{
    // Player stance/context sampled each frame; shared by spread and recoil.
    internal struct ShooterState
    {
        internal double SpeedMetersPerSecond;
        internal bool Aiming;
        internal bool Crouched;
        internal bool InCover;
        internal bool InVehicle;
        internal bool Airborne;

        internal bool BlindFire { get { return InCover && !Aiming; } }

        internal string Describe()
        {
            string text = InVehicle ? "vehicle" : InCover ? (Aiming ? "cover-aim" : "cover-blind") : Crouched ? "crouched" : "standing";
            if (!Aiming && !InCover) { text += "-hip"; }
            if (Airborne) { text += "-air"; }
            return text + " speed=" + SpeedMetersPerSecond.ToString("0.0");
        }
    }
}
