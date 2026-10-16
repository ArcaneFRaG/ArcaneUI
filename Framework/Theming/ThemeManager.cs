using System;

namespace ArcanesGroupFrames.Framework.Theming
{
    // The legacy UI keeps its existing colours until each module explicitly opts in.
    public sealed class ThemeManager
    {
        private readonly ThemeCatalog _catalog = new ThemeCatalog();
        public UiTheme Current { get; private set; }
        public event Action<UiTheme> ThemeChanged;

        public ThemeManager() { Current = _catalog.Get("dark"); }
        public bool SetTheme(string id)
        {
            UiTheme next;
            if (!_catalog.TryGet(id, out next)) return false;
            if (ReferenceEquals(Current, next)) return true;
            Current = next;
            var changed = ThemeChanged;
            if (changed != null) changed(next);
            return true;
        }
        public UiTheme GetTheme(string id) { return _catalog.Get(id); }
    }
}
