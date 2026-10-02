using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    // T-046: how many weapons a vehicle's trunk holds, by vehicle class. The optional "trunkCapacity" block of arsenal.json;
    // without it (an install that predates it) every trunk uses DefaultSlots and safehouses are unlimited.
    [DataContract]
    internal sealed class TrunkCapacityRules
    {
        [DataMember(Name = "defaultSlots", IsRequired = true)] internal int DefaultSlots;
        // Weapons a safehouse stash holds; 0 = unlimited.
        [DataMember(Name = "safehouseSlots", IsRequired = true)] internal int SafehouseSlots;
        [DataMember(Name = "classes", IsRequired = false)] internal List<TrunkClass> Classes;

        internal static TrunkCapacityRules Defaults()
        {
            TrunkCapacityRules rules = new TrunkCapacityRules();
            rules.DefaultSlots = 8; rules.SafehouseSlots = 0; rules.Classes = new List<TrunkClass>();
            return rules;
        }

        internal void Validate()
        {
            if (DefaultSlots < 1 || DefaultSlots > 99 || SafehouseSlots < 0 || SafehouseSlots > 999) { throw new InvalidDataException("trunkCapacity slots out of range"); }
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> models = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (Classes == null) { return; }
            foreach (TrunkClass vehicleClass in Classes)
            {
                if (vehicleClass == null || string.IsNullOrEmpty(vehicleClass.Id) || !ids.Add(vehicleClass.Id) || vehicleClass.Slots < 1 || vehicleClass.Slots > 99 ||
                    vehicleClass.Models == null || vehicleClass.Models.Count == 0)
                { throw new InvalidDataException("trunkCapacity classes need a unique id, 1-99 slots and models"); }
                foreach (string model in vehicleClass.Models)
                {
                    if (string.IsNullOrEmpty(model) || !models.Add(model)) { throw new InvalidDataException("trunkCapacity model listed twice or empty: " + model); }
                }
            }
        }

        // Slots of the trunk of a vehicle model; `modelName` is the game's model name.
        internal int SlotsFor(string modelName)
        {
            if (Classes != null && !string.IsNullOrEmpty(modelName))
            {
                foreach (TrunkClass vehicleClass in Classes)
                {
                    foreach (string model in vehicleClass.Models) { if (string.Equals(model, modelName, System.StringComparison.OrdinalIgnoreCase)) { return vehicleClass.Slots; } }
                }
            }
            return DefaultSlots;
        }

        // 0 = unlimited. A full container refuses a store but still allows a swap (one out, one in).
        internal static bool CanStore(int stored, int capacity) { return capacity <= 0 || stored < capacity; }
    }

    [DataContract]
    internal sealed class TrunkClass
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "slots", IsRequired = true)] internal int Slots;
        [DataMember(Name = "models", IsRequired = true)] internal List<string> Models;
    }
}
