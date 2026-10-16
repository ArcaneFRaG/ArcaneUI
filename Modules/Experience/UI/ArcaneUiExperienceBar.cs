using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.Experience.Configuration;

namespace ArcanesGroupFrames.Modules.Experience.UI
{
    internal sealed class ArcaneUiExperienceBar
    {
        private readonly ArcaneCanvasManager _canvas;
        private RectTransform _layer, _bar, _fill;
        private Image _background, _fillImage;
        private Text _text;
        private float _nextUpdate;
        private static string _status = "Waiting for player stats.";
        internal static string Diagnostics { get { return _status; } }
        // Layout editor uses the actual HUD rect, even when the bar is hidden by its modal.
        internal RectTransform Rect { get { return _bar; } }
        internal ArcaneUiExperienceBar(ArcaneCanvasManager canvas) { _canvas = canvas; }

        private static RectTransform Node(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
        private void Initialize()
        {
            if (_layer != null || _canvas == null || !_canvas.IsReady) return;
            _layer = _canvas.CreateLayer("ArcaneUI_XPBarLayer");
            _bar = Node(_layer,"ArcaneUI_XPBar");
            _bar.anchorMin = _bar.anchorMax = new Vector2(.5f,0f);
            _bar.pivot = new Vector2(.5f,.5f);
            _background = _bar.gameObject.AddComponent<Image>();
            _background.raycastTarget = false;
            _fill = Node(_bar,"ArcaneUI_XPFill");
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f,1f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _fillImage = _fill.gameObject.AddComponent<Image>();
            _fillImage.raycastTarget = false;
            RectTransform labelRoot = Node(_bar,"ArcaneUI_XPText");
            labelRoot.anchorMin = Vector2.zero;
            labelRoot.anchorMax = Vector2.one;
            labelRoot.offsetMin = labelRoot.offsetMax = Vector2.zero;
            _text = labelRoot.gameObject.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.raycastTarget = false;
            Outline shadow = labelRoot.gameObject.AddComponent<Outline>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1f,-1f);
            _bar.gameObject.SetActive(false);
        }
        private static Color Colour(string hex, Color fallback)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(hex, out parsed) ? parsed : fallback;
        }
        internal void ApplyStyle()
        {
            if (_bar == null) return;
            _bar.sizeDelta = new Vector2(ExperienceBarSettings.Width, ExperienceBarSettings.Height);
            _bar.anchoredPosition = new Vector2(ExperienceBarSettings.OffsetX, ExperienceBarSettings.OffsetY);
            _background.color = Colour(ExperienceBarSettings.BackgroundHex,new Color(.08f,.1f,.14f));
            _text.fontSize = ExperienceBarSettings.FontSize;
            _nextUpdate = 0;
        }
        internal void Update()
        {
            if (_layer == null) Initialize();
            if (_bar == null) return;
            Stats stats = GameData.PlayerStats;
            if (!ExperienceBarSettings.Enabled || !NativeUiLayering.CanShowGameplayUi || GameData.InCharSelect ||
                GameData.PlayerControl == null || stats == null)
            {
                if (_bar.gameObject.activeSelf) _bar.gameObject.SetActive(false);
                return;
            }
            if (Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = Time.unscaledTime + .20f;
            ExperienceProgressReader.Progress progress = ExperienceProgressReader.Read(stats);
            if (progress.Ascension && !ExperienceBarSettings.ShowAtMaxLevel)
            {
                _bar.gameObject.SetActive(false);
                return;
            }
            if (!_bar.gameObject.activeSelf) _bar.gameObject.SetActive(true);
            _bar.sizeDelta = new Vector2(ExperienceBarSettings.Width,ExperienceBarSettings.Height);
            _bar.anchoredPosition = new Vector2(ExperienceBarSettings.OffsetX,ExperienceBarSettings.OffsetY);
            _background.color = Colour(ExperienceBarSettings.BackgroundHex,Color.black);
            _text.fontSize = ExperienceBarSettings.FontSize;
            _fillImage.color = Colour(progress.Ascension ? ExperienceBarSettings.AscensionHex : ExperienceBarSettings.NormalHex,
                progress.Ascension ? new Color(.55f,.35f,.75f) : new Color(.75f,.60f,.30f));
            _fill.anchorMax = new Vector2(progress.Known ? progress.Fraction : 0f,1f);
            _text.enabled = ExperienceBarSettings.ShowText;
            if (_text.enabled)
            {
                string label = progress.Ascension ? "Ascension XP" : "XP (Level " + progress.Level + ")";
                string number = progress.Known && progress.Needed > 0 && ExperienceBarSettings.ShowNumbers
                    ? "  " + progress.Current.ToString("N0") + " / " + progress.Needed.ToString("N0") : "";
                string percent = progress.Known && ExperienceBarSettings.ShowPercent
                    ? "  (" + Mathf.RoundToInt(progress.Fraction*100f) + "%)" : "";
                _text.text = label + (progress.Known ? number + percent : "  —");
            }
            _status = (progress.Ascension ? "ASCENSION" : "NORMAL") + ": " + progress.Source +
                " (level " + progress.Level + ")";
        }
        internal void LogDiagnostics()
        {
            Plugin.LogInfo("ArcaneUI XP reader: " + _status);
            Plugin.LogInfo("ArcaneUI XP fields: " + ExperienceProgressReader.Diagnostics(GameData.PlayerStats));
        }
        internal void Shutdown()
        {
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null; _bar = null; _fill = null; _text = null;
        }
    }
}
