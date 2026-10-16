using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Native
{
    // Presentation-only web. Every upgrade is dispatched to the existing
    // Erenshor Skill/Add button; ArcaneUI never writes points or ranks.
    internal sealed partial class CharacterSheetBoundedView
    {
        private sealed class AscensionLine
        {
            internal string Name;
            internal int Rank;
            internal Button NativeUpgrade;
            internal Button NativeSelect;
            internal string Description;
        }
        private readonly List<AscensionLine> _ascensionLines = new List<AscensionLine>();
        private readonly Dictionary<string, string> _nativeAscensionDescriptions = new Dictionary<string, string>();
        private readonly HashSet<string> _missingAscensionEffectLogged = new HashSet<string>();
        private readonly Dictionary<int, Image> _ascensionNodeSelectionRings = new Dictionary<int, Image>();
        private readonly Dictionary<int, Text> _ascensionLineLabels = new Dictionary<int, Text>();
        private static Sprite _ascensionDiscSprite;
        private static Sprite _ascensionRingSprite;
        private RectTransform _ascensionCustom;
        private RectTransform _ascensionWeb;
        private Text _ascensionPoints;
        private Text _ascensionDescription;
        private Text _ascensionSelected;
        private Text _ascensionActionLabel;
        private Text _ascensionNodeStatus;
        private Button _ascensionUpgrade;
        private Button _ascensionRespec;
        private Button _nativeRespec;
        private Button[] _ascensionSwitch = new Button[2];
        private RectTransform[] _ascensionSwitchBars = new RectTransform[2];
        private Button[] _nativeAscensionSwitch = new Button[2];
        private int _ascensionMode;
        private int _selectedAscensionLine = -1;
        private int _selectedAscensionTier;
        private string _ascensionSignature = "";
        private float _nextAscensionRead;
        private bool _ascensionMapped;
        private bool _ascensionWarned;
        private bool _ascensionLogged;
        private int _lastAscensionRowCount = -1;
        private int _lastAscensionNamedCount = -1;
        private bool _ascensionNativeView;
        private RectTransform _ascensionViewToggle;
        private Text _ascensionViewToggleText;
        // A native skill row may first need selecting before its Add button
        // becomes enabled. Keep that as a two-stage native-only transaction;
        // never mutate point totals or rank data from ArcaneUI.
        private string _pendingAscensionName;
        private int _pendingAscensionRank = -1;
        private int _pendingAscensionMode = -1;
        private float _pendingAscensionUntil;
        private string _verifyAscensionName;
        private int _verifyAscensionRank;
        private float _verifyAscensionAt;

        private static RectTransform CenteredRect(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }
        private static Button StyledButton(RectTransform rect, Color fill)
        {
            Image image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = fill;
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }
        private static void LineBetween(Transform parent, string name, Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 delta = end - start;
            RectTransform canvas = parent as RectTransform;
            if (canvas == null) return;
            // Anchors are normalized, but sizeDelta and rotation need pixels.
            Vector2 pixels = new Vector2(delta.x * canvas.rect.width,
                delta.y * canvas.rect.height);
            RectTransform rect = CenteredRect(parent, name, (start + end) * .5f,
                new Vector2(pixels.magnitude, width));
            rect.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Atan2(pixels.y, pixels.x) * Mathf.Rad2Deg);
            Image img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
        private static string TextOf(Transform root)
        {
            if (root == null) return "";
            TMP_Text tmp = root.GetComponent<TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return Clean(tmp.text);
            Text label = root.GetComponent<Text>();
            if (label != null && !string.IsNullOrEmpty(label.text)) return Clean(label.text);
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && !string.IsNullOrEmpty(Clean(t.text))) return Clean(t.text);
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t != null && !string.IsNullOrEmpty(Clean(t.text))) return Clean(t.text);
            return "";
        }
        private static string AllText(Transform root)
        {
            if (root == null) return "";
            StringBuilder builder = new StringBuilder();
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && !string.IsNullOrEmpty(t.text)) builder.Append(Clean(t.text)).Append('\n');
            foreach (Text t in root.GetComponentsInChildren<Text>(true))
                if (t != null && !string.IsNullOrEmpty(t.text)) builder.Append(Clean(t.text)).Append('\n');
            return builder.ToString();
        }
        // Short, word-wrapped labels remain readable on the compact web. The
        // detail panel always shows the unabridged native Ascension name.
        private static string WebSkillName(string skill)
        {
            if (string.IsNullOrEmpty(skill)) return "?";
            skill = skill.Trim();
            if (skill.StartsWith("Innate ", StringComparison.OrdinalIgnoreCase))
                skill = skill.Substring(7);
            if (skill.Length > 29) skill = skill.Substring(0, 28).TrimEnd() + "…";
            if (skill.Length <= 13) return skill;
            int split = skill.LastIndexOf(' ', Mathf.Min(14, skill.Length - 1));
            if (split < 5) split = skill.IndexOf(' ', Mathf.Min(10, skill.Length - 1));
            return split > 0 ? skill.Substring(0, split) + "\n" + skill.Substring(split + 1) : skill;
        }

        // The old normalized-coordinate clamp pushed left/right labels back
        // onto the outermost rank circles. Position labels in PIXELS and reject
        // candidates intersecting any node halo or a previously placed label.
        // Their final positions are converted to anchors after collision tests.
        private static float AscensionLabelOverlap(Vector2 a, Vector2 b,
            float aHalfWidth, float aHalfHeight, float bHalfWidth, float bHalfHeight)
        {
            float dx = Mathf.Max(0f, aHalfWidth + bHalfWidth - Mathf.Abs(a.x - b.x));
            float dy = Mathf.Max(0f, aHalfHeight + bHalfHeight - Mathf.Abs(a.y - b.y));
            return dx * dy;
        }
        private static Vector2 FindAscensionLabelAnchor(Vector2 direction,
            Vector2[,] nodes, int total, List<Vector2> placed,
            Vector2 canvasSize, Vector2 labelSize, out bool fits)
        {
            Vector2 center = canvasSize * .5f;
            Vector2 basePoint = center + new Vector2(direction.x * canvasSize.x * .439f,
                direction.y * canvasSize.y * .439f);
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            float halfW = labelSize.x * .5f;
            float halfH = labelSize.y * .5f;
            float[] slides = { 0f, 28f, -28f, 50f, -50f, 74f, -74f,
                100f, -100f, 125f, -125f, 145f, -145f };
            float[] radialMoves = { 0f, 18f, -18f, 35f, -35f, 55f };
            Vector2 best = basePoint;
            float bestScore = float.MaxValue;
            float bestCollision = float.MaxValue;
            foreach (float slide in slides)
                foreach (float radial in radialMoves)
                {
                    Vector2 candidate = basePoint + tangent * slide + direction * radial;
                    candidate.x = Mathf.Clamp(candidate.x, halfW + 6f,
                        canvasSize.x - halfW - 6f);
                    candidate.y = Mathf.Clamp(candidate.y, halfH + 6f,
                        canvasSize.y - halfH - 6f);
                    float nodeOverlap = 0f;
                    for (int i = 0; i < total; i++)
                        for (int tier = 0; tier < 3; tier++)
                        {
                            Vector2 node = new Vector2(nodes[i, tier].x * canvasSize.x,
                                nodes[i, tier].y * canvasSize.y);
                            nodeOverlap += AscensionLabelOverlap(candidate, node,
                                halfW + 4f, halfH + 4f, CharacterSheetSettings.AscensionNodeSize * .5f + 7f, CharacterSheetSettings.AscensionNodeSize * .5f + 7f);
                        }
                    float otherLabelOverlap = 0f;
                    foreach (Vector2 other in placed)
                        otherLabelOverlap += AscensionLabelOverlap(candidate, other,
                            halfW + 4f, halfH + 4f, halfW + 4f, halfH + 4f);
                    float collision = nodeOverlap + otherLabelOverlap;
                    float score = nodeOverlap * 40f + otherLabelOverlap * 150f +
                        Mathf.Abs(slide) * .2f + Mathf.Abs(radial) * .12f +
                        Vector2.Distance(candidate, basePoint) * .035f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    bestCollision = collision;
                    best = candidate;
                }
            // If a mod adds more skill lines than this compact surface can
            // display without collisions, omit just the unsafe spoke label.
            // The clickable nodes and full-name detail panel stay available.
            fits = bestCollision <= .01f;
            if (fits) placed.Add(best);
            return new Vector2(best.x / canvasSize.x, best.y / canvasSize.y);
        }
        private static bool IsSkillName(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 48) return false;
            string lower = text.ToLowerInvariant().Trim();
            if (lower == "add" || lower == "+" || lower == "skill" ||
                lower == "button" || lower == "text" || lower == "label" ||
                lower == "name" || lower == "rank" || lower == "learn" ||
                lower.Contains("points") || lower == "general" || lower == "class" ||
                lower.Contains("respec") || lower == "ascension" ||
                Regex.IsMatch(lower, @"^\d+\s*/\s*\d+$")) return false;
            return Regex.IsMatch(lower, "[a-z]");
        }
        private Button FindNativeAscensionSwitch(string label)
        {
            if (_nativeAscension == null) return null;
            foreach (Button button in _nativeAscension.GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.name.Equals("Add", StringComparison.OrdinalIgnoreCase)) continue;
                string text = TextOf(button.transform).Trim();
                if (text.Equals(label, StringComparison.OrdinalIgnoreCase) ||
                    button.name.Equals(label, StringComparison.OrdinalIgnoreCase)) return button;
            }
            return null;
        }
        // Runtime-drawn, antialiased sprites avoid the solid square Unity UI
        // buttons used by the original prototype. Both mini webs share them.
        private static Sprite AscensionCircle(bool ring)
        {
            Sprite existing = ring ? _ascensionRingSprite : _ascensionDiscSprite;
            if (existing != null) return existing;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f;
                    float dy = y + .5f - size * .5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01((size * .5f - 1f - distance) * 1.3f);
                    float inner = ring ? Mathf.Clamp01((distance - (size * .5f - 4.5f)) * 1.3f) : 1f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, outer * inner);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f));
            if (ring) _ascensionRingSprite = sprite;
            else _ascensionDiscSprite = sprite;
            return sprite;
        }
        private static Image RoundGraphic(RectTransform host, Color tint, bool ring)
        {
            Image image = host.GetComponent<Image>();
            if (image == null) image = host.gameObject.AddComponent<Image>();
            image.sprite = AscensionCircle(ring);
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }
        private void UpdateAscensionNodeSelection()
        {
            SheetPalette palette = Palette();
            foreach (KeyValuePair<int, Image> pair in _ascensionNodeSelectionRings)
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(pair.Key == _selectedAscensionLine * 3 + _selectedAscensionTier);
            foreach (KeyValuePair<int, Text> pair in _ascensionLineLabels)
                if (pair.Value != null)
                    pair.Value.color = pair.Key == _selectedAscensionLine ? palette.Accent : palette.Body;
        }
        private static bool IsNativeEffectDescription(string text, string skillName)
        {
            if (string.IsNullOrEmpty(text)) return false;
            text = Clean(text).Trim();
            if (text.Length < 12 || text.Length > 500 || text.Equals(skillName, StringComparison.OrdinalIgnoreCase)) return false;
            string lower = text.ToLowerInvariant();
            if (lower == "general" || lower == "class" || lower.StartsWith("ascension points") ||
                lower.StartsWith("points spent") || lower.StartsWith("skill:") ||
                Regex.IsMatch(lower, @"^\d+\s*/\s*3\s*$")) return false;
            // Reject titles, counters and UI prompt fragments; descriptions
            // contain ordinary wording rather than a standalone skill name.
            return text.IndexOf(' ') >= 0 && (text.Length >= 26 ||
                lower.Contains("increase") || lower.Contains("gain") || lower.Contains("reduce") ||
                lower.Contains("damage") || lower.Contains("healing") || lower.Contains("chance"));
        }
        private static string DescriptionFromUi(Transform row, string skillName)
        {
            StringBuilder description = new StringBuilder();
            string[] lines = AllText(row).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in lines)
            {
                string candidate = Clean(raw).Trim();
                if (!IsNativeEffectDescription(candidate, skillName)) continue;
                if (description.Length > 0) description.Append(" ");
                description.Append(candidate);
                if (description.Length > 360) break;
            }
            return description.ToString().Trim();
        }
        private static string DescriptionFromData(object source, string skillName)
        {
            if (source == null) return "";
            Type type = source.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                foreach (FieldInfo field in type.GetFields(flags))
                {
                    if (field.FieldType != typeof(string) || !IsDescriptionMember(field.Name)) continue;
                    string value = field.GetValue(source) as string;
                    if (IsNativeEffectDescription(value, skillName)) return Clean(value).Trim();
                }
                foreach (PropertyInfo property in type.GetProperties(flags))
                {
                    if (property.PropertyType != typeof(string) || !property.CanRead ||
                        property.GetIndexParameters().Length != 0 || !IsDescriptionMember(property.Name)) continue;
                    try
                    {
                        string value = property.GetValue(source, null) as string;
                        if (IsNativeEffectDescription(value, skillName)) return Clean(value).Trim();
                    }
                    catch { /* Native getters may require an active UI context. */ }
                }
            }
            catch { /* Do not let game-version-specific metadata break the web. */ }
            return "";
        }
        private static bool IsDescriptionMember(string member)
        {
            string lower = member.ToLowerInvariant();
            return lower.Contains("desc") || lower.Contains("tooltip") ||
                lower.Contains("effect") || lower.Contains("summary") ||
                lower.Contains("explanation") || lower == "info" || lower == "details";
        }
        private string NativeSkillDescription(Transform row, string name)
        {
            // Native AAScreen can reuse the same row for two modes. Cache
            // against BOTH the tree and skill name, never row identity alone.
            string key = _ascensionMode + "|" + row.GetInstanceID() + "|" + name;
            string cached;
            if (_nativeAscensionDescriptions.TryGetValue(key, out cached)) return cached;
            // First prefer native on-screen text, including hidden effect labels.
            string native = DescriptionFromUi(row, name);
            if (!string.IsNullOrEmpty(native)) { _nativeAscensionDescriptions[key] = native; return native; }
            // Some builds store explanatory text in the backing ascension
            // component rather than its label. Read strings, not stat values.
            foreach (Component component in row.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Text || component is TMP_Text) continue;
                native = DescriptionFromData(component, name);
                if (!string.IsNullOrEmpty(native)) break;
                // Inspect only explicitly named skill/ascension data links;
                // never enumerate arbitrary game objects or execute actions.
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                try
                {
                    foreach (FieldInfo field in component.GetType().GetFields(flags))
                    {
                        string member = field.Name.ToLowerInvariant();
                        if (!member.Contains("skill") && !member.Contains("ascension")) continue;
                        if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string) ||
                            typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;
                        native = DescriptionFromData(field.GetValue(component), name);
                        if (!string.IsNullOrEmpty(native)) break;
                    }
                }
                catch { }
                if (!string.IsNullOrEmpty(native)) break;
            }
            _nativeAscensionDescriptions[key] = native ?? "";
            if (string.IsNullOrEmpty(native) && _missingAscensionEffectLogged.Add(_ascensionMode + "|" + name))
                Plugin.LogInfo("ArcaneUI Ascension description unavailable from native skill row: " +
                    (_ascensionMode == 0 ? "General / " : "Class / ") + name +
                    ". Native tooltip/effect text may be stored outside AAScreen.");
            return native ?? "";
        }
        private void CreateAscensionWeb(RectTransform parent, SheetPalette palette)
        {
            _ascensionCustom = Rect(parent, "ArcaneUI_AscensionWebPage", Vector2.zero,
                Vector2.one, Vector2.zero, Vector2.zero);
            // Distinct header, inset trims and side accents match the selected
            // Character theme without changing the underlying native window.
            RectTransform heading = Box(_ascensionCustom, "Title", new Vector2(.02f,.925f),
                new Vector2(.98f,.995f), palette.Header, Vector2.zero, Vector2.zero);
            CreateText(heading, "TitleText", "A S C E N S I O N  ·  WEB", 17,
                TextAnchor.MiddleCenter, palette.Accent);
            Box(heading,"TitleUnderline",new Vector2(.17f,0f),new Vector2(.83f,.035f),
                palette.Trim,Vector2.zero,Vector2.zero);
            RectTransform switchRow = Rect(_ascensionCustom, "WebSelector", new Vector2(.045f,.862f),
                new Vector2(.955f,.922f), Vector2.zero, Vector2.zero);
            string[] names = { "GENERAL", "CLASS" };
            for (int i = 0; i < 2; i++)
            {
                int modeIndex = i;
                RectTransform toggle = Box(switchRow, "Tree_" + names[i],
                    new Vector2(i * .5f + .008f, .035f), new Vector2((i+1)*.5f-.008f,.965f),
                    palette.Tab, Vector2.zero, Vector2.zero);
                _ascensionSwitch[i] = StyledButton(toggle, palette.Tab);
                _ascensionSwitch[i].onClick.AddListener(() => SetAscensionMode(modeIndex));
                CreateText(toggle, "Text", names[i] + "  ✦  WEB", 12, TextAnchor.MiddleCenter, palette.Body);
                _ascensionSwitchBars[i] = Box(toggle, "SelectionTrim", Vector2.zero,
                    new Vector2(1f,.055f), palette.Accent, Vector2.zero, Vector2.zero);
            }
            RectTransform pointsBand = Box(_ascensionCustom, "PointsBand",
                new Vector2(.04f,.800f),new Vector2(.96f,.858f),palette.Section,
                Vector2.zero,Vector2.zero);
            Box(pointsBand,"PointsAccent",Vector2.zero,new Vector2(.012f,1f),
                palette.Accent,Vector2.zero,Vector2.zero);
            _ascensionPoints = CreateText(pointsBand, "NativePointsReadback", "AVAILABLE  —   •   SPENT  —",
                13, TextAnchor.MiddleCenter, palette.Accent);
            _ascensionWeb = Rect(_ascensionCustom, "RadialWeb",
                new Vector2(.04f,.268f), new Vector2(.96f,.790f),
                Vector2.zero,Vector2.zero);
            Box(_ascensionWeb, "WebBack", Vector2.zero, Vector2.one,
                palette.RowB, Vector2.zero, Vector2.zero);
            Box(_ascensionWeb,"TopTrim",new Vector2(.03f,.988f),new Vector2(.97f,.993f),
                palette.Trim,Vector2.zero,Vector2.zero);
            Box(_ascensionWeb,"BottomTrim",new Vector2(.03f,.007f),new Vector2(.97f,.012f),
                palette.Trim,Vector2.zero,Vector2.zero);
            // Detail uses over a quarter of the page, ensuring multi-line
            // gameplay descriptions remain readable inside native bounds.
            RectTransform detail = Box(_ascensionCustom, "DetailPanel", new Vector2(.04f,.008f),
                new Vector2(.96f,.257f),palette.Equipment,Vector2.zero,Vector2.zero);
            Box(detail,"DetailAccent",new Vector2(0f,.73f),new Vector2(.012f,1f),
                palette.Accent,Vector2.zero,Vector2.zero);
            RectTransform selectedRect = Rect(detail,"Selection",new Vector2(.04f,.79f),
                new Vector2(.96f,.98f),Vector2.zero,Vector2.zero);
            _ascensionSelected = CreateText(selectedRect,"SelectedSkill","SELECT A TALENT NODE",13,
                TextAnchor.MiddleLeft,palette.Accent);
            _ascensionSelected.resizeTextForBestFit = false;
            RectTransform description = Rect(detail,"Description",new Vector2(.04f,.36f),
                new Vector2(.96f,.79f),Vector2.zero,Vector2.zero);
            _ascensionDescription = CreateText(description,"DescriptionText",
                "Select a node to see its effect and rank details.",11,
                TextAnchor.UpperLeft,palette.Body);
            _ascensionDescription.resizeTextForBestFit = false;
            _ascensionDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            _ascensionDescription.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform statusHost = Rect(detail,"RankState",new Vector2(.04f,.25f),
                new Vector2(.96f,.37f),Vector2.zero,Vector2.zero);
            _ascensionNodeStatus = CreateText(statusHost,"StatusText","",10,
                TextAnchor.MiddleLeft,palette.Muted);
            RectTransform upgradeHost = Box(detail,"LearnButton",new Vector2(.035f,.035f),
                new Vector2(.73f,.247f),palette.Selected,Vector2.zero,Vector2.zero);
            _ascensionUpgrade = StyledButton(upgradeHost,palette.Selected);
            _ascensionActionLabel = CreateText(upgradeHost,"LearnText","SELECT A NODE",11,
                TextAnchor.MiddleCenter,palette.Body);
            _ascensionUpgrade.onClick.AddListener(UpgradeSelectedAscension);
            _ascensionUpgrade.interactable = false;
            RectTransform respecHost = Box(detail,"RespecButton",new Vector2(.747f,.035f),
                new Vector2(.965f,.247f),palette.Tab,Vector2.zero,Vector2.zero);
            _ascensionRespec = StyledButton(respecHost,palette.Tab);
            _ascensionRespec.onClick.AddListener(RespecAscensions);
            CreateText(respecHost,"RespecText","RESPEC",10,TextAnchor.MiddleCenter,palette.Body);
            _ascensionRespec.interactable = false;
            _ascensionCustom.gameObject.SetActive(false);
            // Always keep a way back to Erenshor's original Ascension page if
            // a build has nonstandard rank/button handlers. A higher-sorted
            // small Canvas keeps the toggle clickable even over native UI.
            _ascensionViewToggle = Box(parent, "ArcaneUI_AscensionViewToggle",
                new Vector2(.77f,.937f), new Vector2(.965f,.985f),
                palette.Tab, Vector2.zero, Vector2.zero);
            Button viewButton = StyledButton(_ascensionViewToggle, palette.Tab);
            _ascensionViewToggleText = CreateText(_ascensionViewToggle,
                "ToggleText", "NATIVE VIEW", 10, TextAnchor.MiddleCenter, palette.Body);
            UnityEngine.Canvas canvas = _ascensionViewToggle.gameObject.AddComponent<UnityEngine.Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 245;
            _ascensionViewToggle.gameObject.AddComponent<GraphicRaycaster>();
            viewButton.onClick.AddListener(() =>
            {
                _ascensionNativeView = !_ascensionNativeView;
                ShowPage(1);
                RefreshAscensionWeb(true);
            });
            _ascensionViewToggle.gameObject.SetActive(false);
        }
        private bool ReadAscensionLines()
        {
            _ascensionLines.Clear();
            if (_nativeAscension == null) return false;
            int candidateRows = 0;
            int namedRows = 0;
            // Native hierarchy: AAScreen/Skill, Skill (1) ... Skill (7).
            // A row is useful for DISPLAY even if this particular game build
            // exposes its purchase handler differently. Only verified native
            // Buttons with a verified rank are ever allowed to spend points.
            foreach (Transform row in _nativeAscension.GetComponentsInChildren<Transform>(true))
            {
                if (row == _nativeAscension ||
                    !Regex.IsMatch(row.name, @"^Skill(?:\s*\(\d+\))?$", RegexOptions.IgnoreCase))
                    continue;
                candidateRows++;
                Button add = null;
                Transform direct = row.Find("Add");
                if (direct != null)
                {
                    add = direct.GetComponent<Button>();
                    if (add == null) add = direct.GetComponentInChildren<Button>(true);
                }
                if (add == null)
                    foreach (Button candidate in row.GetComponentsInChildren<Button>(true))
                        if (candidate != null && (candidate.name.Equals("Add", StringComparison.OrdinalIgnoreCase) ||
                            TextOf(candidate.transform).Trim() == "+")) { add = candidate; break; }

                // Some Erenshor versions enable Add only once the Skill row
                // has received its own selection click. Prefer a Button on
                // the row itself, then a non-Add descendant. Neither is ever
                // treated as an authoritative purchase callback.
                Button select = row.GetComponent<Button>();
                if (select == add) select = null;
                if (select == null)
                    foreach (Button candidate in row.GetComponentsInChildren<Button>(true))
                    {
                        if (candidate == null || candidate == add ||
                            (add != null && Under(candidate.transform, add.transform)) ||
                            candidate.name.Equals("Add", StringComparison.OrdinalIgnoreCase)) continue;
                        select = candidate;
                        break;
                    }
                string all = AllText(row);
                int rank = -1;
                Match count = Regex.Match(all, @"(?<!\d)([0-3])\s*/\s*3\b");
                if (count.Success) int.TryParse(count.Groups[1].Value, out rank);
                string name = null;
                foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text == null || (add != null && Under(text.transform, add.transform))) continue;
                    foreach (string item in Clean(text.text).Split('\n'))
                        if (IsSkillName(item.Trim())) { name = item.Trim(); break; }
                    if (name != null) break;
                }
                if (name == null)
                    foreach (Text text in row.GetComponentsInChildren<Text>(true))
                    {
                        if (text == null || (add != null && Under(text.transform, add.transform))) continue;
                        foreach (string item in Clean(text.text).Split('\n'))
                            if (IsSkillName(item.Trim())) { name = item.Trim(); break; }
                        if (name != null) break;
                    }
                // Some native versions combine a label and rank on the same
                // line, or put rank text before the skill name.
                if (name == null)
                    foreach (string item in all.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string candidate = Regex.Replace(item.Trim(), @"\s*\d+\s*/\s*3\b", "").Trim();
                        if (IsSkillName(candidate)) { name = candidate; break; }
                    }
                if (name == null) continue;
                name = Regex.Replace(name, @"\s*\d+\s*/\s*3\b", "").Trim();
                namedRows++;
                _ascensionLines.Add(new AscensionLine { Name = name,
                    Rank = rank < 0 ? -1 : Mathf.Clamp(rank, 0, 3),
                    NativeUpgrade = add, NativeSelect = select, Description = NativeSkillDescription(row, name) });
            }
            if ((candidateRows != _lastAscensionRowCount || namedRows != _lastAscensionNamedCount))
            {
                _lastAscensionRowCount = candidateRows;
                _lastAscensionNamedCount = namedRows;
                Plugin.LogInfo("ArcaneUI Ascension web scan: rows=" + candidateRows +
                    ", named=" + namedRows + ", displayed=" + _ascensionLines.Count +
                    ", mode=" + (_ascensionMode == 0 ? "General" : "Class") + ".");
            }
            return _ascensionLines.Count > 0;
        }
        private void SetAscensionMode(int mode)
        {
            if (mode < 0 || mode > 1 || _nativeAscension == null) return;
            Button native = _nativeAscensionSwitch[mode];
            if (native == null)
            {
                Plugin.LogWarning("ArcaneUI Ascension: native " + (mode == 0 ? "General" : "Class") +
                    " switch not found. Showing currently active native skill rows without changing the category.");
                // Do not also suppress the web. Some Erenshor versions keep
                // these switch controls outside AAScreen; their absence must
                // not force the entire page back to native presentation.
                _nextAscensionRead = 0f;
                RefreshAscensionWeb(true);
                return;
            }
            _ascensionMode = mode;
            _pendingAscensionName = null;
            _selectedAscensionLine = -1;
            _selectedAscensionTier = 0;
            _ascensionSignature = "";
            if (native.onClick != null)
            {
                try { native.onClick.Invoke(); }
                catch (Exception ex) { Plugin.LogWarning("ArcaneUI ascension native switch failed: " + ex.GetType().Name); }
            }
            _nextAscensionRead = 0f;
            RefreshAscensionWeb(true);
        }
        private void SelectAscensionNode(int line,int tier)
        {
            _selectedAscensionLine=line;
            _selectedAscensionTier=tier;
            RefreshAscensionDetail();
            UpdateAscensionNodeSelection();
        }
        private void ActivateAscensionNode(int line, int tier)
        {
            SelectAscensionNode(line, tier);
            // Node clicks buy only the immediate next rank; learned or locked
            // nodes remain inspection-only. Hover inspection never purchases.
            if (line >= 0 && line < _ascensionLines.Count &&
                _ascensionLines[line].Rank == tier)
                UpgradeSelectedAscension();
        }
        private int AvailableAscensionPoints()
        {
            Match count = Regex.Match(AllText(_nativeAscension),
                @"Ascension\s*Points\s*:\s*(\d+)", RegexOptions.IgnoreCase);
            int points;
            return count.Success && int.TryParse(count.Groups[1].Value, out points) ? points : -1;
        }
        private void CheckPendingAscensionPurchase()
        {
            if (_verifyAscensionName != null && Time.unscaledTime >= _verifyAscensionAt)
            {
                AscensionLine verify = _ascensionLines.Find(x => x.Name == _verifyAscensionName);
                if (verify != null && verify.Rank <= _verifyAscensionRank)
                    Plugin.LogWarning("ArcaneUI Ascension: native Add callback did not advance " +
                        _verifyAscensionName + " from rank " + _verifyAscensionRank +
                        ". Check native requirements or use NATIVE VIEW.");
                _verifyAscensionName = null;
            }
            if (string.IsNullOrEmpty(_pendingAscensionName)) return;
            if (_ascensionMode != _pendingAscensionMode || Time.unscaledTime >= _pendingAscensionUntil)
            {
                Plugin.LogWarning("ArcaneUI Ascension: native Add did not become interactable for " +
                    _pendingAscensionName + ". Use NATIVE VIEW to inspect the game's purchase restriction.");
                _pendingAscensionName = null;
                return;
            }
            AscensionLine line = _ascensionLines.Find(x => x.Name == _pendingAscensionName);
            if (line == null || line.Rank != _pendingAscensionRank)
            {
                _pendingAscensionName = null;
                return;
            }
            if (line.NativeUpgrade == null || !line.NativeUpgrade.IsActive() ||
                !line.NativeUpgrade.IsInteractable()) return;
            // A valid next rank, confirmed available points, and a native
            // interactable Add button are required at invocation time.
            if (AvailableAscensionPoints() <= 0) { _pendingAscensionName = null; return; }
            _pendingAscensionName = null;
            try
            {
                line.NativeUpgrade.onClick.Invoke();
                _verifyAscensionName = line.Name;
                _verifyAscensionRank = line.Rank;
                _verifyAscensionAt = Time.unscaledTime + 1.1f;
                Plugin.LogInfo("ArcaneUI Ascension: dispatched native Add for " + line.Name +
                    " rank " + (line.Rank + 1) + ". Awaiting native rank confirmation.");
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("ArcaneUI native Ascension purchase failed: " + ex.GetType().Name);
            }
            _ascensionSignature = "";
            _nextAscensionRead = 0f;
        }
        private void RefreshAscensionDetail()
        {
            if (_ascensionSelected == null || _ascensionDescription == null || _ascensionUpgrade == null) return;
            if (_selectedAscensionLine < 0 || _selectedAscensionLine >= _ascensionLines.Count)
            {
                _ascensionSelected.text = "SELECT A TALENT NODE";
                _ascensionDescription.text = "Choose one of the three rank nodes along a branch to inspect its effect. General and Class have separate webs.";
                if (_ascensionNodeStatus != null) _ascensionNodeStatus.text = "RANK I  →  RANK II  →  RANK III";
                _ascensionActionLabel.text = "SELECT A NODE";
                _ascensionUpgrade.interactable = false;
                return;
            }
            AscensionLine line = _ascensionLines[_selectedAscensionLine];
            int displayedRank = _selectedAscensionTier + 1;
            _ascensionSelected.text = line.Name.ToUpperInvariant() + "     •     RANK " + displayedRank + " / 3";
            bool knownRank = line.Rank >= 0;
            bool bought = knownRank && line.Rank > _selectedAscensionTier;
            bool next = knownRank && line.Rank == _selectedAscensionTier;
            _ascensionDescription.text = !string.IsNullOrWhiteSpace(line.Description)
                ? line.Description
                : "Effect description is not exposed by this Erenshor skill row. " +
                  "ArcaneUI will not guess its bonuses; the native skill determines the actual effect.";
            if (_ascensionNodeStatus != null)
                _ascensionNodeStatus.text = (knownRank ? "LEARNED " + line.Rank + "/3" : "NATIVE RANK NOT FOUND") + "     •     " +
                    (bought ? "RANK UNLOCKED" : next ? "NEXT RANK" : knownRank ? "REQUIRES PREVIOUS RANKS" : "VIEW ONLY");
            // Allow the request even when the *native* Add button currently
            // needs the skill row selected. The pending native-only dispatcher
            // still refuses to invoke a disabled purchase button.
            _ascensionUpgrade.interactable = next && line.NativeUpgrade != null &&
                AvailableAscensionPoints() > 0;
            _ascensionActionLabel.text = !knownRank ? "RANK UNVERIFIED" : bought ? "RANK LEARNED" : next ?
                (_ascensionUpgrade.interactable ? "LEARN RANK " + displayedRank : "NOT AVAILABLE") : "LOCKED RANK";
        }
        private void RespecAscensions()
        {
            // Send the original respec click. The game owns any confirmation,
            // currency checks, refund calculations and progression changes.
            if (_nativeRespec == null || !_nativeRespec.IsActive() ||
                !_nativeRespec.IsInteractable()) return;
            try { _nativeRespec.onClick.Invoke(); }
            catch (Exception ex)
            {
                Plugin.LogWarning("ArcaneUI native Ascension respec failed: " + ex.GetType().Name);
            }
            _ascensionSignature = "";
            _nextAscensionRead = 0f;
            RefreshAscensionWeb(true);
        }
        private void UpgradeSelectedAscension()
        {
            if (_selectedAscensionLine < 0 || _selectedAscensionLine >= _ascensionLines.Count) return;
            AscensionLine line = _ascensionLines[_selectedAscensionLine];
            if (_selectedAscensionTier != line.Rank || line.Rank < 0 || line.Rank >= 3 ||
                line.NativeUpgrade == null || AvailableAscensionPoints() <= 0) return;
            // Stage the request by exact name, class-mode and rank. This
            // protects against the underlying native rows being repopulated.
            _pendingAscensionName = line.Name;
            _pendingAscensionRank = line.Rank;
            _pendingAscensionMode = _ascensionMode;
            _pendingAscensionUntil = Time.unscaledTime + 1.5f;
            if (line.NativeSelect != null && line.NativeSelect.IsActive() &&
                line.NativeSelect.IsInteractable())
            {
                try { line.NativeSelect.onClick.Invoke(); }
                catch (Exception ex)
                {
                    Plugin.LogWarning("ArcaneUI native Ascension row selection failed: " + ex.GetType().Name);
                }
            }
            _nextAscensionRead = 0f;
            RefreshAscensionWeb(true);
        }
        private void RenderAscensionWeb(SheetPalette palette)
        {
            if (_ascensionWeb == null) return;
            _ascensionNodeSelectionRings.Clear();
            _ascensionLineLabels.Clear();
            for (int i = _ascensionWeb.childCount - 1; i >= 0; --i)
            {
                Transform child = _ascensionWeb.GetChild(i);
                if (child.name != "WebBack" && child.name != "TopTrim" && child.name != "BottomTrim")
                    UnityEngine.Object.Destroy(child.gameObject);
            }
            int total = _ascensionLines.Count;
            if (total == 0) return;
            Vector2 center = new Vector2(.5f,.5f);
            Vector2[] directions = new Vector2[total];
            Vector2[,] positions = new Vector2[total,3];
            float[] radii = { .132f, .228f, .324f };
            // Delicate constellation links form three closed rank tiers;
            // polygon links avoid stretched circular sprites and huge borders.
            for (int i=0; i<total; i++)
            {
                float angle = Mathf.PI*.5f + 2f*Mathf.PI*i/total;
                directions[i] = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                for (int tier=0;tier<3;tier++)
                    positions[i,tier] = center + directions[i]*radii[tier];
            }
            if (total >= 3)
                for (int tier=0; tier<3; tier++)
                    for (int i=0;i<total;i++)
                    {
                        Color orbitColor = Color.Lerp(palette.Trim,palette.RowB,.65f);
                        LineBetween(_ascensionWeb,"TierLink_"+tier+"_"+i,
                            positions[i,tier],positions[(i+1)%total,tier],orbitColor,1f);
                    }
            for (int i=0; i<total; i++)
            {
                Vector2 last = center;
                for (int tier=0;tier<3;tier++)
                {
                    LineBetween(_ascensionWeb,"Connection_"+i+"_"+tier,last,positions[i,tier],
                        tier<_ascensionLines[i].Rank?palette.Accent:palette.Trim,
                        tier<_ascensionLines[i].Rank?CharacterSheetSettings.AscensionLinkWidth * 1.8f:CharacterSheetSettings.AscensionLinkWidth);
                    last=positions[i,tier];
                }
            }
            RectTransform coreHalo = CenteredRect(_ascensionWeb,"CoreHalo",center,new Vector2(62f,62f));
            RoundGraphic(coreHalo,palette.Trim,true);
            RectTransform core = CenteredRect(_ascensionWeb,"Core",center,new Vector2(45f,45f));
            RoundGraphic(core,palette.Section,false);
            CreateText(core,"CoreLabel",_ascensionMode==0?"GEN":"CLS",11,
                TextAnchor.MiddleCenter,palette.Accent);
            // Keep all label backgrounds clear of the clickable rank circles,
            // including the larger selection rings. A multi-line name should
            // never be rendered over a rank node or its link halo.
            Vector2 canvasSize = _ascensionWeb.rect.size;
            if (canvasSize.x < 200f || canvasSize.y < 200f)
                canvasSize = new Vector2(405f, 420f);
            Vector2 labelSize = total > 8 ? new Vector2(76f, 30f) : new Vector2(96f, 34f);
            List<Vector2> placedLabels = new List<Vector2>();
            for (int i=0;i<total;i++)
            {
                int index=i;
                AscensionLine line=_ascensionLines[i];
                for (int tier=0;tier<3;tier++)
                {
                    int clickedTier=tier;
                    RectTransform selection = CenteredRect(_ascensionWeb,"Selection_"+i+"_"+tier,
                        positions[i,tier],new Vector2(CharacterSheetSettings.AscensionNodeSize + 10f,CharacterSheetSettings.AscensionNodeSize + 10f));
                    Image ring = RoundGraphic(selection,palette.Accent,true);
                    _ascensionNodeSelectionRings[i*3+tier]=ring;
                    bool learned=tier<line.Rank;
                    bool available=tier==line.Rank;
                    RectTransform node=CenteredRect(_ascensionWeb,"Node_"+i+"_"+tier,
                        positions[i,tier],Vector2.one * CharacterSheetSettings.AscensionNodeSize);
                    Color fill=learned?palette.Selected: available?palette.Section:palette.Slot;
                    Button button=StyledButton(node,fill);
                    Image nodeImage = node.GetComponent<Image>();
                    nodeImage.sprite=AscensionCircle(false);
                    button.targetGraphic=nodeImage;
                    button.onClick.AddListener(() => ActivateAscensionNode(index,clickedTier));
                    if (CharacterSheetSettings.AscensionHoverInspect)
                    {
                        EventTrigger events = node.gameObject.AddComponent<EventTrigger>();
                        EventTrigger.Entry enter = new EventTrigger.Entry();
                        enter.eventID = EventTriggerType.PointerEnter;
                        enter.callback.AddListener(data => SelectAscensionNode(index, clickedTier));
                        events.triggers.Add(enter);
                    }
                    RectTransform nodeBorder=CenteredRect(node,"Border",new Vector2(.5f,.5f),
                        Vector2.one * CharacterSheetSettings.AscensionNodeSize);
                    RoundGraphic(nodeBorder,learned?palette.Accent:available?palette.Trim:palette.Muted,true);
                    CreateText(node,"RankText",(tier+1).ToString(),12,TextAnchor.MiddleCenter,
                        learned?palette.Accent:available?palette.Body:palette.Muted);
                }
                if (CharacterSheetSettings.AscensionShowLabels)
                {
                    // A dense web must not silently drop native branch names.
                    // Retry with compact labels before accepting the least
                    // colliding position as a last resort.
                    bool hasRoom;
                    Vector2 usedSize = labelSize;
                    Vector2 labelAt = FindAscensionLabelAnchor(directions[i], positions, total,
                        placedLabels, canvasSize, usedSize, out hasRoom);
                    if (!hasRoom)
                    {
                        usedSize = new Vector2(74f, 28f);
                        labelAt = FindAscensionLabelAnchor(directions[i], positions, total,
                            placedLabels, canvasSize, usedSize, out hasRoom);
                    }
                    if (!hasRoom)
                    {
                        usedSize = new Vector2(62f, 26f);
                        labelAt = FindAscensionLabelAnchor(directions[i], positions, total,
                            placedLabels, canvasSize, usedSize, out hasRoom);
                        if (!hasRoom) placedLabels.Add(new Vector2(labelAt.x * canvasSize.x,
                            labelAt.y * canvasSize.y));
                    }
                    RectTransform labelBack = CenteredRect(_ascensionWeb, "SpokeLabel_" + i,
                        labelAt, usedSize);
                    Image labelPanel = labelBack.gameObject.AddComponent<Image>();
                    labelPanel.color = palette.Header;
                    labelPanel.raycastTarget = false;
                    Text title = CreateText(labelBack, "Name", WebSkillName(line.Name),
                        hasRoom ? 10 : 8, TextAnchor.MiddleCenter, palette.Body);
                    title.resizeTextForBestFit = !hasRoom;
                    title.resizeTextMinSize = 7;
                    title.resizeTextMaxSize = hasRoom ? 10 : 9;
                    title.horizontalOverflow = HorizontalWrapMode.Wrap;
                    title.verticalOverflow = VerticalWrapMode.Truncate;
                    _ascensionLineLabels[i] = title;
                }
            }
            UpdateAscensionNodeSelection();
        }
        private void RefreshAscensionWeb(bool force)
        {
            if (_ascensionCustom == null || _activeTab != 1 || _nativeAscension == null) return;
            if (!force && Time.unscaledTime < _nextAscensionRead) return;
            _nextAscensionRead=Time.unscaledTime+.45f;
            // The web may DISPLAY a native skill line even if the game build
            // exposes a nonstandard switch/upgrade control. Purchases and
            // category changes still require the verified native callbacks.
            _nativeAscensionSwitch[0]=FindNativeAscensionSwitch("General");
            _nativeAscensionSwitch[1]=FindNativeAscensionSwitch("Class");
            RectTransform playerInv = _native != null ? _native.Find("PlayerInv") as RectTransform : null;
            Transform respec = playerInv != null ? playerInv.Find("Respec") : null;
            _nativeRespec = respec != null ? respec.GetComponent<Button>() : null;
            if (_ascensionRespec != null)
                _ascensionRespec.interactable = _nativeRespec != null &&
                    _nativeRespec.IsActive() && _nativeRespec.IsInteractable();
            bool validSwitches=_nativeAscensionSwitch[0]!=null && _nativeAscensionSwitch[1]!=null;
            bool validLines=ReadAscensionLines();
            _ascensionMapped=validLines;
            for (int i = 0; i < _ascensionSwitch.Length; i++)
                if (_ascensionSwitch[i] != null)
                    _ascensionSwitch[i].interactable = _nativeAscensionSwitch[i] != null;
            // Display all verified skill rows in a themed web. Missing native
            // purchase/rank handlers remain read-only; native view is still
            // one click away for original Erenshor gameplay controls.
            bool showWeb = _ascensionMapped && !_ascensionNativeView;
            SetNativeVisibility(_nativeAscension,showWeb);
            if (showWeb)
            {
                // Visually suppress/raycast-disable the hidden native page,
                // but preserve its original interactable state. Otherwise
                // Unity Selectable.IsInteractable() incorrectly reports every
                // underlying Skill/Add button as disabled, even when the game
                // permits a purchase. Native callbacks remain authoritative.
                CanvasGroup group=_nativeAscension.GetComponent<CanvasGroup>();
                if (group!=null)
                    foreach (NativeVisibility snapshot in _nativeVisibility)
                        if(snapshot.Group==group) { group.interactable=snapshot.Interactable; break; }
            }
            _ascensionCustom.gameObject.SetActive(showWeb);
            if (_ascensionViewToggle != null)
            {
                _ascensionViewToggle.gameObject.SetActive(_ascensionMapped);
                if (_ascensionViewToggleText != null)
                    _ascensionViewToggleText.text = _ascensionNativeView ? "SHOW WEB" : "NATIVE VIEW";
            }
            if (_ascensionMapped)
            {
                // The native screen may reorder itself after a tab click.
                // Keep the new web above it, never buried underneath.
                if (showWeb && _panel != null) _panel.SetAsLastSibling();
                else if (_ascensionNativeView && _nativeAscension != null)
                    _nativeAscension.SetAsLastSibling();
                UnityEngine.Canvas.ForceUpdateCanvases();
                if (!_ascensionLogged)
                {
                    _ascensionLogged=true;
                    Plugin.LogInfo("ArcaneUI Ascension web ACTIVE: " + _ascensionLines.Count +
                        " native skill lines, General/Class switches=" + validSwitches +
                        ", 3 displayed rank nodes per line. Verified native buttons alone can purchase.");
                }
            }
            if (!_ascensionMapped)
            {
                if (!_ascensionWarned)
                {
                    _ascensionWarned=true;
                    Plugin.LogWarning("ArcaneUI Ascension web: no named native Skill rows found (" +
                        _lastAscensionRowCount + " candidates). Native Ascension UI retained; " +
                        "send the Ascension web scan log for diagnosis.");
                }
                return;
            }
            StringBuilder signature=new StringBuilder().Append(_ascensionMode);
            foreach (AscensionLine line in _ascensionLines)
                signature.Append('|').Append(line.Name).Append(':').Append(line.Rank);
            if (force || _ascensionSignature!=signature.ToString())
            {
                _ascensionSignature=signature.ToString();
                if (_selectedAscensionLine>=_ascensionLines.Count) _selectedAscensionLine=-1;
                RenderAscensionWeb(Palette());
            }
            string all=AllText(_nativeAscension);
            Match points=Regex.Match(all,@"Ascension\s*Points\s*:\s*(\d+)",RegexOptions.IgnoreCase);
            Match spent=Regex.Match(all,@"Points\s*spent\s*:\s*(\d+)",RegexOptions.IgnoreCase);
            _ascensionPoints.text="AVAILABLE  " + (points.Success?points.Groups[1].Value:"—")+
                "   •   SPENT  " + (spent.Success?spent.Groups[1].Value:"—");
            for(int i=0;i<2;i++)
            {
                if(_ascensionSwitchBars[i]!=null)
                    _ascensionSwitchBars[i].gameObject.SetActive(i==_ascensionMode);
                if(_ascensionSwitch[i]!=null)
                    _ascensionSwitch[i].GetComponent<Image>().color=i==_ascensionMode?Palette().Selected:Palette().Tab;
            }
            RefreshAscensionDetail();
            CheckPendingAscensionPurchase();
        }
    }
}
