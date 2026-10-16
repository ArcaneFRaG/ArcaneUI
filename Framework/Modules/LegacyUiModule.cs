using System;

namespace ArcanesGroupFrames.Framework.Modules
{
    // Metadata bridge for existing static modules. Their established startup/update
    // sequence remains in Plugin until their native interactions are migrated safely.
    public sealed class LegacyUiModule : IUiModule
    {
        public string Id { get; private set; }
        public LegacyUiModule(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Module id required", "id");
            Id = id;
        }
        public void Initialize() { }
        public void Shutdown() { }
    }
}
