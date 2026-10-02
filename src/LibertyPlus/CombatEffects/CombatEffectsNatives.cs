using GTA;
using GTA.Native;

namespace LibertyFramework.CombatEffects
{
    internal static class CombatEffectsNatives
    {
        internal static bool IsMissionPed(Ped ped)
        {
            return Function.Call<bool>("IS_PED_A_MISSION_PED", ped);
        }

        internal static void React(Ped ped, float x, float y, float z)
        {
            Function.Call("APPLY_FORCE_TO_PED", ped, 3, x, y, z, 0.0f, 0.0f, 0.0f, 0, 1, 1, 1);
        }

        // (name, ped, offset xyz, rotation xyz in degrees, bone, SCALE). The CE handler reads the last argument
        // with movss (0xBD678D): it is a float scale. Passing an integer 0 made every earlier effect invisible.
        // The engine refuses looping effects here (0xAA0D6E); those go through Start.
        internal static bool Burst(string name, Ped ped, int bone, float scale)
        {
            if (string.IsNullOrEmpty(name) || scale <= 0) { return false; }
            return Function.Call<bool>("TRIGGER_PTFX_ON_PED_BONE", name, ped, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, bone, scale);
        }

        // Same arguments; starts a persistent (looping) effect attached to the bone and returns its id (0 = failed).
        internal static int Start(string name, Ped ped, int bone, float scale)
        {
            if (string.IsNullOrEmpty(name) || scale <= 0) { return 0; }
            return Function.Call<int>("START_PTFX_ON_PED_BONE", name, ped, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, bone, scale);
        }

        internal static void Stop(int handle)
        {
            if (handle != 0) { Function.Call("STOP_PTFX", handle); }
        }

        // The game's own wound bleeding on a ped (a leaking body, as for scripted mission injuries).
        internal static void SetBleeding(Ped ped, bool on)
        {
            Function.Call("SET_CHAR_BLEEDING", ped, on);
        }

        internal static void RemoveHead(Ped ped)
        {
            Function.Call("EXPLODE_CHAR_HEAD", ped);
        }
    }
}
