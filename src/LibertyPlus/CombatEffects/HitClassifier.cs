namespace LibertyFramework.CombatEffects
{
    internal static class HitClassifier
    {
        // GTA IV PedBone tags (same values as ScriptHookDotNet GTA.Bone). Roll, twist and finger
        // bones count toward their limb so dismemberment sees every limb hit.
        internal static HitRegion Classify(int bone)
        {
            switch (bone)
            {
                case 0x4B4: case 0x4B5: case 0x37A0: return HitRegion.Head;
                case 0x1A1: case 0x4B2: case 0x4B3: case 0x36A0: case 0x36A1: return HitRegion.Torso;
                case 0x4C0: case 0x4C1: case 0x4C2: case 0x4C3: case 0x38A0: case 0x38A1: case 0x38A2: case 0x3DF1: return HitRegion.LeftArm;
                case 0x4C7: case 0x4C8: case 0x4C9: case 0x4D0: case 0x39A0: case 0x39A1: case 0x39A2: case 0x3E01: return HitRegion.RightArm;
                case 0x1A2: case 0x1A3: case 0x1A4: case 0x1A5: case 0x38B0: return HitRegion.LeftLeg;
                case 0x1A7: case 0x1A8: case 0x1A9: case 0x4B0: case 0x39B0: return HitRegion.RightLeg;
            }
            if (bone >= 0x35D0 && bone <= 0x35E4) { return HitRegion.LeftArm; }
            if (bone >= 0x35B0 && bone <= 0x35C4) { return HitRegion.RightArm; }
            return HitRegion.Unknown;
        }
    }
}
