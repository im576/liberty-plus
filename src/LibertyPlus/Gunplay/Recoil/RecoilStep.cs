namespace LibertyFramework.Gunplay.Recoil
{
    internal struct RecoilStep
    {
        internal readonly double PitchDegrees;
        internal readonly double HeadingDegrees;

        internal RecoilStep(double pitchDegrees, double headingDegrees)
        {
            PitchDegrees = pitchDegrees;
            HeadingDegrees = headingDegrees;
        }

        internal bool IsZero { get { return PitchDegrees == 0 && HeadingDegrees == 0; } }
    }
}
