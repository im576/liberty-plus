using System;

namespace LibertyFramework.DevTools.Menu
{
    // One row in a DevTools page. Label is re-evaluated every frame so values stay live.
    internal sealed class MenuItem
    {
        internal Func<string> Label;
        internal Func<string> Activate;
        internal Func<int, string> Adjust;
        internal MenuPage Submenu;
        internal bool RequiresConfirmation;

        internal static MenuItem Action(string label, Func<string> activate)
        {
            MenuItem item = new MenuItem();
            item.Label = () => label;
            item.Activate = activate;
            return item;
        }

        internal static MenuItem Confirmed(string label, Func<string> activate)
        {
            MenuItem item = Action(label, activate);
            item.RequiresConfirmation = true;
            return item;
        }

        internal static MenuItem Page(MenuPage page)
        {
            MenuItem item = new MenuItem();
            item.Label = () => page.Title + "  >";
            item.Submenu = page;
            return item;
        }

        internal static MenuItem Toggle(string label, Func<bool> get, Action<bool> set)
        {
            MenuItem item = new MenuItem();
            item.Label = () => label + ": " + (get() ? "ON" : "OFF");
            item.Activate = () => { set(!get()); return label + " " + (get() ? "ON" : "OFF"); };
            item.Adjust = direction => { set(direction > 0); return label + " " + (get() ? "ON" : "OFF"); };
            return item;
        }

        internal static MenuItem Info(Func<string> label)
        {
            MenuItem item = new MenuItem();
            item.Label = label;
            return item;
        }
    }
}
