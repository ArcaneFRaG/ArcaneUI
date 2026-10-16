using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class GroupOptionsButton
    {
        private const string ObjectName = "ArcanesGroupFrames_GroupOptionsButton";

        private static GameObject _buttonObject;
        private static SimPlayerGrouping _owner;
        private static RectTransform _resizedParent;
        private static Vector2 _originalParentSize;
        private static bool _parentSizeCaptured;
        private static float _retryTimer;

        internal static void Initialize()
        {
            TryInstall();
        }

        internal static void Update()
        {
            if (_buttonObject != null &&
                _owner != null &&
                _owner == GameData.SimPlayerGrouping)
            {
                return;
            }

            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer > 0f)
            {
                return;
            }

            _retryTimer = 1f;
            TryInstall();
        }

        private static void TryInstall()
        {
            SimPlayerGrouping grouping = GameData.SimPlayerGrouping;
            if (grouping == null)
            {
                return;
            }

            Transform existing = grouping.transform.Find(ObjectName);
            if (existing != null)
            {
                _buttonObject = existing.gameObject;
                _owner = grouping;
                WireButton(_buttonObject.GetComponent<Button>());
                return;
            }

            Button source = FindButton(grouping, "Loot Distribution");
            if (source == null)
            {
                source = FindButtonGlobal("Loot Distribution");
            }

            if (source == null)
            {
                return;
            }

            _buttonObject = UnityEngine.Object.Instantiate(
                source.gameObject,
                source.transform.parent,
                false);

            _buttonObject.name = ObjectName;
            _owner = grouping;

            SetLabel(_buttonObject, "Options");

            Button button = _buttonObject.GetComponent<Button>();
            WireButton(button);

            RectTransform sourceRect = source.transform as RectTransform;
            RectTransform newRect = _buttonObject.transform as RectTransform;

            if (newRect != null && sourceRect != null)
            {
                newRect.SetSiblingIndex(sourceRect.GetSiblingIndex() + 1);

                float spacing = 2f;
                float step = Mathf.Abs(sourceRect.rect.height) + spacing;
                if (step <= spacing)
                {
                    step = Mathf.Abs(sourceRect.sizeDelta.y) + spacing;
                }
                if (step <= spacing)
                {
                    step = 24f;
                }

                VerticalLayoutGroup layout =
                    source.transform.parent.GetComponent<VerticalLayoutGroup>();

                if (layout == null)
                {
                    Vector2 position = sourceRect.anchoredPosition;
                    position.y -= step;
                    newRect.anchoredPosition = position;
                }

                // The native command strip is sized for exactly its stock
                // buttons.  A cloned button can therefore exist but be
                // clipped just below Loot Distribution.  Grow the immediate
                // list container by one button height so Options is visible.
                RectTransform parentRect =
                    source.transform.parent as RectTransform;

                if (parentRect != null)
                {
                    _resizedParent = parentRect;
                    _originalParentSize = parentRect.sizeDelta;
                    _parentSizeCaptured = true;

                    Vector2 size = _originalParentSize;
                    size.y += step;
                    parentRect.sizeDelta = size;
                }
            }

}

        private static Button FindButton(
            SimPlayerGrouping grouping,
            string wantedLabel)
        {
            Button[] buttons = grouping.GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                if (LabelMatches(
                    button.gameObject,
                    wantedLabel))
                {
                    return button;
                }
            }

            return null;
        }


        private static Button FindButtonGlobal(
            string wantedLabel)
        {
            Button[] buttons =
                Resources.FindObjectsOfTypeAll<Button>();

            foreach (Button button in buttons)
            {
                if (button == null ||
                    button.gameObject == null ||
                    !button.gameObject.scene.IsValid() ||
                    !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (LabelMatches(
                    button.gameObject,
                    wantedLabel))
                {
                    return button;
                }
            }

            return null;
        }

        private static bool LabelMatches(
            GameObject obj,
            string wantedLabel)
        {
            if (obj == null)
            {
                return false;
            }

            string wanted = Normalize(wantedLabel);

            TMP_Text[] tmpTexts =
                obj.GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text tmp in tmpTexts)
            {
                if (tmp != null &&
                    string.Equals(
                        Normalize(tmp.text),
                        wanted,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            Text[] legacyTexts =
                obj.GetComponentsInChildren<Text>(true);

            foreach (Text text in legacyTexts)
            {
                if (text != null &&
                    string.Equals(
                        Normalize(text.text),
                        wanted,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetLabel(GameObject obj)
        {
            Text legacy = obj.GetComponentInChildren<Text>(true);
            if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text))
            {
                return legacy.text;
            }

            TMP_Text tmp = obj.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            {
                return tmp.text;
            }

            return string.Empty;
        }

        private static void SetLabel(GameObject obj, string value)
        {
            Text legacy = obj.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = value;
            }

            TMP_Text tmp = obj.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = value;
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace(" ", string.Empty)
                .Replace(".", string.Empty)
                .Replace("-", string.Empty)
                .Trim();
        }

        private static void WireButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            // The Options button is cloned from the native Loot Distribution
            // button so it inherits its serialized Button.onClick persistent
            // listener. RemoveAllListeners() only clears runtime listeners;
            // it does not remove persistent listeners baked into the prefab.
            //
            // Replace the entire event object instead so the clone cannot
            // invoke Loot Distribution when clicked.
            button.onClick =
                new Button.ButtonClickedEvent();

            button.onClick.AddListener(
                GroupFramesOptionsUI.Toggle);

            // Also strip any EventTrigger copied from the native source
            // button. The stock button may use one for pointer-click actions
            // outside Button.onClick.
            UnityEngine.EventSystems.EventTrigger trigger =
                button.GetComponent<UnityEngine.EventSystems.EventTrigger>();

            if (trigger != null)
            {
                UnityEngine.Object.Destroy(trigger);
            }
        }

        internal static void Shutdown()
        {
            if (_buttonObject != null)
            {
                UnityEngine.Object.Destroy(_buttonObject);
            }

            if (_parentSizeCaptured &&
                _resizedParent != null)
            {
                _resizedParent.sizeDelta =
                    _originalParentSize;
            }

            _buttonObject = null;
            _owner = null;
            _resizedParent = null;
            _originalParentSize = Vector2.zero;
            _parentSizeCaptured = false;
            _retryTimer = 0f;
        }
    }
}
