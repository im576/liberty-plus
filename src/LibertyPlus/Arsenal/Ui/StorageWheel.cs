using System;
using System.Collections.Generic;
using Liberty.Sdk;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.DevTools.Menu;
using LibertyFramework.Engine;

namespace LibertyFramework.Arsenal.Ui
{
    // T-046 on Liberty.Ui: the storage interface for every weapon container (trunk, safehouse stash, gunsmith). Two groups:
    // CARRIED is the same radial as the weapon wheel (sidearm, two long guns, melee, thrown: icon, ammunition, empty slots
    // read empty) and TRUNK / STASH is a list at the right with the capacity used and available. Right stick / D-pad
    // left-right pick a carried slot, LB/RB (D-pad up/down) move the cursor in the container list, X stores the highlighted
    // carried weapon, A takes the highlighted stored weapon (swapping out the carried one it displaces, which the centre
    // names before you press), Y opens the gunsmith list (safehouse), B closes. Keyboard: arrows, Page Up/Down, Space, Enter,
    // G, Backspace. Every move is a visible change on Niko: the holsters follow the carried set the same frame.
    internal sealed class StorageWheel
    {
        internal interface IHost
        {
            string Title { get; }
            IList<WeaponRecord> Carried { get; }
            IList<WeaponRecord> Stored { get; }
            // Weapons the container holds at most; 0 = unlimited.
            int Capacity { get; }
            bool GunsmithAvailable { get; }
            string Name(int weaponId);
            // The wheel segment (WeaponWheelLogic) a carried weapon sits in; -1 when it has none.
            int Segment(WeaponRecord carried);
            // The carried weapon taking `stored` would swap out, or null for a plain take.
            WeaponRecord DisplacedBy(WeaponRecord stored);
            string Store(WeaponRecord carried);
            string Take(WeaponRecord stored);
            List<MenuItem> GunsmithItems();
            // The player closed the wheel (B) or it was closed from outside.
            void WheelClosed();
        }

        // What the draw pass shows of the container; built on the tick, read on the draw thread.
        private sealed class PanelSnapshot
        {
            internal string Header, Capacity, Footer;
            internal string[] Names, Ammo;
            internal bool[] Owned;
            internal int Cursor, First, Count;
            internal bool Full;
        }

        private const int VisibleRows = 9;
        private readonly LibertyModule owner;
        private IHost host;
        private IMenu menu, gunsmith;
        private int cursor;
        private volatile PanelSnapshot panel;

        internal StorageWheel(LibertyModule owner) { this.owner = owner; }

        internal bool IsOpen { get { return menu != null && menu.IsOpen; } }

        internal void Open(IHost container)
        {
            host = container;
            cursor = 0;
            LibertyEngine engine = LibertyEngine.Current;
            RadialMenu radial = new RadialMenu();
            radial.Title = container.Title;
            radial.Size = 0.6f;
            // Arsenal locks the player's controls itself for the whole storage visit (including the lid animation).
            radial.LockPlayerControl = false;
            for (int i = 0; i < WeaponWheelLogic.SegmentCount; i++)
            {
                int slot = i;
                RadialSegment segment = new RadialSegment();
                segment.Label = () => { WeaponRecord c = Carried(slot); return c != null ? host.Name(c.WeaponId) : "-"; };
                segment.Icon = () => { WeaponRecord c = Carried(slot); return c != null ? engine.Ui.WeaponIcon(c.WeaponId) : TextureRef.None; };
                segment.Badge = () => { WeaponRecord c = Carried(slot); return c != null ? c.Ammo + "" : null; };
                radial.Segments.Add(segment);
            }
            radial.CenterLines = Centre;
            radial.OnAccept = s => Take();
            radial.OnX = Store;
            radial.OnY = s => OpenGunsmith();
            radial.OnCycle = (s, direction) => Move(direction);
            radial.OnClosed = () =>
            {
                menu = null;
                panel = null;
                CloseGunsmith();
                if (host != null) { IHost closing = host; host = null; closing.WheelClosed(); }
            };
            // Start on the first carried slot that holds a weapon, else the top.
            int selected = 0;
            for (int i = 0; i < WeaponWheelLogic.SegmentCount; i++)
            {
                if (Carried(i) != null) { selected = i; break; }
            }
            menu = engine.Ui.OpenRadial(owner, radial, selected);
            if (!menu.IsOpen) { menu = null; }
        }

