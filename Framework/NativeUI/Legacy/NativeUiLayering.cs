using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class NativeUiLayering
    {
        // Keep gameplay-facing addon UI below Erenshor's native overlay
        // canvases. ScreenSpaceOverlay canvases still render above the 3D
        // world, but native UI with the normal/default sorting order wins.
        internal const int GameplayCanvasSortingOrder =
            -100;


        private static bool _initialized;

        private static bool _sceneTransitionSuppressed;

        private static float _releaseAfter;

        private static float _nextLoadingScan;

        private static bool _loadingScreenVisible;


        internal static bool CanShowGameplayUi
        {
            get
            {
                // Consult scene state even between the 0.1-second hierarchy
                // scans. Erenshor uses a scene named "LoadScene" during zones.
                return !_sceneTransitionSuppressed &&
                       !_loadingScreenVisible &&
                       !IsInNonGameplayScene() &&
                       !GameData.InCharSelect &&
                       GameData.PlayerStats != null &&
                       GameData.PlayerControl != null;
            }
        }

        private static bool IsInNonGameplayScene()
        {
            // Loading can use an additive scene, leaving the previous world
            // active briefly. Check loaded scenes as well as the active scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && IsNonGameplaySceneName(scene.name))
                    return true;
            }
            return IsNonGameplaySceneName(SceneManager.GetActiveScene().name);
        }

        private static bool IsNonGameplaySceneName(string sceneName)
        {
            switch (NormalizeName(sceneName))
            {
                case "loadscene":
                case "loadingscene":
                case "loading":
                case "loadingscreen":
                case "loadscreen":
                case "menu":
                case "mainmenu":
                case "characterselect":
                case "charselect":
                    return true;
                default:
                    return false;
            }
        }


        internal static void Initialize()
        {
            if (_initialized)
            {
                return;
            }


            _initialized =
                true;


            SceneManager.sceneUnloaded +=
                OnSceneUnloaded;

            SceneManager.sceneLoaded +=
                OnSceneLoaded;

            SceneManager.activeSceneChanged +=
                OnActiveSceneChanged;


            _sceneTransitionSuppressed =
                false;

            _releaseAfter =
                0f;

            _nextLoadingScan =
                0f;

            _loadingScreenVisible =
                DetectLoadingScreen();
        }


        internal static void Update()
        {
            if (!_initialized)
            {
                Initialize();
            }


            if (Time.unscaledTime >=
                _nextLoadingScan)
            {
                _nextLoadingScan =
                    Time.unscaledTime +
                    0.10f;


                _loadingScreenVisible =
                    DetectLoadingScreen();
            }


            if (_loadingScreenVisible)
            {
                // Keep a short grace period after the loading canvas goes
                // away; the new scene can still be arranging its native UI.
                SuppressForTransition(0.35f);
                return;
            }


            if (_sceneTransitionSuppressed &&
                Time.unscaledTime >=
                    _releaseAfter)
            {
                _sceneTransitionSuppressed =
                    false;
            }
        }


        private static void OnSceneUnloaded(
            Scene scene)
        {
            SuppressForTransition(
                0.50f);
        }


        private static void OnSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            // Give native scene/UI setup a short head start before the
            // persistent addon canvases are allowed to reappear.
            SuppressForTransition(
                0.85f);
        }


        private static void OnActiveSceneChanged(
            Scene previous,
            Scene current)
        {
            SuppressForTransition(
                0.85f);
        }


        private static void SuppressForTransition(
            float minimumSeconds)
        {
            _sceneTransitionSuppressed =
                true;


            float requestedRelease =
                Time.unscaledTime +
                Mathf.Max(
                    0f,
                    minimumSeconds);


            if (requestedRelease >
                _releaseAfter)
            {
                _releaseAfter =
                    requestedRelease;
            }
        }


        private static bool DetectLoadingScreen()
        {
            if (IsInNonGameplayScene()) return true;

            // Most native loading panels are descendants of the persistent
            // UI Canvas, not the Canvas itself. Scanning only Canvas parents
            // missed those panels and let the Player Frame show during loads.
            UnityEngine.Canvas[] canvases =
                UnityEngine.Object.FindObjectsOfType<UnityEngine.Canvas>();

            for (int i = 0; i < canvases.Length; i++)
            {
                UnityEngine.Canvas canvas = canvases[i];
                if (canvas == null || !canvas.gameObject.activeInHierarchy ||
                    IsOurCanvas(canvas.transform))
                    continue;

                if (HasVisibleLoadingHierarchy(canvas.transform)) return true;
            }
            return false;
        }

        private static bool IsOurCanvas(Transform candidate)
        {
            for (Transform t = candidate; t != null; t = t.parent)
            {
                string name = t.name ?? "";
                if (name.StartsWith("ArcaneUI_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("ArcanesGroupFrames_", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool HasVisibleLoadingHierarchy(Transform canvasRoot)
        {
            // Includes the root and ACTIVE descendants only. We intentionally
            // don't examine inactive loading panels kept around by the game.
            Transform[] children = canvasRoot.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < children.Length; i++)
            {
                Transform t = children[i];
                if (t == null || !t.gameObject.activeInHierarchy || IsOurCanvas(t))
                    continue;

                string normalized = NormalizeName(t.name);
                bool isLoadingContainer =
                    normalized.Contains("loadingscreen") ||
                    normalized.Contains("loadscreen") ||
                    normalized.Contains("loadingcanvas") ||
                    normalized.Contains("loadingpanel") ||
                    normalized == "loadscene";
                bool isLoadingLabel = normalized == "loading";
                if (!isLoadingContainer && !isLoadingLabel) continue;
                if (!IsEffectivelyVisible(t)) continue;

                // A persistent loading panel might be active yet hidden via a
                // CanvasGroup. Require at least one visible Graphic to avoid
                // treating unused placeholder UI as a live loading screen.
                if (HasVisibleGraphic(t)) return true;
            }
            return false;
        }

        private static bool IsEffectivelyVisible(Transform transform)
        {
            // Check ancestor groups, including the loading container itself.
            for (Transform t = transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeInHierarchy) return false;
                CanvasGroup[] groups = t.GetComponents<CanvasGroup>();
                for (int i = 0; i < groups.Length; i++)
                    if (groups[i] != null && groups[i].enabled && groups[i].alpha <= 0.01f)
                        return false;
            }
            return true;
        }

        private static bool HasVisibleGraphic(Transform root)
        {
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(false);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || !graphic.isActiveAndEnabled ||
                    graphic.color.a <= 0.01f) continue;
                if (IsEffectivelyVisible(graphic.transform)) return true;
            }
            return false;
        }


        private static string NormalizeName(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return "";
            }


            return value
                .Replace(
                    " ",
                    "")
                .Replace(
                    "_",
                    "")
                .Replace(
                    "-",
                    "")
                .ToLowerInvariant();
        }


        internal static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }


            SceneManager.sceneUnloaded -=
                OnSceneUnloaded;

            SceneManager.sceneLoaded -=
                OnSceneLoaded;

            SceneManager.activeSceneChanged -=
                OnActiveSceneChanged;


            _initialized =
                false;

            _sceneTransitionSuppressed =
                false;

            _loadingScreenVisible =
                false;

            _releaseAfter =
                0f;

            _nextLoadingScan =
                0f;
        }
    }
}
