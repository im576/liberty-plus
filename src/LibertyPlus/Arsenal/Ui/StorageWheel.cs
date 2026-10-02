using System;
using System.Collections.Generic;
using Liberty.Sdk;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.DevTools.Menu;
using LibertyFramework.Engine;

namespace LibertyFramework.Arsenal.Ui
{
    // S-2 on Liberty.Ui: the radial weapon wheel for every weapon container (trunk, safehouse stash, gunsmith).
    // Eight segments = the inventory categories, each showing the weapon carried in that category (its HUD icon) and a
    // "+n" badge for what the container holds. The centre shows the selected category. Right stick / D-pad left-right
    // pick a segment, LB/RB (D-pad up/down) cycle the stored weapons, A takes (swapping out what is carried), X stores,
    // Y opens the gunsmith list (safehouse), B closes. Keyboard: arrows, Enter, Space, G, Backspace.
    internal sealed class StorageWheel
    {
        internal interface IHost
        {
            string Title { get; }
            IList<WeaponRecord> Carried { get; }
            IList<WeaponRecord> Stored { get; }
            bool GunsmithAvailable { get; }
            string Name(int weaponId);
            string Store(WeaponRecord carried);
            string Take(WeaponRecord stored);
            List<MenuItem> GunsmithItems();
            // The player closed the wheel (B) or it was closed from outside.
            void WheelClosed();
        }

        // Clockwise from the top.
        private static readonly WeaponCategory[] Order =
        {
            WeaponCategory.Handgun, WeaponCategory.SMG, WeaponCategory.Rifle, WeaponCategory.Sniper,
            WeaponCategory.Heavy, WeaponCategory.Thrown, WeaponCategory.Melee, WeaponCategory.Shotgun
        };
        private static readonly string[] CategoryNames = { "", "MELEE", "PISTOLS", "SHOTGUNS", "SUBMACHINE GUNS", "RIFLES", "SNIPER RIFLES", "HEAVY", "THROWN", "OTHER" };

        private readonly LibertyModule owner;
        private IHost host;
        private IMenu menu, gunsmith;
        private int storedIndex, lastSegment = -1;

        internal StorageWheel(LibertyModule owner) { this.owner = owner; }

        internal bool IsOpen { get { return menu != null && menu.IsOpen; } }

        internal void Open(IHost container)
        {
            host = container;
            storedIndex = 0;
            lastSegment = -1;
            LibertyEngine engine = LibertyEngine.Current;
            RadialMenu radial = new RadialMenu();
            radial.Title = container.Title;
            // Arsenal locks the player's controls itself for the whole storage visit (including the lid animation).
            radial.LockPlayerControl = false;
            for (int i = 0; i < Order.Length; i++)
            {
                WeaponCategory category = Order[i];
                RadialSegment segment = new RadialSegment();
                segment.Label = () => { WeaponRecord c = Carried(category); return c != null ? host.Name(c.WeaponId) : "-"; };
                segment.Icon = () => { WeaponRecord c = Carried(category); return c != null ? engine.Ui.WeaponIcon(c.WeaponId) : TextureRef.None; };
                segment.Badge = () => { int n = Stored(category).Count; return n > 0 ? "+" + n : null; };
                radial.Segments.Add(segment);
            }
            radial.CenterLines = Centre;
            radial.OnAccept = Take;
            radial.OnX = Store;
            radial.OnY = s => OpenGunsmith();
            radial.OnCycle = Cycle;
            radial.OnClosed = () =>
            {
                menu = null;
                CloseGunsmith();
                if (host != null) { IHost closing = host; host = null; closing.WheelClosed(); }
            };
            menu = engine.Ui.OpenRadial(owner, radial);
            // Start on the first category that holds something (carried or stored), else the top.
            for (int i = 0; i < Order.Length; i++)
            {
                if (Carried(Order[i]) != null || Stored(Order[i]).Count > 0) { menu.Selected = i; break; }
            }
        }

        // Closing from outside does not call back WheelClosed.
        internal void Close()
        {
            host = null;
            CloseGunsmith();
            if (menu == null) { return; }
            IMenu open = menu;
            menu = null;
            open.Close();
        }

        private WeaponRecord Carried(WeaponCategory category)
        {
            if (host == null) { return null; }
            foreach (WeaponRecord record in host.Carried) { if (record.Category == category) { return record; } }
            return null;
        }

        private List<WeaponRecord> Stored(WeaponCategory category)
        {
            List<WeaponRecord> result = new List<WeaponRecord>();
            if (host == null) { return result; }
            foreach (WeaponRecord record in host.Stored) { if (record.Category == category) { result.Add(record); } }
            return result;
        }

        private string[] Centre(int segment)
        {
            if (host == null) { return new string[0]; }
            if (segment != lastSegment) { lastSegment = segment; storedIndex = 0; }
            WeaponCategory category = Order[segment];
            WeaponRecord carried = Carried(category);
            List<WeaponRecord> stored = Stored(category);
            string storedLine = "Nothing stored in this slot";
            if (stored.Count > 0)
            {
                int index = Math.Min(storedIndex, stored.Count - 1);
                storedLine = "In here: " + host.Name(stored[index].WeaponId) + "  " + stored[index].Ammo +
                    (stored.Count > 1 ? "  (" + (index + 1) + "/" + stored.Count + ")" : "");
            }
            string hints = (stored.Count > 0 ? (carried != null ? "A Swap   " : "A Take   ") : "") + (carried != null ? "X Store   " : "") +
                (host.GunsmithAvailable ? "Y Gunsmith   " : "") + "B Close";
            return new[]
            {
                CategoryNames[(int)category],
                carried != null ? host.Name(carried.WeaponId) : "Empty",
                carried != null ? carried.Ammo + " rounds" + (carried.Owned ? "  -  owned" : "") : "",
                storedLine,
                hints
            };
        }

        private void Cycle(int segment, int direction)
        {
            int count = Stored(Order[segment]).Count;
            if (count > 0) { storedIndex = ((storedIndex + direction) % count + count) % count; }
        }

        private string Take(int segment)
        {
            List<WeaponRecord> stored = Stored(Order[segment]);
            if (stored.Count == 0) { return null; }
            string result = host.Take(stored[Math.Min(storedIndex, stored.Count - 1)]);
            storedIndex = 0;
            return result;
        }

        private string Store(int segment)
        {
            WeaponRecord carried = Carried(Order[segment]);
            return carried != null ? host.Store(carried) : "Nothing carried in this slot";
        }

        private string OpenGunsmith()
        {
            if (host == null || !host.GunsmithAvailable || (gunsmith != null && gunsmith.IsOpen)) { return null; }
            ListMenu list = new ListMenu();
            list.Title = "GUNSMITH";
            list.LockPlayerControl = false;
            list.Items = () =>
            {
                List<ListItem> items = new List<ListItem>();
                if (host == null) { return items; }
                foreach (MenuItem item in host.GunsmithItems())
                {
                    if (item.Activate == null) { continue; }
                    ListItem row = new ListItem();
                    row.Label = item.Label;
                    row.OnSelect = item.Activate;
                    row.Confirm = item.RequiresConfirmation;
                    items.Add(row);
                }
                if (items.Count == 0) { items.Add(ListItem.Info(() => "Nothing to work on")); }
                return items;
            };
            list.OnClosed = () => gunsmith = null;
            gunsmith = LibertyEngine.Current.Ui.OpenList(owner, list);
            return null;
        }

        private void CloseGunsmith()
        {
            if (gunsmith == null) { return; }
            IMenu open = gunsmith;
            gunsmith = null;
            open.Close();
        }
    }
}
