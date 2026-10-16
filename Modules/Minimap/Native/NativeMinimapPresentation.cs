using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Lunaris;
using ArcanesGroupFrames.Modules.Minimap.Configuration;
using ArcanesGroupFrames.Framework.Theming;

namespace ArcanesGroupFrames.Modules.Minimap.Native
{
    /// <summary>
    /// Controls the native minimap viewport's stencil *geometry* when its
    /// hierarchy can be verified. Never substitutes its original map sprite. The 800x800 map source is never reparented,
    /// scaled, replaced or used as an overlay. The border follows the actual
    /// viewport, not its larger parent containing the zoom/map buttons.
    /// </summary>
    internal sealed class NativeMinimapPresentation
    {
        private readonly ThemeManager _themes;
        private readonly ArcaneUiMinimapReplacement _replacement;
        private RectTransform _native; // Verified bounded HUD container ONLY.
        private RectTransform _surface; // Locator, never transformed.
        private Vector2 _originalPosition;
        private Vector3 _originalScale;
        private bool _appliedMove;
        private bool _appliedScale;
        private RectTransform _frame;
        private MinimapFrameGraphic _frameGraphic;
        // A dedicated group is intentionally added instead of modifying any
        // native CanvasGroup values; removal restores the original hierarchy.
        private CanvasGroup _visibilityGroup;
        private RectTransform _visibilityRoot;
        private RectTransform _viewport;
        private Mask _nativeMask;
        private Image _maskImage;
        // Clip is reshaped through the native Image's stencil mesh. Do not
        // replace/hide its original sprite: it may contain map terrain artwork.
        private MinimapMaskGeometry _maskGeometry;
        // The original viewport has a circular MapMask image. Square mode must
        // bypass that native circle completely, not merely change its mesh.
        private RectMask2D _squareRectClip;
        private bool _ownsSquareRectClip;
        private bool _squareClipActive;
        private bool _originalMaskEnabled;
        private bool _originalMaskImageEnabled;
        private string _appliedShape;
        private bool _ownsShape;
        private string _shapeStatus = "Map mask not inspected.";
        private float _nextScan;
        private float _nextApply;
        private string _status = "Minimap not yet discovered.";
        private string _path = "Unbound";
        internal string Status { get { return _status + (MinimapSettings.ShowCustom ?
            " | " + _replacement.Status + (_replacement.Ready ? " | Native HUD hidden; backend active" : " | Native fallback visible") :
            (MinimapSettings.Enabled ? (_ownsShape ? " | Native viewport shape: " + _appliedShape : " | Shape not applied") : " | Skin disabled")); } }
        internal string BoundPath { get { return _path; } }
        internal RectTransform EditorRect { get { return MinimapSettings.ShowCustom && _replacement.Ready ? _replacement.EditorRect : (IsUsable(_native) ? _native : null); } }
        internal bool CanEdit { get { return EditorRect != null; } }

        internal NativeMinimapPresentation(ThemeManager themes) { _themes = themes; _replacement = new ArcaneUiMinimapReplacement(themes); }

        // KeyDown must be examined every frame (the map refresh is throttled).
        // Modal settings, the layout editor and text entry always take priority.
        private void TickEnlargeShortcut()
        {
            if (!MinimapSettings.ShowCustom || !MinimapSettings.MKeyEnlarge || !_replacement.Ready)
                return;
            if (GameData.PlayerTyping ||
                ArcanesGroupFrames.Framework.NativeUI.ArcaneUiModalInputGuard.IsBlocking)
                return;
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null)
            {
                var selected = events.currentSelectedGameObject;
                InputField input = selected.GetComponentInParent<InputField>();
                TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
                if ((input != null && input.isFocused) || (tmpInput != null && tmpInput.isFocused))
                    return;
            }
            if (Input.GetKeyDown(KeyCode.M)) _replacement.ToggleExpanded();
        }

