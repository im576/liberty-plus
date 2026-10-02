using LibertyFramework.Arsenal.Contracts;

namespace LibertyFramework.Arsenal.Logic
{
    // T-045: what the weapon wheel's segments are and how a press ends. Pure logic (offline-tested).
    internal static class WeaponWheelLogic
    {
        internal const int Sidearm = 0, LongGun1 = 1, LongGun2 = 2, Melee = 3, Thrown = 4, SegmentCount = 5;

        internal static readonly string[] SegmentTitles = { "SIDEARM", "LONG GUN", "LONG GUN", "MELEE", "THROWN" };

        // The segment a carried weapon belongs to, or -1 for a weapon the wheel does not show. The wheel shows the physical
        // loadout: the sidearm slot, the two long-gun slots, melee and thrown. A second sidearm (general rules) has no segment.
        internal static int SegmentOf(BodySlot slot, WeaponCategory category)
        {
            switch (slot)
            {
                case BodySlot.SidearmPrimary: return Sidearm;
                case BodySlot.LongGun1: return LongGun1;
                case BodySlot.LongGun2: return LongGun2;
                case BodySlot.Melee: return Melee;
            }
            return category == WeaponCategory.Thrown ? Thrown : -1;
        }

        internal enum Release { Equip, StayOpen }

        // A press that ends before tapMilliseconds keeps the wheel open for stick/arrow selection; a longer one is a hold:
        // releasing it equips the highlighted slot.
        internal static Release OnRelease(long heldMilliseconds, int tapMilliseconds)
        {
            return heldMilliseconds < tapMilliseconds ? Release.StayOpen : Release.Equip;
        }

        // Where the highlight starts: the slot of the weapon in hand, else the first filled slot, else the top.
        internal static int StartSegment(int[] weaponIdBySegment, int heldWeaponId)
        {
            for (int i = 0; i < weaponIdBySegment.Length; i++) { if (weaponIdBySegment[i] > 0 && weaponIdBySegment[i] == heldWeaponId) { return i; } }
            for (int i = 0; i < weaponIdBySegment.Length; i++) { if (weaponIdBySegment[i] > 0) { return i; } }
            return 0;
        }
    }
}
