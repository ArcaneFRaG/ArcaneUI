using System;
using System.Collections.Generic;

namespace ArcanesGroupFrames.Framework.Modules
{
    // Modules are registered but never initialized until explicitly requested.
    public sealed class UiModuleRegistry
    {
        private readonly List<IUiModule> _modules = new List<IUiModule>();
        private readonly List<IUiModule> _started = new List<IUiModule>();

        public void Register(IUiModule module)
        {
            if (module == null) throw new ArgumentNullException("module");
            foreach (var existing in _modules)
                if (string.Equals(existing.Id, module.Id, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Duplicate UI module: " + module.Id);
            _modules.Add(module);
        }

        // One source of truth for the in-game module switches. The registered
        // entries remain available in settings even when the module is off.
        public bool IsEnabled(string id)
        {
            return ArcaneUiModuleSettings.IsEnabled(id);
        }

        public void SetEnabled(string id, bool enabled)
        {
            foreach (var module in _modules)
                if (string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    ArcaneUiModuleSettings.SetEnabled(module.Id, enabled);
                    return;
                }
            throw new ArgumentException("Module is not registered: " + id, "id");
        }

        public void InitializeAll()
        {
            try
            {
                foreach (var module in _modules)
                {
                    module.Initialize();
                    _started.Add(module);
                }
            }
            catch
            {
                ShutdownAll();
                throw;
            }
        }

        public void ShutdownAll()
        {
            for (int i = _started.Count - 1; i >= 0; i--)
            {
                try { _started[i].Shutdown(); }
                catch (Exception ex) { Plugin.LogError("Module shutdown failed: " + _started[i].Id + ": " + ex); }
            }
            _started.Clear();
        }
    }
}