        internal void Update()
        {
            TickEnlargeShortcut();
            if (Time.unscaledTime < _nextApply) return;
            _nextApply = Time.unscaledTime + .15f;
            if (!IsUsable(_native))
            {
                Release(true);
                if (Time.unscaledTime < _nextScan) return;
                _nextScan = Time.unscaledTime + 2f;
                Discover();
            }
            Apply();
        }
        internal void EnsureBinding()
        {
            if (IsUsable(_native)) return;
            _nextScan = Time.unscaledTime + 1f;
            Discover();
            Apply();
        }
        internal void Refresh() { EnsureBinding(); Apply(); }
        internal void Restore()
        {
            Release(true);
            _nextScan = Time.unscaledTime + 1f;
        }
        internal void Shutdown() { Release(true); }
        private static bool IsUsable(RectTransform rect)
        {
            return rect != null && rect.gameObject != null && rect.gameObject.activeInHierarchy;
        }
        private static string Path(Transform t)
        {
            string s = "";
            for (int i = 0; t != null && i < 16; ++i, t = t.parent) s = "/" + t.name + s;
            return s;
        }
        private static bool NameMatches(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            return n.Contains("minimap") || n.Contains("radarmap") || n == "radar";
        }
        private static bool IsKnownNativeMap(RectTransform r, string path)
        {
            if (!IsUsable(r) || r.parent == null) return false;
            if (!string.Equals(path, "/ui/map par/mapbg/gameobject/minimap",
                StringComparison.OrdinalIgnoreCase)) return false;
            Vector2 size = r.rect.size;
            return size.x >= 256f && size.y >= 256f &&
                size.x <= 1600f && size.y <= 1600f &&
                size.x / Mathf.Max(1f, size.y) >= .75f &&
                size.x / Mathf.Max(1f, size.y) <= 1.3334f;
        }
        private static int Score(RectTransform r)
        {
            if (!NameMatches(r.name)) return -1;
            string path = Path(r).ToLowerInvariant();
            if (path.Contains("arcaneui") || path.Contains("preview") || path.Contains("editor") ||
                path.Contains("worldmap") || path.Contains("fullmap")) return -1;
            if (IsKnownNativeMap(r, path)) return 100;
            // Generic candidates are inspected, but never edited without a verified
            // map-specific ancestor. Avoid guessing from an 800px surface name.
            return -1;
        }
        private static Rect ScreenRect(RectTransform r)
        {
            if (r == null) return new Rect();
            Vector3[] corners = new Vector3[4];
            r.GetWorldCorners(corners);
            Canvas canvas = r.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            float left = a.x, right = a.x, bottom = a.y, top = a.y;
            for (int i = 1; i < 4; ++i)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x);
                bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y);
            }
            return new Rect(left, bottom, right - left, top - bottom);
        }
        private static bool SmallHudBounds(RectTransform r)
        {
            if (!IsUsable(r) || Screen.width < 640 || Screen.height < 480) return false;
            Rect b = ScreenRect(r);
            // Native HUD minimap is a small view. Never move an 800px render
            // surface, full-screen canvas, world-map panel, or offscreen container.
            float maxW = Mathf.Min(510f, Screen.width * .48f);
            float maxH = Mathf.Min(510f, Screen.height * .62f);
            if (b.width < 90f || b.height < 90f || b.width > maxW || b.height > maxH) return false;
            if (b.xMax < 0f || b.xMin > Screen.width || b.yMax < 0f || b.yMin > Screen.height) return false;
            return true;
        }
        private static bool KnownHudAncestor(RectTransform r)
        {
            if (r == null) return false;
            return string.Equals(r.name, "GameObject", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.name, "MapBG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.name, "MAP PAR", StringComparison.OrdinalIgnoreCase);
        }
        private static RectTransform FindSafeHudRoot(RectTransform surface)
        {
            if (surface == null || !IsKnownNativeMap(surface, Path(surface))) return null;
            RectTransform result = null;
            for (Transform t = surface.parent; t != null; t = t.parent)
            {
                RectTransform r = t as RectTransform;
                if (r == null) break;
                if (r.GetComponent<Canvas>() != null) break; // Never shift the whole UI canvas.
                if (!KnownHudAncestor(r)) break;
                if (!SmallHudBounds(r)) continue;
                // Select the OUTERMOST bounded map-only root: moving the source
                // child would leave its viewport, masks and other map widgets behind.
                result = r;
            }
            return result;
        }
        private void Discover()
        {
            RectTransform bestSurface = null;
            int bestScore = -1;
            var all = Resources.FindObjectsOfTypeAll<RectTransform>();
            for (int i = 0; i < all.Length; ++i)
            {
                RectTransform candidate = all[i];
                if (candidate == null || !candidate.gameObject.scene.IsValid()) continue;
                int score = Score(candidate);
                if (score <= bestScore) continue;
                bestSurface = candidate;
                bestScore = score;
            }
            _surface = bestSurface;
            if (bestSurface == null)
            {
                _status = "Native map render surface not found. Enable the native minimap, then INSPECT.";
                _path = "Unbound";
                return;
            }
            RectTransform hud = FindSafeHudRoot(bestSurface);
            if (hud == null)
            {
                _status = "Render surface found (" + bestSurface.rect.width.ToString("0") + "x" +
                    bestSurface.rect.height.ToString("0") + "), but no safe compact HUD parent. " +
                    "Native minimap left untouched; use INSPECT to log MapBG/MAP PAR bounds.";
                _path = "Unbound (render surface only)";
                return;
            }
            _native = hud;
            _originalPosition = hud.anchoredPosition;
            _originalScale = hud.localScale;
            _appliedMove = false;
            _appliedScale = false;
            _path = Path(hud);
            FindNativeClip(bestSurface, hud);
            _status = "Bound to HUD: " + hud.name + "; " + _shapeStatus +
                "; 800x800 render child preserved.";
        }
        /// <summary>
        /// Only an existing compact *ancestor* Mask+Image of the native map
        /// qualifies. A circle-shaped overlay cannot reveal square map pixels.
        /// Changing the native mask sprite can, provided the source texture
        /// really has pixels outside the previous circle.
        /// </summary>
        private void FindNativeClip(RectTransform surface, RectTransform hud)
        {
            _viewport = null; _nativeMask = null; _maskImage = null;
            _shapeStatus = "No compatible native stencil mask (shape changes unavailable)";
            int maskCount = 0;
            RectTransform candidate = null;
            for (Transform t = surface.parent; t != null && (t == hud || t.IsChildOf(hud)); t = t.parent)
            {
                RectTransform r = t as RectTransform;
                if (r == null) continue;
                Mask m = r.GetComponent<Mask>();
                if (m == null || !m.enabled) continue;
                ++maskCount;
                Image im = r.GetComponent<Image>();
                // Never change a source RawImage or a full HUD background.
                if (im != null && im.enabled && SmallHudBounds(r) && candidate == null)
                {
                    candidate = r;
                    _nativeMask = m;
                    _maskImage = im;
                }
            }
            if (maskCount != 1 || candidate == null)
            {
                _nativeMask = null;
                _maskImage = null;
                _shapeStatus = maskCount == 0 ?
                    "No native Mask+Image found; true shape ownership disabled" :
                    "Multiple/incompatible map masks; shape ownership disabled (" + maskCount + ")";
                return;
            }
            _viewport = candidate;
            _originalMaskEnabled = _nativeMask.enabled;
            _originalMaskImageEnabled = _maskImage.enabled;
            _shapeStatus = "Native viewport " + Path(candidate) + " " +
                ScreenRect(candidate).width.ToString("0") + "x" +
                ScreenRect(candidate).height.ToString("0") + " (stencil geometry override available; native image preserved)";
        }

        private void RestoreNativeClip(bool destroyOwnedRectMask = false)
        {
            // Restore Erenshor's original clipping source when leaving Square
            // mode. The 800x800 map RawImage is never changed or reparented.
            if (_squareClipActive)
            {
                if (_squareRectClip != null) _squareRectClip.enabled = false;
                if (_maskImage != null) _maskImage.enabled = _originalMaskImageEnabled;
                if (_nativeMask != null) _nativeMask.enabled = _originalMaskEnabled;
                _squareClipActive = false;
            }
            if (_squareRectClip != null) _squareRectClip.enabled = false;
            if (destroyOwnedRectMask && _ownsSquareRectClip && _squareRectClip != null)
                UnityEngine.Object.Destroy(_squareRectClip);
            if (destroyOwnedRectMask)
            {
                _squareRectClip = null;
                _ownsSquareRectClip = false;
            }
            if (_maskGeometry != null)
            {
                UnityEngine.Object.Destroy(_maskGeometry);
                if (_maskImage != null) _maskImage.SetVerticesDirty();
            }
            _maskGeometry = null;
            _ownsShape = false;
            _appliedShape = null;
        }

        private void ApplyNativeClip()
        {
            if (!MinimapSettings.Enabled || _viewport == null || _maskImage == null || _nativeMask == null ||
                _surface == null || !_viewport.gameObject.activeInHierarchy || !_surface.IsChildOf(_viewport))
            {
                RestoreNativeClip();
                return;
            }

            string desired = MinimapSettings.Style;
            // The real native map path is 250x250 Mask+Image(MapMask) holding an
            // 800x800 RawImage. A BaseMeshEffect cannot remove the visible old
            // circular MapMask sprite. RectMask2D clips children geometrically
            // to the actual square viewport; switch off that old stencil and
            // its original circle artwork *only while Square is active*.
            if (desired == "Square")
            {
                if (!_squareClipActive)
                {
                    RestoreNativeClip();
                    if (_squareRectClip == null)
                    {
                        _squareRectClip = _viewport.GetComponent<RectMask2D>();
                        if (_squareRectClip == null)
                        {
                            _squareRectClip = _viewport.gameObject.AddComponent<RectMask2D>();
                            _ownsSquareRectClip = true;
                        }
                    }
                    if (_squareRectClip == null)
                    {
                        _shapeStatus = "Square clipping unavailable: RectMask2D failed to attach";
                        return;
                    }
                    // Disable native round Mask first. The rectangular mask
                    // covers the same viewport and has no child-UI controls.
                    _nativeMask.enabled = false;
                    _maskImage.enabled = false;
                    _squareRectClip.enabled = true;
                    _squareClipActive = true;
                    _shapeStatus = "Square RectMask2D active on " + Path(_viewport) +
                        "; original MapMask disabled; RawImage untouched";
                }
                _ownsShape = true;
                _appliedShape = "Square (RectMask2D)";
                return;
            }

            if (_squareClipActive)
                RestoreNativeClip();

            // Round/Ornate/Minimal use the native round mask unchanged.
            if (desired == "Round" || desired == "Ornate" || desired == "Minimal")
            {
                if (_maskGeometry != null) RestoreNativeClip();
                _ownsShape = true;
                _appliedShape = "Native Round";
                return;
            }

            // Rounded continues to use the guarded native stencil mesh path.
            // Its success depends on the Erenshor mask texture; keep its
            // distinction visible in diagnostics until tested in-game.
            if (_maskGeometry == null)
            {
                _maskGeometry = _maskImage.gameObject.AddComponent<MinimapMaskGeometry>();
                _maskImage.SetVerticesDirty();
            }
            _maskGeometry.SetShape(desired);
            _ownsShape = _maskGeometry != null && _maskGeometry.isActiveAndEnabled;
            _appliedShape = _ownsShape ? desired + " (stencil; unverified)" : null;
        }

        private void AlignFrameToViewport()
        {
            if (_frame == null || _native == null || _viewport == null) return;
            Vector3[] corners = new Vector3[4];
            _viewport.GetWorldCorners(corners);
            Vector3 bottomLeft = _native.InverseTransformPoint(corners[0]);
            Vector3 topRight = _native.InverseTransformPoint(corners[2]);
            _frame.anchorMin = _frame.anchorMax = new Vector2(.5f, .5f);
            _frame.pivot = new Vector2(.5f, .5f);
            _frame.localScale = Vector3.one;
            _frame.localRotation = Quaternion.identity;
            _frame.sizeDelta = new Vector2(Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));
            _frame.anchoredPosition = new Vector2((bottomLeft.x + topRight.x) * .5f - _native.rect.center.x,
                (bottomLeft.y + topRight.y) * .5f - _native.rect.center.y);
        }

        internal void Inspect()
        {
            EnsureBinding();
            var all = Resources.FindObjectsOfTypeAll<RectTransform>();
            int count = 0;
            for (int i = 0; i < all.Length && count < 30; ++i)
            {
                RectTransform r = all[i];
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                if (!NameMatches(r.name) && !r.name.ToLowerInvariant().Contains("compass")) continue;
                ++count;
                string path = Path(r);
                Rect b = ScreenRect(r);
                Plugin.LogInfo("[ArcaneUI Minimap] " + path + " local=" + r.rect.size +
                    " screen=" + b.width.ToString("0") + "x" + b.height.ToString("0") +
                    " pos=" + r.anchoredPosition + " scale=" + r.localScale +
                    " active=" + r.gameObject.activeInHierarchy + " score=" + Score(r));
            }
            if (_surface != null)
            {
                Plugin.LogInfo("[ArcaneUI Minimap] Native render surface ancestry (diagnostic only):");
                for (Transform t = _surface; t != null && t.name != "UI"; t = t.parent)
                {
                    RectTransform r = t as RectTransform;
                    if (r == null) continue;
                    Rect b = ScreenRect(r);
                    Plugin.LogInfo("[ArcaneUI Minimap] PARENT " + Path(r) +
                        " local=" + r.rect.size + " screen=" + b.width.ToString("0") +
                        "x" + b.height.ToString("0") + " at " + b.position +
                        " anchored=" + r.anchoredPosition + " scale=" + r.localScale +
                        " mask=" + (r.GetComponent<Mask>() != null) +
                        " rectMask=" + (r.GetComponent<RectMask2D>() != null) +
                        " image=" + (r.GetComponent<Image>() != null) +
                        " rawImage=" + (r.GetComponent<RawImage>() != null) +
                        " selected=" + (r == _native) + " viewport=" + (r == _viewport));
                }
            }
            if (_viewport != null && _maskImage != null)
            {
                Material material = _maskImage.material;
                Plugin.LogInfo("[ArcaneUI Minimap] CLIP viewport=" + Path(_viewport) +
                    " maskEnabled=" + (_nativeMask != null && _nativeMask.enabled) +
                    " showGraphic=" + (_nativeMask != null && _nativeMask.showMaskGraphic) +
                    " imageType=" + _maskImage.type +
                    " imageUseSpriteMesh=" + _maskImage.useSpriteMesh +
                    " originalSprite=" + (_maskImage.sprite != null ? _maskImage.sprite.name : "null") +
                    " shader=" + (material != null && material.shader != null ? material.shader.name : "default") +
                    " geometryOverride=" + (_maskGeometry != null) +
                    " rectMaskActive=" + (_squareRectClip != null && _squareRectClip.enabled) +
                    " squarePath=" + _squareClipActive + " requestedStyle=" + MinimapSettings.Style);
                Mask[] descendantMasks = _viewport.GetComponentsInChildren<Mask>(true);
                foreach (Mask mask in descendantMasks)
                    if (mask != null && mask != _nativeMask)
                        Plugin.LogInfo("[ArcaneUI Minimap] OTHER MASK " + Path(mask.transform) +
                            " enabled=" + mask.enabled + " showGraphic=" + mask.showMaskGraphic);
                Graphic[] graphics = _surface.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length && i < 12; ++i)
                {
                    Graphic g = graphics[i];
                    Material m = g != null ? g.material : null;
                    if (g != null)
                        Plugin.LogInfo("[ArcaneUI Minimap] MAP SOURCE " + Path(g.transform) +
                            " type=" + g.GetType().Name +
                            " shader=" + (m != null && m.shader != null ? m.shader.name : "default") +
                            (g is RawImage ? " texture=" + (((RawImage)g).texture != null ?
                                ((RawImage)g).texture.GetType().Name + " " + ((RawImage)g).texture.width +
                                "x" + ((RawImage)g).texture.height : "null") +
                                " uv=" + ((RawImage)g).uvRect : ""));
                }
            }
            _replacement.Inspect(_native);
            Plugin.LogInfo("[ArcaneUI Minimap] " + count + " candidate(s); selected=" + _path +
                " status=" + _status + " shape=" + _shapeStatus +
                " shapeApplied=" + _ownsShape + " squareRectMask=" + _squareClipActive +
                "; if Square still shows a circle, the map source has circular content outside its original mask.");
        }
        internal bool TryMove(Vector2 offset)
        {
            EnsureBinding();
            if (!CanEdit) return false;
            MinimapSettings.SetBool("MoveNative", true);
            MinimapSettings.SetFloat("OffsetX", offset.x);
            MinimapSettings.SetFloat("OffsetY", offset.y);
            Apply();
            return true;
        }
        private void RestoreVisibility()
        {
            if (_visibilityGroup != null)
                UnityEngine.Object.Destroy(_visibilityGroup);
            _visibilityGroup = null;
            _visibilityRoot = null;
        }

        private void ApplyNativeVisibility()
        {
            if (_native == null) { RestoreVisibility(); return; }
            if (MinimapSettings.ShowNative || !_replacement.Ready)
            {
                RestoreVisibility();
                return;
            }
            if (_visibilityRoot != _native || _visibilityGroup == null)
            {
                RestoreVisibility();
                // This separate group leaves any game-authored group and
                // its original alpha/interactivity completely intact.
                _visibilityGroup = _native.gameObject.AddComponent<CanvasGroup>();
                _visibilityRoot = _native;
            }
            _visibilityGroup.alpha = 0f;
            _visibilityGroup.interactable = true; // Preserve native button callbacks for ArcaneUI proxies.
            _visibilityGroup.blocksRaycasts = false;
        }

        // The viewport and native zoom/map buttons can live under different
        // descendants. Keep our frame immediately above the actual map branch
        // rather than forcing it in front of all native HUD controls.
        private void PlaceFrameBehindControls()
        {
            if (_frame == null || _viewport == null || _native == null) return;
            Transform branch = _viewport;
            while (branch.parent != null && branch.parent != _native)
                branch = branch.parent;
            if (branch.parent != _native) return;
            _frame.SetAsFirstSibling();
            _frame.SetSiblingIndex(Mathf.Min(branch.GetSiblingIndex() + 1,
                _native.childCount - 1));
        }

        private void UpdateBorderExclusions()
        {
            if (_frameGraphic == null || _frame == null || _native == null) return;
            _frameGraphic.ExcludedControlRects.Clear();
            Rect mapArea = _frame.rect;
            foreach (RectTransform control in _native.GetComponentsInChildren<RectTransform>(true))
            {
                if (control == null || !control.gameObject.activeInHierarchy ||
                    control == _frame || control.IsChildOf(_frame)) continue;
                // Erenshor builds can use regular UI Buttons, EventTriggers or
                // collider-driven widgets for their +, -, V, M and ^ controls.
                bool nativeInteraction = control.GetComponent<Button>() != null ||
                    control.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null ||
                    control.GetComponent<BoxCollider2D>() != null;
                if (!nativeInteraction) continue;
                Vector3[] corners = new Vector3[4];
                control.GetWorldCorners(corners);
                float x0 = float.MaxValue, y0 = float.MaxValue;
                float x1 = float.MinValue, y1 = float.MinValue;
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = _frame.InverseTransformPoint(corner);
                    x0 = Mathf.Min(x0, point.x); x1 = Mathf.Max(x1, point.x);
                    y0 = Mathf.Min(y0, point.y); y1 = Mathf.Max(y1, point.y);
                }
                // Prevent an unrelated remote control from cutting a border.
                Rect hit = Rect.MinMaxRect(x0 - 5f, y0 - 5f, x1 + 5f, y1 + 5f);
                // A real map viewport/collider must never erase the whole ring.
                if (hit.width > 8f && hit.height > 8f &&
                    hit.width < mapArea.width * .37f && hit.height < mapArea.height * .37f &&
                    hit.Overlaps(mapArea))
                    _frameGraphic.ExcludedControlRects.Add(hit);
            }
        }

        internal void Apply()
        {
            if (!IsUsable(_native)) return;
            // Recheck the source/root containment. A map hierarchy that changes
            // at runtime must not retain movement/scaling on the wrong object.
            if (_surface == null || !_surface.IsChildOf(_native) || !SmallHudBounds(_native) && !_appliedMove && !_appliedScale)
            {
                Release(true);
                _status = "Native HUD minimap hierarchy changed; edits safely disabled until re-scan.";
                return;
            }
            if (MinimapSettings.MoveNative)
            {
                _native.anchoredPosition = _originalPosition +
                    new Vector2(MinimapSettings.OffsetX, MinimapSettings.OffsetY);
                _appliedMove = true;
            }
            else if (_appliedMove)
            {
                _native.anchoredPosition = _originalPosition;
                _appliedMove = false;
            }
            else _originalPosition = _native.anchoredPosition;
            float factor = MinimapSettings.Scale / 100f;
            if (Mathf.Abs(factor - 1f) > .001f)
            {
                _native.localScale = new Vector3(_originalScale.x * factor,
                    _originalScale.y * factor, _originalScale.z);
                _appliedScale = true;
            }
            else if (_appliedScale)
            {
                _native.localScale = _originalScale;
                _appliedScale = false;
            }
            else _originalScale = _native.localScale;
            if (MinimapSettings.ShowCustom)
            {
                // Native gameplay still renders into the original RawImage; a separate
                // ArcaneUI HUD owns its visible mask, decorations, and control proxies.
                RestoreNativeClip();
                RawImage raw = _surface != null ? _surface.GetComponent<RawImage>() : null;
                _replacement.Refresh(_native, _viewport, raw);
            }
            else _replacement.Destroy();
            ApplyNativeVisibility();
            if (MinimapSettings.ShowCustom)
            {
                if (_frame != null) _frame.gameObject.SetActive(false);
                return;
            }
            // A frame without a verified matching viewport repeats the old
            // cyan-square problem; never decorate the outer button container.
            if (_viewport == null || !_ownsShape || !MinimapSettings.Enabled || !MinimapSettings.ShowBorder || MinimapSettings.BorderThickness < .1f)
            {
                if (_frame != null) _frame.gameObject.SetActive(false);
                return;
            }
            if (_frame == null)
            {
                GameObject go = new GameObject("ArcaneUI_Minimap_Decoration", typeof(RectTransform));
                _frame = go.GetComponent<RectTransform>();
                _frame.SetParent(_native, false);
                _frame.anchorMin = _frame.anchorMax = new Vector2(.5f, .5f);
                _frame.pivot = new Vector2(.5f, .5f);
                _frameGraphic = go.AddComponent<MinimapFrameGraphic>();
                _frameGraphic.raycastTarget = false;
            }
            _frame.gameObject.SetActive(true);
            AlignFrameToViewport();
            PlaceFrameBehindControls();
            UpdateBorderExclusions();
            _frameGraphic.Shape = MinimapSettings.Style;
            _frameGraphic.Thickness = MinimapSettings.Style == "Minimal" ? Mathf.Min(2f, MinimapSettings.BorderThickness) : MinimapSettings.BorderThickness;
            _frameGraphic.Inset = MinimapSettings.Inset;
            Color border = _themes != null ? _themes.Current.Accent : new Color(.83f, .72f, .49f);
            if (!MinimapSettings.InheritTheme) ColorUtility.TryParseHtmlString(MinimapSettings.BorderHex, out border);
            border.a = MinimapSettings.BorderOpacity / 100f;
            _frameGraphic.color = border;
            _frameGraphic.Redraw();
        }
        private void Release(bool restore)
        {
            RestoreVisibility();
            _replacement.Destroy();
            RestoreNativeClip(true);
            if (_native != null && restore)
            {
                _native.anchoredPosition = _originalPosition;
                _native.localScale = _originalScale;
            }
            if (_frame != null) UnityEngine.Object.Destroy(_frame.gameObject);
            _frame = null; _frameGraphic = null;
            _native = null;
            _surface = null;
            _viewport = null; _nativeMask = null; _maskImage = null;
            _appliedMove = false; _appliedScale = false;
            _path = "Unbound";
        }
    }
}
