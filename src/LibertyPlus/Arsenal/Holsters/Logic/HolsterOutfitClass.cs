using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Arsenal.Holsters.Logic
{
    // T-044: a group of Niko outfits that need the same holster offsets (a bulky coat pushes a slung gun further out than
    // a shirt does). Niko is in the class when the drawable he wears in `component` (GET_CHAR_DRAWABLE_VARIATION) is listed.
    [DataContract]
    internal sealed class HolsterOutfitClass
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "component", IsRequired = true)] internal int Component;
        [DataMember(Name = "drawables", IsRequired = true)] internal int[] Drawables;

        internal bool Matches(int component, int drawable)
        {
            if (component != Component) { return false; }
            foreach (int listed in Drawables) { if (listed == drawable) { return true; } }
            return false;
        }
    }
}
