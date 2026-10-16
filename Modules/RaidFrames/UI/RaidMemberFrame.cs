using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Theming.Rendering;
using ArcanesGroupFrames.Framework.Modules;
namespace ArcanesGroupFrames
{
    internal sealed class RaidMemberFrame
    {
        // ============================================================
        // DATA
        // ============================================================

        private readonly RaidMemberSlot _slot;

        private readonly SimPlayerTracking _groupMember;

        private readonly bool _player;

        private readonly bool _partyContext;

        private readonly int _groupSlotIndex;


        // ============================================================
        // UI
        // ============================================================

        private readonly GameObject _root;

        private readonly RectTransform _rootRect;

        private readonly Image _background;

        private readonly Image _healthFill;

        private readonly Image _absorbFill;

        private readonly Image _manaBackground;

        private readonly Image _manaFill;

        private readonly GameObject _targetHighlight;

        private readonly Image _targetTop;

        private readonly Image _targetBottom;

        private readonly Image _targetLeft;

        private readonly Image _targetRight;

        private readonly GameObject _aggroHighlight;

        private readonly Image _aggroTop;

        private readonly Image _aggroBottom;

        private readonly Image _aggroLeft;

        private readonly Image _aggroRight;

        private readonly Text _nameText;

        private readonly Text _hpPercentText;

        private readonly Text _rawHealthText;

        private readonly Button _kickButton;

        private readonly Button _combatRezButton;

        private readonly StatusIndicator[] _debuffIndicators;

        private readonly StatusIndicator[] _buffIndicators;

        private static FieldInfo _spellSpriteField;

        private static bool _spellSpriteFieldResolved;


        // ============================================================
        // CONSTRUCTOR - RAID MEMBER
        // ============================================================

        internal RaidMemberFrame(
            RectTransform parent,
            RaidMemberSlot slot)
        {
            _slot =
                slot;


            _groupMember =
                null;


            _player =
                false;


            _partyContext =
                false;


            _groupSlotIndex =
                -1;


            _root =
                CreateRoot(
                    parent);


            _rootRect =
                _root.GetComponent<RectTransform>();


            _background =
                CreateBackground();


            _healthFill =
                CreateHealthFill();

            _absorbFill =
                CreateAbsorbFill();


            _manaBackground =
                CreateManaBackground();


            _manaFill =
                CreateManaFill();


            _nameText =
                CreateNameText();


            _hpPercentText =
                CreateHpPercentText();


            _rawHealthText =
                CreateRawHealthText();


            _targetHighlight =
                CreateTargetHighlight(
                    out _targetTop,
                    out _targetBottom,
                    out _targetLeft,
                    out _targetRight);


            _aggroHighlight =
                CreateAggroHighlight(
                    out _aggroTop,
                    out _aggroBottom,
                    out _aggroLeft,
                    out _aggroRight);


            _kickButton =
                null;


            _combatRezButton =
                CreateCombatRezButton();


            _debuffIndicators =
                CreateStatusIndicators(
                    true);


            _buffIndicators =
                CreateStatusIndicators(
                    false);


            ConfigureInteraction();

            ConfigureRaidDrag();


            Refresh();
        }


        // ============================================================
        // CONSTRUCTOR - PLAYER
        // ============================================================

        internal RaidMemberFrame(
            RectTransform parent,
            bool partyContext = false)
        {
            _slot =
                null;


            _groupMember =
                null;


            _player =
                true;


            _partyContext =
                partyContext;


            _groupSlotIndex =
                -1;


            _root =
                CreateRoot(
                    parent);


            _rootRect =
                _root.GetComponent<RectTransform>();


            _background =
                CreateBackground();


            _healthFill =
                CreateHealthFill();

            _absorbFill =
                CreateAbsorbFill();


            _manaBackground =
                CreateManaBackground();


            _manaFill =
                CreateManaFill();


            _nameText =
                CreateNameText();


            _hpPercentText =
                CreateHpPercentText();


            _rawHealthText =
                CreateRawHealthText();


            _targetHighlight =
                CreateTargetHighlight(
                    out _targetTop,
                    out _targetBottom,
                    out _targetLeft,
                    out _targetRight);


            _aggroHighlight =
                CreateAggroHighlight(
                    out _aggroTop,
                    out _aggroBottom,
                    out _aggroLeft,
                    out _aggroRight);


            _kickButton =
                null;


            _combatRezButton =
                CreateCombatRezButton();


            _debuffIndicators =
                CreateStatusIndicators(
                    true);


            _buffIndicators =
                CreateStatusIndicators(
                    false);


            ConfigureInteraction();


            Refresh();
        }


        // ============================================================
        // CONSTRUCTOR - NORMAL GROUP MEMBER
        // ============================================================

        internal RaidMemberFrame(
            RectTransform parent,
            SimPlayerTracking groupMember,
            int groupSlotIndex)
        {
            _slot =
                null;


            _groupMember =
                groupMember;


            _player =
                false;


            _partyContext =
                true;


            _groupSlotIndex =
                groupSlotIndex;


            _root =
                CreateRoot(
                    parent);


            _rootRect =
                _root.GetComponent<RectTransform>();


            _background =
                CreateBackground();


            _healthFill =
                CreateHealthFill();

            _absorbFill =
                CreateAbsorbFill();


            _manaBackground =
                CreateManaBackground();


            _manaFill =
                CreateManaFill();


            _nameText =
                CreateNameText();


            _hpPercentText =
                CreateHpPercentText();


            _rawHealthText =
                CreateRawHealthText();


            _targetHighlight =
                CreateTargetHighlight(
                    out _targetTop,
                    out _targetBottom,
                    out _targetLeft,
                    out _targetRight);


            _aggroHighlight =
                CreateAggroHighlight(
                    out _aggroTop,
                    out _aggroBottom,
                    out _aggroLeft,
                    out _aggroRight);


            _kickButton =
                CreatePartyKickButton();


            _combatRezButton =
                CreateCombatRezButton();


            _debuffIndicators =
                CreateStatusIndicators(
                    true);


            _buffIndicators =
                CreateStatusIndicators(
                    false);


            ConfigureInteraction();


            Refresh();
        }


        // ============================================================
        // PARTY KICK BUTTON
        // ============================================================

        private Button CreatePartyKickButton()
        {
            if (!_partyContext ||
                _player ||
                _groupMember == null ||
                _groupSlotIndex < 0 ||
                _groupSlotIndex > 3)
            {
                return null;
            }

            GameObject obj =
                new GameObject(
                    "KickButton");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    0.5f);


