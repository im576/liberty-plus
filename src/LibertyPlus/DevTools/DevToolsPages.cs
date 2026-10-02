using System;
using System.Collections.Generic;
using LibertyFramework.DevTools.Menu;

namespace LibertyFramework.DevTools
{
    // Extension point so feature scripts add a DevTools page without editing DevToolsMenu.
    // Register once from a script constructor; pages appear on the root menu the next time it opens.
    internal static class DevToolsPages
    {
        private static readonly List<MenuPage> Registered = new List<MenuPage>();

        internal static void Register(string title, Func<List<MenuItem>> factory)
        {
            lock (Registered)
            {
                foreach (MenuPage page in Registered) { if (page.Title == title) { return; } }
                Registered.Add(new MenuPage(title, factory));
            }
        }

        internal static List<MenuPage> All()
        {
            lock (Registered) { return new List<MenuPage>(Registered); }
        }
    }
}
