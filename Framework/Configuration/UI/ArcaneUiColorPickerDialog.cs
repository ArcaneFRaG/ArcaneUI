using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    // Lightweight pointer adapter used by the colour wheel and SV square.
    internal sealed class ArcaneUiColorPickerSurface : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        internal Action<Vector2> PointChanged;
        internal Action Released;

        private void Read(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            var rect = transform as RectTransform;
            if (rect == null) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, data.position, data.pressEventCamera, out local)) return;

            // ArcaneUiControls.Rect uses a top-left pivot (0, 1). Unity's
            // local point is therefore measured from that corner, whereas
            // the hue wheel / SV picker and their markers use the CENTRE
            // as their origin. Without this conversion the top/right parts
            // of the surfaces map to the wrong colours or are ignored.
            // rect.rect.center also remains correct if the UI is rescaled.
            Vector2 centeredPoint = local - rect.rect.center;
            PointChanged?.Invoke(centeredPoint);
        }

        public void OnPointerDown(PointerEventData eventData) { Read(eventData); }
        public void OnDrag(PointerEventData eventData) { Read(eventData); }
        public void OnPointerUp(PointerEventData eventData) { Released?.Invoke(); }
    }

    // Reusable colour picker. All colour channels use existing hex persistence
    // setters; this window owns no parallel settings or PlayerPrefs keys.
    internal sealed class ArcaneUiColorPickerDialog : MonoBehaviour
    {
        private const int WheelPixels = 216;
        private const int SquarePixels = 200;
        private const float WheelInnerRadius = 73f;
        private const float WheelOuterRadius = 106f;

        private string _initialHex;
        private string _lastAppliedHex;
        private Action<string> _changed;
        private Action _closed;
        private float _h, _s, _v;
        private float _lastDispatchAt = -100f;
        private bool _finalized;
        private Texture2D _wheelTexture;
        private Texture2D _squareTexture;
        private RectTransform _wheelMarker;
        private RectTransform _squareMarker;
        private RawImage _squareImage;
        private Image _currentSwatch;
        private Image _initialSwatch;
        private InputField _hexInput;
        private InputField[] _rgbInputs;
        private Text _validation;

        internal static ArcaneUiColorPickerDialog Show(Transform parent, string title, string original,
            Color background, Color surface, Color accent, Color foreground,
            Action<string> onChanged, Action onClosed)
        {
            var root = ArcaneUiControls.Panel(parent, "ArcaneUI_ColorPickerModal", 0, 0, 0, 0,
                new Color(0f, 0f, 0f, .70f));
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var picker = root.gameObject.AddComponent<ArcaneUiColorPickerDialog>();
            picker.Setup(title, original, background, surface, accent, foreground, onChanged, onClosed);
            root.SetAsLastSibling();
            return picker;
        }

        private void Setup(string title, string original, Color background, Color surface,
            Color accent, Color foreground, Action<string> onChanged, Action onClosed)
        {
            _changed = onChanged;
            _closed = onClosed;
            Color parsed;
            if (!TryHex(original, out parsed)) parsed = Color.white;
            _initialHex = ToHex(parsed);
            _lastAppliedHex = _initialHex;
            Color.RGBToHSV(parsed, out _h, out _s, out _v);

            var frame = ArcaneUiControls.Panel(transform, "PickerWindow", 0, 0, 560, 527, background);
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            ArcaneUiControls.Panel(frame, "Header", 0, 0, 560, 46, surface);
            ArcaneUiControls.Panel(frame, "Accent", 0, 44, 560, 2, accent, false);
            ArcaneUiControls.Label(frame, "Title", "EDIT COLOUR  /  " + title, 18, 5, 470, 33,
                15, foreground, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Button(frame, "Close", "X", 507, 8, 34, 30, surface,
                foreground, Cancel, 13);

            _wheelTexture = BuildWheel();
            var wheel = AddRaw(frame, "HueWheel", 24, 65, 216, 216, _wheelTexture);
            var wheelDrag = wheel.gameObject.AddComponent<ArcaneUiColorPickerSurface>();
            wheelDrag.PointChanged = PickHue;
            wheelDrag.Released = () => Dispatch(true);
            _wheelMarker = CreateMarker(wheel.transform, "HueMarker", 12, 12);

            _squareTexture = new Texture2D(SquarePixels, SquarePixels, TextureFormat.RGBA32, false);
            _squareTexture.wrapMode = TextureWrapMode.Clamp;
            _squareTexture.filterMode = FilterMode.Bilinear;
            var square = AddRaw(frame, "SaturationBrightness", 294, 71, 200, 200, _squareTexture);
            square.gameObject.AddComponent<Outline>().effectColor = new Color(.7f, .7f, .7f, .75f);
            _squareImage = square;
            var squareDrag = square.gameObject.AddComponent<ArcaneUiColorPickerSurface>();
            squareDrag.PointChanged = PickSaturationAndValue;
            squareDrag.Released = () => Dispatch(true);
            _squareMarker = CreateMarker(square.transform, "SVMarker", 11, 11);
            ArcaneUiControls.Label(frame, "WheelHint", "HUE", 93, 279, 88, 19,
                11, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            ArcaneUiControls.Label(frame, "SquareHint", "SATURATION / BRIGHTNESS", 288, 279, 220, 19,
                11, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);

            ArcaneUiControls.Label(frame, "OldLabel", "ORIGINAL", 23, 307, 95, 23, 11, foreground);
            ArcaneUiControls.Label(frame, "NewLabel", "SELECTED", 280, 307, 93, 23, 11, foreground);
            _initialSwatch = ArcaneUiControls.Panel(frame, "OriginalColour", 118, 310, 126, 26, parsed, false)
                .GetComponent<Image>();
            _currentSwatch = ArcaneUiControls.Panel(frame, "NewColour", 375, 310, 130, 26, parsed, false)
                .GetComponent<Image>();

            _rgbInputs = new InputField[3];
            const string channels = "RGB";
            for (int i = 0; i < 3; i++)
            {
                int channel = i;
                float x = 25 + i * 174f;
                ArcaneUiControls.Label(frame, "ChannelLabel_" + channels[i], channels[i].ToString(),
                    x, 356, 25, 35, 14, foreground, TextAnchor.MiddleLeft, FontStyle.Bold);
                var input = ArcaneUiControls.Search(frame, "Channel_" + channels[i], x + 31, 357, 110,
                    32, "0–255", surface, foreground, null);
                input.contentType = InputField.ContentType.IntegerNumber;
                input.characterLimit = 3;
                input.onEndEdit.AddListener(value => ApplyRgb(channel, value));
                _rgbInputs[i] = input;
            }
            ArcaneUiControls.Label(frame, "HexLabel", "HEX", 25, 404, 70, 31, 13,
                foreground, TextAnchor.MiddleLeft, FontStyle.Bold);
            _hexInput = ArcaneUiControls.Search(frame, "HexValue", 87, 403, 228, 35,
                "#RRGGBB", surface, foreground, null);
            _hexInput.characterLimit = 7;
            _hexInput.onEndEdit.AddListener(ApplyHex);
            _validation = ArcaneUiControls.Label(frame, "Validation", "#RRGGBB or RRGGBB",
                327, 404, 205, 33, 11, foreground);

            ArcaneUiControls.Panel(frame, "Divider", 0, 463, 560, 1,
                new Color(foreground.r, foreground.g, foreground.b, .18f), false);
            ArcaneUiControls.Button(frame, "Cancel", "CANCEL", 291, 476, 116, 35,
                surface, foreground, Cancel, 12);
            ArcaneUiControls.Button(frame, "Apply", "APPLY", 420, 476, 113, 35,
                accent, Color.white, Apply, 12);
            UpdateSquareTexture();
            RefreshVisuals();
        }

        private static RawImage AddRaw(Transform parent, string name, float x, float y,
            float width, float height, Texture texture)
        {
            var rt = ArcaneUiControls.Rect(parent, name, x, y, width, height);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = true;
            return raw;
        }

        private static RectTransform CreateMarker(Transform parent, string name, float width, float height)
        {
            var rect = ArcaneUiControls.Panel(parent, name, 0, 0, width, height, Color.white, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.gameObject.AddComponent<Outline>().effectColor = Color.black;
            rect.GetComponent<Image>().raycastTarget = false;
            return rect;
        }

        private static Texture2D BuildWheel()
        {
            var texture = new Texture2D(WheelPixels, WheelPixels, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[WheelPixels * WheelPixels];
            float center = (WheelPixels - 1) * .5f;
            for (int y = 0; y < WheelPixels; y++)
                for (int x = 0; x < WheelPixels; x++)
                {
                    float dx = x - center, dy = y - center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - (WheelInnerRadius - 1f)) *
                                  Mathf.Clamp01((WheelOuterRadius + 1f) - radius);
                    float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI), 1f);
                    Color color = Color.HSVToRGB(hue, 1f, 1f);
                    color.a = alpha;
                    pixels[y * WheelPixels + x] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void UpdateSquareTexture()
        {
            if (_squareTexture == null) return;
            var pixels = new Color32[SquarePixels * SquarePixels];
            for (int y = 0; y < SquarePixels; y++)
                for (int x = 0; x < SquarePixels; x++)
                    pixels[y * SquarePixels + x] =
                        Color.HSVToRGB(_h, x / (float)(SquarePixels - 1), y / (float)(SquarePixels - 1));
            _squareTexture.SetPixels32(pixels);
            _squareTexture.Apply(false, false);
        }

        private void PickHue(Vector2 point)
        {
            float radius = point.magnitude;
            if (radius < WheelInnerRadius - 7f || radius > WheelOuterRadius + 7f) return;
            _h = Mathf.Repeat(Mathf.Atan2(point.y, point.x) / (2f * Mathf.PI), 1f);
            UpdateSquareTexture();
            ColourChanged();
        }

        private void PickSaturationAndValue(Vector2 point)
        {
            _s = Mathf.Clamp01(point.x / SquarePixels + .5f);
            _v = Mathf.Clamp01(point.y / SquarePixels + .5f);
            ColourChanged();
        }

        private void ApplyHex(string input)
        {
            Color result;
            if (!TryHex(input, out result))
            {
                _validation.text = "Invalid HEX (use #RRGGBB)";
                _validation.color = new Color(1f, .46f, .40f);
                return;
            }
            Color.RGBToHSV(result, out _h, out _s, out _v);
            UpdateSquareTexture();
            ColourChanged(true);
        }

        private void ApplyRgb(int channel, string input)
        {
            int value;
            if (!int.TryParse(input, out value) || value < 0 || value > 255)
            {
                _validation.text = "RGB must be 0–255";
                _validation.color = new Color(1f, .46f, .40f);
                return;
            }
            Color color = Current;
            float v = value / 255f;
            if (channel == 0) color.r = v;
            else if (channel == 1) color.g = v;
            else color.b = v;
            Color.RGBToHSV(color, out _h, out _s, out _v);
            UpdateSquareTexture();
            ColourChanged(true);
        }

        private Color Current { get { return Color.HSVToRGB(_h, _s, _v); } }
        private static string ToHex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
        private static bool TryHex(string raw, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string clean = raw.Trim().TrimStart('#');
            if (clean.Length != 6) return false;
            return ColorUtility.TryParseHtmlString("#" + clean, out color);
        }

        private void ColourChanged(bool final = false)
        {
            RefreshVisuals();
            Dispatch(final);
        }

        private void RefreshVisuals()
        {
            Color current = Current;
            if (_currentSwatch != null) _currentSwatch.color = current;
            if (_wheelMarker != null)
            {
                float angle = _h * 2f * Mathf.PI;
                _wheelMarker.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 89.5f;
            }
            if (_squareMarker != null)
                _squareMarker.anchoredPosition = new Vector2((_s - .5f) * SquarePixels,
                    (_v - .5f) * SquarePixels);
            if (_hexInput != null && !_hexInput.isFocused)
                _hexInput.SetTextWithoutNotify(ToHex(current));
            if (_rgbInputs != null)
            {
                int[] rgb = { Mathf.RoundToInt(current.r * 255f),
                              Mathf.RoundToInt(current.g * 255f),
                              Mathf.RoundToInt(current.b * 255f) };
                for (int i = 0; i < 3; i++)
                    if (_rgbInputs[i] != null && !_rgbInputs[i].isFocused)
                        _rgbInputs[i].SetTextWithoutNotify(rgb[i].ToString());
            }
            if (_validation != null)
            {
                _validation.text = "#RRGGBB or RRGGBB";
                _validation.color = new Color(.8f, .8f, .8f, 1f);
            }
        }

        private void Dispatch(bool force)
        {
            if (_finalized || _changed == null) return;
            string hex = ToHex(Current);
            if (string.Equals(_lastAppliedHex, hex, StringComparison.OrdinalIgnoreCase)) return;
            if (!force && Time.unscaledTime - _lastDispatchAt < .08f) return;
            _lastAppliedHex = hex;
            _lastDispatchAt = Time.unscaledTime;
            _changed(hex);
        }

        private void Apply()
        {
            if (_finalized) return;
            Dispatch(true);
            Finish();
        }

        internal void Cancel()
        {
            if (_finalized) return;
            if (!string.Equals(_initialHex, _lastAppliedHex, StringComparison.OrdinalIgnoreCase))
                _changed?.Invoke(_initialHex);
            Finish();
        }

        private void Finish()
        {
            _finalized = true;
            _closed?.Invoke();
            if (this != null) gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_wheelTexture != null) Destroy(_wheelTexture);
            if (_squareTexture != null) Destroy(_squareTexture);
        }
    }
}