        // Closing from outside does not call back WheelClosed.
        internal void Close()
        {
            host = null;
            panel = null;
            CloseGunsmith();
            if (menu == null) { return; }
            IMenu open = menu;
            menu = null;
            open.Close();
        }

        // Test and console access to the same actions the keys and buttons run (the "storage" command).
        internal string SelectSegment(int segment)
        {
            if (menu == null || !menu.IsOpen) { return "storage wheel is not open"; }
            if (segment < 0 || segment >= WeaponWheelLogic.SegmentCount) { return "segment 0-4"; }
            menu.Selected = segment;
            return "highlight " + segment;
        }

        internal string StoreHighlighted()
        {
            return menu == null || !menu.IsOpen ? "storage wheel is not open" : Store(menu.Selected);
        }

        internal string TakeAt(int index)
        {
            if (menu == null || !menu.IsOpen) { return "storage wheel is not open"; }
            cursor = index;
            return Take() ?? "nothing stored";
        }

        internal string StatusLine()
        {
            if (host == null) { return "storage_status open=False"; }
            List<string> slots = new List<string>();
            for (int i = 0; i < WeaponWheelLogic.SegmentCount; i++)
            {
                WeaponRecord c = Carried(i);
                slots.Add(c != null ? c.WeaponId.ToString() : "0");
            }
            List<string> stored = new List<string>();
            foreach (WeaponRecord record in host.Stored) { stored.Add(record.WeaponId.ToString()); }
            return "storage_status open=True selected=" + (menu != null ? menu.Selected : -1) + " container=" + host.Title + " stored=" + host.Stored.Count +
                " capacity=" + host.Capacity + " carried=" + string.Join(",", slots.ToArray()) + " list=" + (stored.Count == 0 ? "none" : string.Join(",", stored.ToArray()));
        }

        // Called from the module's draw pass: the container list at the right of the wheel.
        internal void Draw(ICanvas canvas)
        {
            PanelSnapshot p = panel;
            if (p == null) { return; }
            // Keep the top-right HUD band clear; the list ends above the radar/navigation footer band.
            float x = canvas.Width / 2f + 360f, y = 220f, width = 330f;
            if (x + width > canvas.Width - 16f) { x = canvas.Width - 16f - width; }
            Rgba amber = new Rgba(226, 150, 40, 255);
            canvas.Rect(x, y, width, 52 + VisibleRows * 34 + 40, new Rgba(12, 12, 12, 205));
            canvas.Rect(x, y, 4, 52 + VisibleRows * 34 + 40, amber);
            canvas.Text(p.Header, x + 18, y + 10, width - 30, 24, TextStyle.Emphasis, TextAlign.Left, Rgba.White);
            canvas.Text(p.Capacity, x + 18, y + 10, width - 30, 24, TextStyle.Body, TextAlign.Right, p.Full ? amber : Rgba.Muted);
            float rowY = y + 48;
            if (p.Count == 0) { canvas.Text("Empty", x + 18, rowY + 6, width - 30, 22, TextStyle.Body, TextAlign.Left, Rgba.Muted); }
            for (int i = 0; i < VisibleRows && p.First + i < p.Count; i++)
            {
                int index = p.First + i;
                bool selected = index == p.Cursor;
                if (selected) { canvas.Rect(x + 8, rowY, width - 16, 32, new Rgba(226, 150, 40, 70)); canvas.Rect(x + 8, rowY, 3, 32, amber); }
                canvas.Text(p.Names[index], x + 18, rowY + 6, width - 120, 22, TextStyle.Body, TextAlign.Left, p.Owned[index] ? Rgba.White : Rgba.Muted);
                canvas.Text(p.Ammo[index], x + width - 108, rowY + 8, 96, 20, TextStyle.Small, TextAlign.Right, Rgba.Muted);
                rowY += 34;
            }
            if (p.Count > VisibleRows)
            {
                canvas.Text((p.First > 0 ? "^ " : "") + (p.First + VisibleRows < p.Count ? "v " : "") + (p.Cursor + 1) + " / " + p.Count, x + 18, y + 52 + VisibleRows * 34 + 4, width - 30, 20, TextStyle.Small, TextAlign.Right, Rgba.Muted);
            }
            canvas.Text(p.Footer, x + 18, y + 52 + VisibleRows * 34 + 4, width - 130, 20, TextStyle.Small, TextAlign.Left, Rgba.Muted);
        }

