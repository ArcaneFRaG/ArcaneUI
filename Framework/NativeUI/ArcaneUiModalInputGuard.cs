namespace ArcanesGroupFrames.Framework.NativeUI
{
    /// <summary>
    /// While an ArcaneUI modal is visible, use Erenshor's own "player typing"
    /// state to prevent native keyboard gameplay shortcuts from reacting to
    /// keys pressed in our UI. The modal canvases already intercept mouse
    /// input through their fullscreen raycast surfaces.
    ///
    /// Do not disable PlayerControl, native UI canvases, or the EventSystem:
    /// those changes can break targeting, input fields and gameplay state.
    /// </summary>
    internal static class ArcaneUiModalInputGuard
    {
        private static bool _settingsOpen;
        private static bool _editorOpen;
        private static bool _hasClaim;
        private static bool _previousTyping;
        private static int _pendingReleaseFrame = -1;

        internal static bool IsBlocking { get { return _settingsOpen || _editorOpen; } }

        internal static void SetSettingsOpen(bool open)
        {
            _settingsOpen = open;
            Refresh();
        }

        internal static void SetEditorOpen(bool open)
        {
            _editorOpen = open;
            Refresh();
        }

        // Reassert when a game script resets PlayerTyping between frames.
        // This deliberately does not intercept Unity Input for ArcaneUI's
        // own buttons, text fields or hotkey capture controls.
        internal static void Tick()
        {
            if (!_hasClaim) return;
            if (IsBlocking)
            {
                if (!GameData.PlayerTyping) GameData.PlayerTyping = true;
            }
            else if (_pendingReleaseFrame >= 0 && UnityEngine.Time.frameCount > _pendingReleaseFrame)
            {
                Release();
            }
        }

        private static void Refresh()
        {
            if (IsBlocking)
            {
                if (!_hasClaim)
                {
                    _previousTyping = GameData.PlayerTyping;
                    _hasClaim = true;
                }
                _pendingReleaseFrame = -1;
                GameData.PlayerTyping = true;
            }
            else if (_hasClaim && _pendingReleaseFrame < 0)
            {
                // Keep shortcuts blocked through the closing input frame:
                // the Escape that dismisses our window must not also dismiss
                // or open one of Erenshor's native windows.
                _pendingReleaseFrame = UnityEngine.Time.frameCount;
            }
        }

        private static void Release()
        {
            if (!_hasClaim) return;
            GameData.PlayerTyping = _previousTyping;
            _hasClaim = false;
            _pendingReleaseFrame = -1;
        }

        internal static void Shutdown()
        {
            _settingsOpen = false;
            _editorOpen = false;
            Release();
        }
    }
}
