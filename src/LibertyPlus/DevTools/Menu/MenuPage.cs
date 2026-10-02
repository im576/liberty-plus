using System;
using System.Collections.Generic;

namespace LibertyFramework.DevTools.Menu
{
    // A DevTools page. Items are rebuilt by the factory each time the page is entered, so
    // lists that depend on config (weapons, presets, locations) always reflect the current files.
    internal sealed class MenuPage
    {
        private readonly Func<List<MenuItem>> factory;

        internal MenuPage(string title, Func<List<MenuItem>> factory)
        {
            Title = title;
            this.factory = factory;
            Items = new List<MenuItem>();
        }

        internal string Title { get; private set; }
        internal List<MenuItem> Items { get; private set; }
        internal int Selected;
        internal int Scroll;

        internal void Rebuild()
        {
            Items = factory();
            if (Selected >= Items.Count) { Selected = Math.Max(0, Items.Count - 1); }
        }
    }
}
