using System;
using System.Collections.Generic;

namespace ArcanesGroupFrames.Framework.NativeUI
{
    // Lifecycle bookkeeping only. Native objects must be restored by their
    // existing visibility adapters, not by blindly re-enabling GameObjects.
    public sealed class NativeUiReplacementState
    {
        private readonly HashSet<string> _replaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsReplacing(string id) { return _replaced.Contains(id); }
        public void MarkReplaced(string id) { if (!string.IsNullOrEmpty(id)) _replaced.Add(id); }
        public void MarkRestored(string id) { if (!string.IsNullOrEmpty(id)) _replaced.Remove(id); }
        public void Clear() { _replaced.Clear(); }
    }
}