            rect.anchorMax =
                new Vector2(
                    0f,
                    0.5f);


            rect.pivot =
                new Vector2(
                    1f,
                    0.5f);


            rect.anchoredPosition =
                new Vector2(
                    -3f,
                    0f);


            rect.sizeDelta =
                new Vector2(
                    17f,
                    17f);


            Image image =
                obj.AddComponent<Image>();


            image.color =
                new Color(
                    0.34f,
                    0.08f,
                    0.08f,
                    0.96f);


            Button button =
                obj.AddComponent<Button>();


            ColorBlock colors =
                button.colors;


            colors.normalColor =
                Color.white;


            colors.highlightedColor =
                new Color(
                    1f,
                    0.78f,
                    0.78f,
                    1f);


            colors.pressedColor =
                new Color(
                    0.72f,
                    0.58f,
                    0.58f,
                    1f);


            button.colors =
                colors;


            GameObject textObject =
                new GameObject(
                    "Text");


            textObject.transform.SetParent(
                obj.transform,
                false);


            RectTransform textRect =
                textObject.AddComponent<RectTransform>();


            textRect.anchorMin =
                Vector2.zero;


            textRect.anchorMax =
                Vector2.one;


            textRect.offsetMin =
                Vector2.zero;


            textRect.offsetMax =
                Vector2.zero;


            Text text =
                textObject.AddComponent<Text>();


            text.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");


            text.fontSize =
                12;


            text.fontStyle =
                FontStyle.Bold;


            text.alignment =
                TextAnchor.MiddleCenter;


            text.color =
                Color.white;


            text.raycastTarget =
                false;


            text.text =
                "X";


            AddOutline(
                textObject);


            button.onClick.AddListener(
                DismissPartyMember);


            return button;
        }


        private void DismissPartyMember()
        {
            if (!_partyContext ||
                _player ||
                _groupMember == null ||
                GameData.SimPlayerGrouping == null)
            {
                return;
            }

            // Use Erenshor's native group-dismiss methods. These perform
            // the full native cleanup for the corresponding GroupMembers
            // slot rather than mutating GameData.GroupMembers ourselves.
            switch (_groupSlotIndex)
            {
                case 0:
                    GameData.SimPlayerGrouping.DismissMember1();
                    break;

                case 1:
                    GameData.SimPlayerGrouping.DismissMember2();
                    break;

                case 2:
                    GameData.SimPlayerGrouping.DismissMember3();
                    break;

                case 3:
                    GameData.SimPlayerGrouping.DismissMember4();
                    break;
            }
        }


        // ============================================================
        // RAID COMBAT REZ BUTTON
        // ============================================================

        private Button CreateCombatRezButton()
        {
            if (_partyContext ||
                (!_player &&
                 _slot == null))
            {
                return null;
            }


            GameObject obj =
                new GameObject(
                    "CombatRezButton");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    1f,
                    1f);

            rect.anchorMax =
                new Vector2(
                    1f,
                    1f);

            rect.pivot =
                new Vector2(
                    1f,
                    1f);

            rect.anchoredPosition =
                new Vector2(
                    -2f,
                    -2f);

            rect.sizeDelta =
                new Vector2(
                    18f,
                    18f);


            Image image =
                obj.AddComponent<Image>();


            image.color =
                new Color(
                    0.08f,
                    0.42f,
                    0.34f,
                    0.98f);


            Button button =
                obj.AddComponent<Button>();


            button.targetGraphic =
                image;


            ColorBlock colors =
                button.colors;


            colors.normalColor =
                Color.white;

            colors.highlightedColor =
                new Color(
                    0.72f,
                    1f,
                    0.88f,
                    1f);

            colors.pressedColor =
                new Color(
                    0.58f,
                    0.78f,
                    0.70f,
                    1f);


            button.colors =
                colors;


            GameObject textObject =
                new GameObject(
                    "Text");


            textObject.transform.SetParent(
                obj.transform,
                false);


            RectTransform textRect =
                textObject.AddComponent<RectTransform>();


            textRect.anchorMin =
                Vector2.zero;

            textRect.anchorMax =
                Vector2.one;

            textRect.offsetMin =
                Vector2.zero;

            textRect.offsetMax =
                Vector2.zero;


            Text text =
                textObject.AddComponent<Text>();


            text.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");

            text.fontSize =
                15;

            text.fontStyle =
                FontStyle.Bold;

            text.alignment =
                TextAnchor.MiddleCenter;

            text.color =
                Color.white;

            text.raycastTarget =
                false;

            text.text =
                "+";


            AddOutline(
                textObject);


            button.onClick.AddListener(
                ExecuteCombatRez);


            obj.SetActive(
                false);