        private WeaponRecord Carried(int segment)
        {
            if (host == null) { return null; }
            foreach (WeaponRecord record in host.Carried) { if (host.Segment(record) == segment) { return record; } }
            return null;
        }

        private WeaponRecord Cursor()
        {
            if (host == null || host.Stored.Count == 0) { return null; }
            cursor = Math.Max(0, Math.Min(cursor, host.Stored.Count - 1));
            return host.Stored[cursor];
        }

        private string[] Centre(int segment)
        {
            if (host == null) { return new string[0]; }
            WeaponRecord carried = Carried(segment);
            WeaponRecord stored = Cursor();
            string takeLine = "Nothing stored", swapLine = null;
            if (stored != null)
            {
                bool alreadyCarried = false;
                foreach (WeaponRecord record in host.Carried) { if (record.WeaponId == stored.WeaponId) { alreadyCarried = true; break; } }
                if (alreadyCarried) { takeLine = "Already carried: store first"; }
                else
                {
                    WeaponRecord displaced = host.DisplacedBy(stored);
                    takeLine = "A  " + (displaced != null ? "Swap in " : "Take ") + host.Name(stored.WeaponId);
                    if (displaced != null) { swapLine = "out: " + host.Name(displaced.WeaponId); }
                }
            }
            string hints = (carried != null ? "X Store   " : "") + "LB / RB List   " + (host.GunsmithAvailable ? "Y Gunsmith   " : "") + "B Close";
            BuildPanel(stored);
            string[] lines =
            {
                WeaponWheelLogic.SegmentTitles[segment],
                carried != null ? host.Name(carried.WeaponId) : "Empty",
                carried != null ? carried.Ammo + " rounds" + (carried.Owned ? "  -  owned" : "") : "",
                takeLine,
                hints
            };
            if (swapLine == null) { return lines; }
            return new[] { lines[0], lines[1], lines[2], lines[3], swapLine, lines[4] };
        }

        private void BuildPanel(WeaponRecord cursorRecord)
        {
            IList<WeaponRecord> stored = host.Stored;
            PanelSnapshot p = new PanelSnapshot();
            p.Header = host.Title;
            int capacity = host.Capacity;
            p.Capacity = capacity > 0 ? stored.Count + " / " + capacity : stored.Count + " stored";
            p.Full = capacity > 0 && stored.Count >= capacity;
            p.Count = stored.Count;
            p.Cursor = stored.Count == 0 ? 0 : Math.Max(0, Math.Min(cursor, stored.Count - 1));
            p.First = Math.Max(0, Math.Min(p.Cursor - VisibleRows / 2, Math.Max(0, stored.Count - VisibleRows)));
            p.Names = new string[stored.Count];
            p.Ammo = new string[stored.Count];
            p.Owned = new bool[stored.Count];
            for (int i = 0; i < stored.Count; i++)
            {
                p.Names[i] = host.Name(stored[i].WeaponId);
                p.Ammo[i] = stored[i].Ammo + "";
                p.Owned[i] = stored[i].Owned;
            }
            p.Footer = p.Full ? "Full: swap or take first" : "";
            panel = p;
        }

        private void Move(int direction)
        {
            if (host == null || host.Stored.Count == 0) { return; }
            int count = host.Stored.Count;
            cursor = ((cursor + direction) % count + count) % count;
        }

        private string Take()
        {
            WeaponRecord stored = Cursor();
            if (stored == null) { return null; }
            string result = host.Take(stored);
            cursor = host.Stored.Count == 0 ? 0 : Math.Min(cursor, host.Stored.Count - 1);
            return result;
        }

        private string Store(int segment)
        {
            WeaponRecord carried = Carried(segment);
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