            return button;
        }


        private void ExecuteCombatRez()
        {
            if (_partyContext)
            {
                return;
            }


            if (_player)
            {
                NativeRaidBattleRezBackend
                    .BattleRezPlayer();
            }
            else
            {
                NativeRaidBattleRezBackend
                    .BattleRez(
                        _slot);
            }


            Refresh();
        }


        private void RefreshCombatRezButton()
        {
            if (_combatRezButton == null)
            {
                return;
            }


            bool visible =
                _player
                    ? NativeRaidBattleRezBackend
                        .CanBattleRezPlayer()
                    : NativeRaidBattleRezBackend
                        .CanBattleRez(
                            _slot);


            _combatRezButton
                .gameObject
                .SetActive(
                    visible);
        }


        // ============================================================
        // STATUS ICONS
        // ============================================================

        private StatusIndicator[] CreateStatusIndicators(
            bool debuff)
        {
            StatusIndicator[] indicators =
                new StatusIndicator[2];


            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(
                _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            int allowed = Mathf.Min(indicators.Length, fine.MaxStatusIcons);
            for (int i = 0;
                 i < indicators.Length;
                 i++)
            {
                indicators[i] =
                    CreateStatusIndicator(
                        debuff,
                        i);
            }


            return indicators;
        }


        private StatusIndicator CreateStatusIndicator(
            bool debuff,
            int index)
        {
            const float size = 14f;
            const float spacing = 2f;


            GameObject root =
                new GameObject(
                    debuff
                        ? "DebuffIcon"
                        : "BuffIcon");


            root.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                root.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    1f,
                    1f);


            rect.anchorMax =
                new Vector2(
                    1f,
                    1f);


            rect.pivot =
                new Vector2(
                    1f,
                    1f);


            float groupOffset =
                debuff
                    ? 3f
                    : 3f + (2f * (size + spacing));


            rect.anchoredPosition =
                new Vector2(
                    -(groupOffset + index * (size + spacing)),
                    -3f);


            rect.sizeDelta =
                new Vector2(
                    size,
                    size);


            Image border =
                root.AddComponent<Image>();


            border.color =
                debuff
                    ? new Color(0.62f, 0.12f, 0.10f, 0.98f)
                    : new Color(0.12f, 0.48f, 0.22f, 0.98f);


            border.raycastTarget =
                false;


            GameObject iconObject =
                new GameObject(
                    "Icon");


            iconObject.transform.SetParent(
                root.transform,
                false);


            RectTransform iconRect =
                iconObject.AddComponent<RectTransform>();


            iconRect.anchorMin =
                Vector2.zero;


            iconRect.anchorMax =
                Vector2.one;


            iconRect.offsetMin =
                new Vector2(
                    1f,
                    1f);


            iconRect.offsetMax =
                new Vector2(
                    -1f,
                    -1f);


            Image icon =
                iconObject.AddComponent<Image>();


            icon.color =
                new Color(
                    0.18f,
                    0.18f,
                    0.18f,
                    1f);


            icon.preserveAspect =
                true;


            icon.raycastTarget =
                false;


            GameObject fallbackObject =
                new GameObject(
                    "Fallback");


            fallbackObject.transform.SetParent(
                root.transform,
                false);


            RectTransform fallbackRect =
                fallbackObject.AddComponent<RectTransform>();


            fallbackRect.anchorMin =
                Vector2.zero;


            fallbackRect.anchorMax =
                Vector2.one;


            fallbackRect.offsetMin =
                Vector2.zero;


            fallbackRect.offsetMax =
                Vector2.zero;


            Text fallback =
                fallbackObject.AddComponent<Text>();


            fallback.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");


            fallback.fontSize =
                8;


            fallback.fontStyle =
                FontStyle.Bold;


            fallback.alignment =
                TextAnchor.MiddleCenter;


            fallback.color =
                Color.white;


            fallback.raycastTarget =
                false;


            AddOutline(
                fallbackObject);


            GameObject durationObject =
                new GameObject(
                    "Duration");


            durationObject.transform.SetParent(
                root.transform,
                false);


            RectTransform durationRect =
                durationObject.AddComponent<RectTransform>();


            durationRect.anchorMin =
                Vector2.zero;


            durationRect.anchorMax =
                Vector2.one;


            durationRect.offsetMin =
                new Vector2(
                    -2f,
                    -2f);


            durationRect.offsetMax =
                new Vector2(
                    2f,
                    2f);


            Text duration =
                durationObject.AddComponent<Text>();


            duration.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");


            duration.fontSize =
                7;


            duration.fontStyle =
                FontStyle.Bold;


            duration.alignment =
                TextAnchor.LowerRight;


            duration.color =
                Color.white;


            duration.raycastTarget =
                false;


            AddOutline(
                durationObject);


            root.SetActive(
                false);


            return new StatusIndicator(
                root,
                icon,
                fallback,
                duration,
                debuff);
        }


        private void RefreshStatusIndicators(
            Stats stats)
        {
            bool show =
                GetShowStatusIcons();


            if (!show ||
                stats == null ||
                stats.StatusEffects == null ||
                stats.CurrentHP <= 0)
            {
                HideStatusIndicators(
                    _debuffIndicators);


                HideStatusIndicators(
                    _buffIndicators);


                return;
            }


            List<StatusEffect> debuffs =
                new List<StatusEffect>();


            List<StatusEffect> buffs =
                new List<StatusEffect>();


            Character player =
                GameData.PlayerControl != null
                    ? GameData.PlayerControl.Myself
                    : null;


            for (int i = 0;
                 i < stats.StatusEffects.Length;
                 i++)
            {
                StatusEffect status =
                    stats.StatusEffects[i];


                if (status == null ||
                    status.Effect == null ||
                    status.Duration <= 0f)
                {
                    continue;
                }


                Spell effect =
                    status.Effect;


                bool beneficial =
                    effect.Type == Spell.SpellType.Beneficial ||
                    effect.TargetHealing > 0 ||
                    effect.ShieldingAmt > 0;


                bool castByPlayer =
                    status.CastedByPC ||
                    (player != null && status.Owner == player);


                if (beneficial)
                {
                    // Whitelist only persistent effects that belong to an
                    // actual Heal spell. This intentionally excludes auras,
                    // shields, stat buffs, item effects, and other beneficial
                    // StatusEffects even when the player is their owner.
                    //
                    // In practice these are the HoT carrier effects referenced
                    // by native Heal spells through StatusEffectToApply.
                    if (castByPlayer &&
                        IsHealingStatusEffect(
                            effect))
                    {
                        buffs.Add(
                            status);
                    }
                }
                else
                {
                    // Debuffs are useful regardless of who applied them.
                    debuffs.Add(
                        status);
                }
            }


            debuffs.Sort(
                CompareStatusDuration);


            buffs.Sort(
                CompareStatusDuration);


            ArcaneUiFrameFineTuning fineStatus = ArcaneUiFrameFineTuning.For(
                _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            if (!fineStatus.ShowBuffIcons) buffs.Clear();
            if (!fineStatus.ShowDebuffIcons) debuffs.Clear();
            if (fineStatus.StatusSort == "Longest First") { buffs.Reverse(); debuffs.Reverse(); }
            ApplyStatusIndicators(
                _debuffIndicators,
                debuffs);


            ApplyStatusIndicators(
                _buffIndicators,
                buffs);
        }


        private static bool IsHealingStatusEffect(
            Spell effect)
        {
            if (effect == null ||
                effect.TargetHealing <= 0 ||
                GameData.SpellDatabase == null ||
                GameData.SpellDatabase.SpellDatabase == null)
            {
                return false;
            }


            foreach (Spell healSpell
                     in GameData.SpellDatabase.SpellDatabase)
            {
                if (!HealingSpellCatalog.IsHealingSpell(
                        healSpell))
                {
                    continue;
                }


                // Most Erenshor HoTs are Heal spells whose persistent
                // healing is carried by StatusEffectToApply. Keep the
                // direct equality fallback for any native/modded Heal spell
                // that is itself used as the StatusEffect.
                if (healSpell == effect ||
                    healSpell.StatusEffectToApply == effect)
                {
                    return true;
                }
            }


            return false;
        }


        private static int CompareStatusDuration(
            StatusEffect left,
            StatusEffect right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }


            if (left == null)
            {
                return 1;
            }


            if (right == null)
            {
                return -1;
            }


            return left.Duration.CompareTo(
                right.Duration);
        }


        private void ApplyStatusIndicators(
            StatusIndicator[] indicators,
            List<StatusEffect> statuses)
        {
            if (indicators == null)
            {
                return;
            }


            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(
                _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            int allowed = Mathf.Min(indicators.Length, fine.MaxStatusIcons);
            for (int i = 0;
                 i < indicators.Length;
                 i++)
            {
                StatusIndicator indicator =
                    indicators[i];


                if (indicator == null)
                {
                    continue;
                }


                ArcaneUiFrameFineTuning.ApplyAlpha(indicator.Root, fine.StatusOpacity);
                if (statuses == null || i >= allowed ||
                    i >= statuses.Count)
                {
                    indicator.Root.SetActive(
                        false);


                    continue;
                }


                StatusEffect status =
                    statuses[i];


                Spell effect =
                    status != null
                        ? status.Effect
                        : null;


                if (effect == null)
                {
                    indicator.Root.SetActive(
                        false);


                    continue;
                }


                Sprite sprite =
                    ResolveSpellSprite(
                        effect);


                indicator.Icon.sprite =
                    sprite;


                indicator.Icon.color =
                    sprite != null
                        ? Color.white
                        : (indicator.Debuff
                            ? new Color(0.34f, 0.08f, 0.08f, 1f)
                            : new Color(0.06f, 0.22f, 0.10f, 1f));


                indicator.Fallback.gameObject.SetActive(
                    sprite == null);


                indicator.Fallback.text =
                    GetStatusFallbackText(
                        effect,
                        indicator.Debuff);


                int seconds =
                    Mathf.CeilToInt(
                        status.Duration);


                indicator.Duration.text =
                    seconds > 99
                        ? "99+"
                        : seconds.ToString();


                indicator.Root.SetActive(
                    true);
            }
        }


        private static string GetStatusFallbackText(
            Spell effect,
            bool debuff)
        {
            if (effect != null &&
                !string.IsNullOrEmpty(effect.SpellName))
            {
                string name =
                    effect.SpellName.Trim();


                if (name.Length > 0)
                {
                    return name.Substring(
                        0,
                        1).ToUpperInvariant();
                }
            }


            return debuff
                ? "D"
                : "B";
        }


        private static void HideStatusIndicators(
            StatusIndicator[] indicators)
        {
            if (indicators == null)
            {
                return;
            }


            for (int i = 0;
                 i < indicators.Length;
                 i++)
            {
                if (indicators[i] != null &&
                    indicators[i].Root != null)
                {
                    indicators[i].Root.SetActive(
                        false);
                }
            }
        }


        private static Sprite ResolveSpellSprite(
            Spell spell)
        {
            if (spell == null)
            {
                return null;
            }


            if (!_spellSpriteFieldResolved)
            {
                _spellSpriteFieldResolved =
                    true;


                FieldInfo[] fields =
                    typeof(Spell).GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);


                string[] preferredNames =
                {
                    "Icon",
                    "SpellIcon",
                    "IconSprite",
                    "SpellSprite",
                    "MyIcon"
                };


                for (int p = 0;
                     p < preferredNames.Length && _spellSpriteField == null;
                     p++)
                {
                    for (int i = 0;
                         i < fields.Length;
                         i++)
                    {
                        if (fields[i].FieldType == typeof(Sprite) &&
                            string.Equals(
                                fields[i].Name,
                                preferredNames[p],
                                StringComparison.OrdinalIgnoreCase))
                        {
                            _spellSpriteField =
                                fields[i];


                            break;
                        }
                    }
                }


                if (_spellSpriteField == null)
                {
                    for (int i = 0;
                         i < fields.Length;
                         i++)
                    {
                        if (fields[i].FieldType == typeof(Sprite))
                        {
                            _spellSpriteField =
                                fields[i];


                            break;
                        }
                    }
                }
            }


            if (_spellSpriteField == null)
            {
                return null;
            }


            try
            {
                return _spellSpriteField.GetValue(
                    spell) as Sprite;
            }
            catch
            {
                return null;
            }
        }


        private sealed class StatusIndicator
        {
            internal readonly GameObject Root;
            internal readonly Image Icon;
            internal readonly Text Fallback;
            internal readonly Text Duration;
            internal readonly bool Debuff;


            internal StatusIndicator(
                GameObject root,
                Image icon,
                Text fallback,
                Text duration,
                bool debuff)
            {
                Root = root;
                Icon = icon;
                Fallback = fallback;
                Duration = duration;
                Debuff = debuff;
            }
        }


        private void ConfigureRaidDrag()
        {
            if (_partyContext ||
                _player ||
                _slot == null)
            {
                return;
            }


            RaidMemberDragHandler drag =
                _root.AddComponent<RaidMemberDragHandler>();


            drag.Initialize(
                _slot);
        }


        // ============================================================
        // INTERACTION
        // ============================================================

        private void ConfigureInteraction()
        {
            UnitFrameInteraction interaction =
                _root.AddComponent<UnitFrameInteraction>();


            if (_player)
            {
                interaction.InitializeForPlayer(
                    _partyContext);
            }
            else if (_groupMember != null)
            {
                interaction.InitializeForGroupMember(
                    _groupMember,
                    true);
            }
            else
            {
                interaction.InitializeForRaidMember(
                    _slot,
                    false);
            }
        }


        // ============================================================
        // ROOT
        // ============================================================

        private GameObject CreateRoot(
            RectTransform parent)
        {
            GameObject root =
                new GameObject(
                    _player
                        ? "UnitFrame_Player"
                        : (_groupMember != null
                            ? "GroupMember"
                            : "RaidMember"));


            root.transform
                .SetParent(
                    parent,
                    false);


            RectTransform rect =
                root.AddComponent<RectTransform>();


            rect.sizeDelta =
                new Vector2(
                    GetFrameWidth(),
                    GetFrameHeight());


            LayoutElement layout =
                root.AddComponent<LayoutElement>();


            layout.preferredWidth =
                GetFrameWidth();


            layout.minWidth =
                GetFrameWidth();


            layout.preferredHeight =
                GetFrameHeight();


            layout.minHeight =
                GetFrameHeight();


            layout.flexibleWidth =
                0f;


            layout.flexibleHeight =
                0f;


            // Thin outer border / missing-health background.
            Image rootImage =
                root.AddComponent<Image>();


            rootImage.color =
                new Color(
                    0.025f,
                    0.03f,
                    0.035f,
                    0.98f);


            return root;
        }


        // ============================================================
        // BACKGROUND
        // ============================================================

        private Image CreateBackground()
        {
            GameObject obj =
                new GameObject(
                    "Background");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                Vector2.zero;


            rect.anchorMax =
                Vector2.one;


            rect.offsetMin =
                new Vector2(
                    1f,
                    1f);


            rect.offsetMax =
                new Vector2(
                    -1f,
                    -1f);


            Image image =
                obj.AddComponent<Image>();


            image.color =
                new Color(
                    0.07f,
                    0.07f,
                    0.075f,
                    1f);


            image.raycastTarget =
                false;


            return image;
        }


        // ============================================================
        // HEALTH FILL
        // ============================================================

        private Image CreateHealthFill()
        {
            GameObject obj =
                new GameObject(
                    "HealthFill");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    0f);


            rect.anchorMax =
                new Vector2(
                    1f,
                    1f);


            rect.pivot =
                new Vector2(
                    0f,
                    0.5f);


            rect.offsetMin =
                new Vector2(
                    2f,
                    2f);


            rect.offsetMax =
                new Vector2(
                    -2f,
                    -2f);


            Image image =
                obj.AddComponent<Image>();


            image.raycastTarget =
                false;


            return image;
        }


        /// <summary>Ward layer above health fill and below labels; no input capture.</summary>
        private Image CreateAbsorbFill()
        {
            GameObject obj = new GameObject("LightbringerAbsorb");
            obj.transform.SetParent(_root.transform, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            Image image = obj.AddComponent<Image>();
            image.color = new Color(1f, .82f, .19f, .43f);
            image.raycastTarget = false;
            obj.SetActive(false);
            return image;
        }

        // ============================================================
        // MANA / RESOURCE BAR
        // ============================================================

        private Image CreateManaBackground()
        {
            GameObject obj =
                new GameObject(
                    "ManaBackground");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    0f);


            rect.anchorMax =
                new Vector2(
                    1f,
                    0f);


            rect.pivot =
                new Vector2(
                    0.5f,
                    0f);


            rect.offsetMin =
                new Vector2(
                    2f,
                    2f);


            rect.offsetMax =
                new Vector2(
                    -2f,
                    7f);


            Image image =
                obj.AddComponent<Image>();


            image.color =
                new Color(
                    0.035f,
                    0.04f,
                    0.055f,
                    1f);


            image.raycastTarget =
                false;


            return image;
        }


        private Image CreateManaFill()
        {
            GameObject obj =
                new GameObject(
                    "ManaFill");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    0f);


            rect.anchorMax =
                new Vector2(
                    1f,
                    0f);


            rect.pivot =
                new Vector2(
                    0f,
                    0f);


            rect.offsetMin =
                new Vector2(
                    2f,
                    2f);


            rect.offsetMax =
                new Vector2(
                    -2f,
                    7f);


            Image image =
                obj.AddComponent<Image>();


            image.color =
                new Color(
                    0.18f,
                    0.42f,
                    0.85f,
                    1f);


            image.raycastTarget =
                false;


            return image;
        }


        // ============================================================
        // AGGRO HIGHLIGHT
        // ============================================================

        private GameObject CreateAggroHighlight(
            out Image top,
            out Image bottom,
            out Image left,
            out Image right)
        {
            GameObject root =
                new GameObject(
                    "AggroHighlight");


            root.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                root.AddComponent<RectTransform>();


            rect.anchorMin =
                Vector2.zero;


            rect.anchorMax =
                Vector2.one;


            rect.offsetMin =
                Vector2.zero;


            rect.offsetMax =
                Vector2.zero;


            top = CreateHighlightEdge(root.transform, "Top", new Color(1f, 0.30f, 0.08f, 1f));
            bottom = CreateHighlightEdge(root.transform, "Bottom", new Color(1f, 0.30f, 0.08f, 1f));
            left = CreateHighlightEdge(root.transform, "Left", new Color(1f, 0.30f, 0.08f, 1f));
            right = CreateHighlightEdge(root.transform, "Right", new Color(1f, 0.30f, 0.08f, 1f));


            root.SetActive(false);


            return root;
        }


        private void RefreshAggroHighlight()
        {
            if (_aggroHighlight == null)
            {
                return;
            }


            // Current-target highlighting has visual priority over aggro.
            bool show =
                GetShowAggroHighlight() &&
                !IsCurrentTarget() &&
                HasEnemyAggro();


            _aggroHighlight.SetActive(show);


            if (!show)
            {
                return;
            }


            float thickness =
                GetAggroHighlightThickness();


            SetTargetEdgeLayout(
                _aggroTop.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -thickness),
                Vector2.zero);


            SetTargetEdgeLayout(
                _aggroBottom.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, thickness));


            SetTargetEdgeLayout(
                _aggroLeft.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(thickness, 0f));


            SetTargetEdgeLayout(
                _aggroRight.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-thickness, 0f),
                Vector2.zero);
        }


        private bool HasEnemyAggro()
        {
            Stats stats =
                GetStats();


            Character unit =
                stats != null
                    ? stats.Myself
                    : null;


            if (unit == null ||
                !unit.Alive ||
                unit.NearbyEnemies == null)
            {
                return false;
            }


            foreach (Character enemy in unit.NearbyEnemies)
            {
                if (enemy == null ||
                    !enemy.Alive ||
                    enemy.MyNPC == null)
                {
                    continue;
                }


                // Erenshor itself treats NPC.CurrentAggroTarget as the
                // authoritative target-of-aggro signal.
                if (enemy.MyNPC.CurrentAggroTarget == unit)
                {
                    return true;
                }
            }


            return false;
        }


        // ============================================================
        // CURRENT TARGET HIGHLIGHT
        // ============================================================

        private GameObject CreateTargetHighlight(
            out Image top,
            out Image bottom,
            out Image left,
            out Image right)
        {
            GameObject root =
                new GameObject(
                    "TargetHighlight");


            root.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                root.AddComponent<RectTransform>();


            rect.anchorMin =
                Vector2.zero;


            rect.anchorMax =
                Vector2.one;


            rect.offsetMin =
                Vector2.zero;


            rect.offsetMax =
                Vector2.zero;


            top =
                CreateTargetEdge(
                    root.transform,
                    "Top");


            bottom =
                CreateTargetEdge(
                    root.transform,
                    "Bottom");


            left =
                CreateTargetEdge(
                    root.transform,
                    "Left");


            right =
                CreateTargetEdge(
                    root.transform,
                    "Right");


            root.SetActive(
                false);


            return root;
        }


        private static Image CreateTargetEdge(
            Transform parent,
            string name)
        {
            return CreateHighlightEdge(
                parent,
                name,
                new Color(
                    1f,
                    0.86f,
                    0.28f,
                    1f));
        }


        private static Image CreateHighlightEdge(
            Transform parent,
            string name,
            Color color)
        {
            GameObject obj =
                new GameObject(
                    name);


            obj.transform.SetParent(
                parent,
                false);


            obj.AddComponent<RectTransform>();


            Image image =
                obj.AddComponent<Image>();


            image.color =
                color;


            image.raycastTarget =
                false;


            return image;
        }


        private void RefreshTargetHighlight()
        {
            if (_targetHighlight == null)
            {
                return;
            }


            bool show =
                GetShowTargetHighlight() &&
                IsCurrentTarget();


            _targetHighlight.SetActive(
                show);


            if (!show)
            {
                return;
            }


            float thickness =
                GetTargetHighlightThickness();


            SetTargetEdgeLayout(
                _targetTop.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -thickness),
                Vector2.zero);


            SetTargetEdgeLayout(
                _targetBottom.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                Vector2.zero,
                new Vector2(0f, thickness));


            SetTargetEdgeLayout(
                _targetLeft.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(thickness, 0f));


            SetTargetEdgeLayout(
                _targetRight.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(-thickness, 0f),
                Vector2.zero);
        }


        private static void SetTargetEdgeLayout(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin =
                anchorMin;


            rect.anchorMax =
                anchorMax;


            rect.offsetMin =
                offsetMin;


            rect.offsetMax =
                offsetMax;
        }


        private bool IsCurrentTarget()
        {
            if (GameData.PlayerControl == null ||
                GameData.PlayerControl.CurrentTarget == null)
            {
                return false;
            }


            Stats stats =
                GetStats();


            return stats != null &&
                   stats.Myself != null &&
                   GameData.PlayerControl.CurrentTarget == stats.Myself;
        }


        // ============================================================
        // NAME
        // ============================================================

        private Text CreateNameText()
        {
            GameObject obj =
                new GameObject(
                    "Name");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                Vector2.zero;


            rect.anchorMax =
                Vector2.one;


            rect.offsetMin =
                new Vector2(
                    5f,
                    4f);


            rect.offsetMax =
                new Vector2(
                    -5f,
                    -3f);


            Text text =
                obj.AddComponent<Text>();


            text.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");


            text.fontSize =
                12;


            text.alignment =
                TextAnchor.UpperLeft;


            text.color =
                Color.white;


            text.raycastTarget =
                false;


            AddOutline(
                obj);


            return text;
        }


        // ============================================================
        // HP PERCENT
        // ============================================================

        private Text CreateHpPercentText()
        {
            GameObject obj =
                new GameObject(
                    "HpPercent");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0.45f,
                    0f);


            rect.anchorMax =
                new Vector2(
                    1f,
                    0.55f);


            rect.offsetMin =
                Vector2.zero;


            rect.offsetMax =
                new Vector2(
                    -5f,
                    0f);


            Text text =
                obj.AddComponent<Text>();


            text.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");


            text.fontSize =
                11;


            text.alignment =
                TextAnchor.LowerRight;


            text.color =
                Color.white;


            text.raycastTarget =
                false;


            AddOutline(
                obj);


            return text;
        }


        // ============================================================
        // RAW HP
        // ============================================================

        private Text CreateRawHealthText()
        {
            GameObject obj =
                new GameObject(
                    "RawHealth");


            obj.transform.SetParent(
                _root.transform,
                false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    0f);


            rect.anchorMax =
                new Vector2(
                    0.70f,
                    0.55f);


            rect.offsetMin =
                new Vector2(
                    5f,
                    0f);


            rect.offsetMax =
                Vector2.zero;


            Text text =
                obj.AddComponent<Text>();


            text.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");


            text.fontSize =
                10;


            text.alignment =
                TextAnchor.LowerLeft;


            text.color =
                new Color(
                    0.90f,
                    0.90f,
                    0.90f,
                    1f);


            text.raycastTarget =
                false;


            AddOutline(
                obj);


            return text;
        }


        // ============================================================
        // REFRESH
        // ============================================================

        internal void Refresh()
        {
            Stats stats =
                GetStats();


            _nameText.text =
                ArcaneUiNameFormatting.Fit(_nameText, GetName(),
                    _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);


            ApplyAppearance();


            ApplyResourceLayout();


            RefreshStatusIndicators(
                stats);


            RefreshAggroHighlight();


            RefreshTargetHighlight();


            RefreshCombatRezButton();


            if (stats == null)
            {
                SetHealth(
                    0,
                    1,
                    null);


                SetMana(
                    0,
                    1);


                return;
            }


            SetHealth(
                stats.CurrentHP,
                stats.CurrentMaxHP,
                stats);


            SetMana(
                stats.CurrentMana,
                stats.GetCurrentMaxMana());
        }


        // ============================================================
        // HEALTH
        // ============================================================

        private void SetHealth(
            int current,
            int maximum,
            Stats stats)
        {
            if (maximum <= 0)
            {
                maximum =
                    1;
            }


            current =
                Mathf.Clamp(
                    current,
                    0,
                    maximum);


            float percentage =
                Mathf.Clamp01(
                    (float)current /
                    maximum);


            // Apply all four directions to the health region *above* the
            // optional mana strip. In particular, vertical directions must
            // use health-band height rather than the entire party/raid card.
            int healthDirection = ArcaneUiBarDirection.Health(_partyContext ?
                ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            float frameWidth = _rootRect.rect.width > 4f
                ? _rootRect.rect.width : GetFrameWidth();
            float frameHeight = _rootRect.rect.height > 4f
                ? _rootRect.rect.height : GetFrameHeight();
            float contentBottom = GetShowManaBars() ? 2f + GetManaStripHeight() : 2f;
            Rect healthBounds = Rect.MinMaxRect(2f,
                Mathf.Min(contentBottom, frameHeight - 2f),
                Mathf.Max(2f, frameWidth - 2f), Mathf.Max(contentBottom, frameHeight - 2f));
            ArcaneUiBarFillGeometry.Apply(_healthFill.rectTransform,
                healthBounds, healthDirection, 0f, percentage);


            // Resolve the final bar colour before rendering the absorb: a
            // Lightbringer's pure-white fill requires a contrasting ward.
            Color healthColour = current <= 0
                ? new Color(.18f, .18f, .18f, .95f) : GetHealthColor();
            _healthFill.color = healthColour;
            LightHealthBarContrast.HealthText(_hpPercentText, healthColour, true);
            LightHealthBarContrast.HealthText(_rawHealthText, healthColour, true);
            FOTFAbsorbOverlay.RenderWithinBounds(_absorbFill, current, maximum,
                FOTFAbsorbOverlay.Read(stats), healthDirection,
                healthBounds, healthColour);

            // --------------------------------------------------------
            // HEALTH TEXT
            // --------------------------------------------------------

            _hpPercentText.gameObject.SetActive(
                GetShowHpPercent());


            _hpPercentText.text =
                Mathf.RoundToInt(
                    percentage * 100f) +
                "%";


            _rawHealthText.gameObject.SetActive(
                GetShowRawHealth());


            _rawHealthText.text = ArcaneUiValueFormatting.Format(current) + " / " +
                ArcaneUiValueFormatting.Format(maximum);


            // --------------------------------------------------------
            // COLORS
            // --------------------------------------------------------

            if (current <= 0)
            {
                _healthFill.color =
                    new Color(
                        0.18f,
                        0.18f,
                        0.18f,
                        0.95f);


                _nameText.color =
                    new Color(
                        0.65f,
                        0.65f,
                        0.65f,
                        1f);


                _hpPercentText.text =
                    "DEAD";


                return;
            }


            _nameText.color =
                Color.white;


            // Health and contrast were already resolved before the ward draw.
        }


        // ============================================================
        // MANA / RESOURCE
        // ============================================================

        private void SetMana(
            int current,
            int maximum)
        {
            bool show =
                GetShowManaBars();


            _manaBackground.gameObject.SetActive(
                show);


            _manaFill.gameObject.SetActive(
                show);


            if (!show)
            {
                return;
            }


            if (maximum <= 0)
            {
                maximum =
                    1;


                current =
                    0;
            }


            current =
                Mathf.Clamp(
                    current,
                    0,
                    maximum);


            float percentage =
                Mathf.Clamp01(
                    (float)current /
                    maximum);


            RectTransform fillRect = _manaFill.rectTransform;
            int manaDirection = ArcaneUiBarDirection.Mana(_partyContext ?
                ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            float strip = GetManaStripHeight();
            // Mana is a horizontal strip in the frame; changing the fill
            // direction changes its occupied portion, not the strip itself.
            float lower = 2f;
            float upper = 2f + strip;
            if (manaDirection == 1) // right -> left
            {
                fillRect.anchorMin = new Vector2(1f - percentage, 0f);
                fillRect.anchorMax = new Vector2(1f, 0f);
                fillRect.offsetMin = new Vector2(2f, lower);
                fillRect.offsetMax = new Vector2(-2f, upper);
            }
            else if (manaDirection == 2 || manaDirection == 3)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(1f, 0f);
                fillRect.offsetMin = new Vector2(2f,
                    manaDirection == 3 ? upper - strip * percentage : lower);
                fillRect.offsetMax = new Vector2(-2f,
                    manaDirection == 2 ? lower + strip * percentage : upper);
            }
            else // left -> right
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(percentage, 0f);
                fillRect.offsetMin = new Vector2(2f, lower);
                fillRect.offsetMax = new Vector2(-2f, upper);
            }

        }


        private float GetManaStripHeight()
        {
            string module = _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            return ArcaneUiBarLayout.ManaHeight(module,
                Mathf.Max(1f, GetFrameHeight() - 4f), GetShowManaBars());
        }

        private void ApplyResourceLayout()
        {
            bool showMana = GetShowManaBars();
            float strip = GetManaStripHeight();
            float contentBottom = showMana ? 2f + strip : 2f;
            _manaBackground.rectTransform.offsetMin = new Vector2(2f, 2f);
            _manaBackground.rectTransform.offsetMax = new Vector2(-2f, 2f + strip);
            _manaFill.rectTransform.offsetMin = new Vector2(2f, 2f);
            _manaFill.rectTransform.offsetMax = new Vector2(-2f, 2f + strip);
            _healthFill.rectTransform.offsetMin =
                new Vector2(
                    2f,
                    contentBottom);


            _nameText.rectTransform.offsetMin =
                new Vector2(
                    5f,
                    showMana ? 9f : 4f);


            _nameText.rectTransform.offsetMax =
                new Vector2(
                    GetShowStatusIcons() ? -69f : -5f,
                    -3f);


            _hpPercentText.rectTransform.offsetMin =
                new Vector2(
                    0f,
                    showMana ? 7f : 0f);


            _rawHealthText.rectTransform.offsetMin =
                new Vector2(
                    5f,
                    showMana ? 7f : 0f);
        }


        // ============================================================
        // APPEARANCE
        // ============================================================

        private void ApplyAppearance()
        {
            // Single shared rendering path for Party and Raid unit frames.
            // Legacy colour/texture/font preferences remain untouched and are
            // restored whenever this module stops inheriting the global theme.
            ArcaneUiFrameStyle style = ArcaneUiStyleService.FrameStyle(
                _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            ArcaneUiFrameRenderer.Apply(style,
                _root.GetComponent<Image>(), _background, _manaBackground,
                _healthFill, _manaFill, _nameText, _hpPercentText, _rawHealthText,
                GetBarTexture(), GetFontName(),
                UnitFrameAppearance.ParseColorOrDefault(GetMissingHealthColorHex(),
                    new Color(.07f, .07f, .075f, 1f)),
                _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            _nameText.fontSize = GetNameFontSize();
            _hpPercentText.fontSize = GetHpPercentFontSize();
            _rawHealthText.fontSize = GetRawHpFontSize();
            ArcaneUiFrameFineTuning fine = ArcaneUiFrameFineTuning.For(
                _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            if (_manaFill != null) ArcaneUiFrameFineTuning.ApplyAlpha(_manaFill.gameObject, fine.ManaOpacity);
            if (_healthFill != null) ArcaneUiFrameFineTuning.ApplyAlpha(_healthFill.gameObject, fine.HealthOpacity);
            string module = _partyContext ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            ArcaneUiTextTagStyle.Apply(_nameText, module, "name", GetNameFontSize());
            ArcaneUiTextTagStyle.Apply(_hpPercentText, module, "percent", GetHpPercentFontSize());
            ArcaneUiTextTagStyle.Apply(_rawHealthText, module, "health", GetRawHpFontSize());
        }


        // ============================================================
        // CLASS COLOR
        // ============================================================

        private Color GetHealthColor()
        {
            // Both Party and Raid members use the same effective palette.
            // The global theme wins over the legacy class setting by default;
            // class colours while themed require an explicit per-module opt-in.
            string module = _partyContext
                ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            Stats stats = GetStats();
            Color classColor = RaidClassColors.GetColorForActor(stats, GetName());
            Color legacyStatic = UnitFrameAppearance.ParseColorOrDefault(
                GetStaticHealthColorHex(), RaidClassColors.DefaultColor);
            return ArcaneUiStyleService.ResolveHealthColor(module,
                classColor, legacyStatic, GetUseClassColors());
        }


        // ============================================================
        // DATA
        // ============================================================

        private Stats GetStats()
        {
            if (_player)
            {
                return GameData.PlayerStats;
            }


            if (_groupMember != null)
            {
                if (_groupMember.MyAvatar != null &&
                    _groupMember.MyAvatar.MyStats != null)
                {
                    return _groupMember.MyAvatar.MyStats;
                }


                return _groupMember.MyStats;
            }


            if (_slot == null)
            {
                return null;
            }


            if (_slot.AssignedAvatar != null &&
                _slot.AssignedAvatar.MyStats != null)
            {
                return _slot
                    .AssignedAvatar
                    .MyStats;
            }


            if (_slot.AssignedSimTracking != null &&
                _slot.AssignedSimTracking.MyStats != null)
            {
                return _slot
                    .AssignedSimTracking
                    .MyStats;
            }


            return null;
        }


        private string GetName()
        {
            if (_player)
            {
                if (GameData.PlayerStats != null &&
                    !string.IsNullOrWhiteSpace(
                        GameData.PlayerStats.MyName))
                {
                    return GameData
                        .PlayerStats
                        .MyName;
                }


                return "Player";
            }


            if (_groupMember != null)
            {
                if (!string.IsNullOrWhiteSpace(
                        _groupMember.SimName))
                {
                    return _groupMember.SimName;
                }


                if (_groupMember.MyStats != null &&
                    !string.IsNullOrWhiteSpace(
                        _groupMember.MyStats.MyName))
                {
                    return _groupMember.MyStats.MyName;
                }


                return "Unknown";
            }


            if (_slot == null)
            {
                return "Empty";
            }


            if (_slot.AssignedSimTracking != null &&
                !string.IsNullOrWhiteSpace(
                    _slot.AssignedSimTracking.SimName))
            {
                return _slot
                    .AssignedSimTracking
                    .SimName;
            }


            if (_slot.AssignedAvatar != null &&
                _slot.AssignedAvatar.MyStats != null &&
                !string.IsNullOrWhiteSpace(
                    _slot.AssignedAvatar.MyStats.MyName))
            {
                return _slot
                    .AssignedAvatar
                    .MyStats
                    .MyName;
            }


            return "Unknown";
        }


        // ============================================================
        // OUTLINE
        // ============================================================

        private static void AddOutline(
            GameObject obj)
        {
            Outline outline =
                obj.AddComponent<Outline>();


            outline.effectColor =
                new Color(
                    0f,
                    0f,
                    0f,
                    0.85f);


            outline.effectDistance =
                new Vector2(
                    1f,
                    -1f);
        }


        // ============================================================
        // SETTINGS CONTEXT
        // ============================================================

        private float GetFrameWidth()
        {
            return _partyContext
                ? PartyFramesSettings.FrameWidth
                : RaidFramesSettings.FrameWidth;
        }

        private float GetFrameHeight()
        {
            return _partyContext
                ? PartyFramesSettings.FrameHeight
                : RaidFramesSettings.FrameHeight;
        }

        private bool GetShowHpPercent()
        {
            return _partyContext
                ? PartyFramesSettings.ShowHpPercent
                : RaidFramesSettings.ShowHpPercent;
        }

        private bool GetShowRawHealth()
        {
            return _partyContext
                ? PartyFramesSettings.ShowRawHealth
                : RaidFramesSettings.ShowRawHealth;
        }

        private bool GetUseClassColors()
        {
            return _partyContext
                ? PartyFramesSettings.UseClassColors
                : RaidFramesSettings.UseClassColors;
        }


        private string GetBarTexture()
        {
            return _partyContext
                ? PartyFramesSettings.BarTexture
                : RaidFramesSettings.BarTexture;
        }


        private string GetStaticHealthColorHex()
        {
            return _partyContext
                ? PartyFramesSettings.StaticHealthColorHex
                : RaidFramesSettings.StaticHealthColorHex;
        }


        private string GetMissingHealthColorHex()
        {
            return _partyContext
                ? PartyFramesSettings.MissingHealthColorHex
                : RaidFramesSettings.MissingHealthColorHex;
        }


        private string GetFontName()
        {
            return _partyContext
                ? PartyFramesSettings.FontName
                : RaidFramesSettings.FontName;
        }


        private int GetNameFontSize()
        {
            return _partyContext
                ? PartyFramesSettings.NameFontSize
                : RaidFramesSettings.NameFontSize;
        }


        private int GetHpPercentFontSize()
        {
            return _partyContext
                ? PartyFramesSettings.HpPercentFontSize
                : RaidFramesSettings.HpPercentFontSize;
        }


        private int GetRawHpFontSize()
        {
            return _partyContext
                ? PartyFramesSettings.RawHpFontSize
                : RaidFramesSettings.RawHpFontSize;
        }


        private bool GetShowStatusIcons()
        {
            return _partyContext
                ? PartyFramesSettings.ShowStatusIcons
                : RaidFramesSettings.ShowStatusIcons;
        }


        private bool GetShowManaBars()
        {
            return _partyContext
                ? PartyFramesSettings.ShowManaBars
                : RaidFramesSettings.ShowManaBars;
        }


        private bool GetShowTargetHighlight()
        {
            return _partyContext
                ? PartyFramesSettings.ShowTargetHighlight
                : RaidFramesSettings.ShowTargetHighlight;
        }


        private float GetTargetHighlightThickness()
        {
            return _partyContext
                ? PartyFramesSettings.TargetHighlightThickness
                : RaidFramesSettings.TargetHighlightThickness;
        }


        private bool GetShowAggroHighlight()
        {
            return _partyContext
                ? PartyFramesSettings.ShowAggroHighlight
                : RaidFramesSettings.ShowAggroHighlight;
        }


        private float GetAggroHighlightThickness()
        {
            return _partyContext
                ? PartyFramesSettings.AggroHighlightThickness
                : RaidFramesSettings.AggroHighlightThickness;
        }


        // ============================================================
        // DESTROY
        // ============================================================

        internal void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(
                    _root);
            }
        }
    }
}