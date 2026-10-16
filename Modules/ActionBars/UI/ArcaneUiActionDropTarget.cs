using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Native;

namespace ArcanesGroupFrames.Modules.ActionBars.UI
{
    /// <summary>
    /// A slot is the only valid destination. Unity's native spellbook may own
    /// the drag gesture and render a floating drag proxy, so OnDrop alone does
    /// not reliably receive the original source. Cache the native UI entry on
    /// mouse down and hit-test the actual destination rect on mouse up.
    /// </summary>
    internal sealed class ArcaneUiActionDropTarget : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
        IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal int Bar;
        internal int Slot;
        internal Action<int, int> OnCleared;
        internal Action<int, int, bool> OnHover;

        private static readonly List<ArcaneUiActionDropTarget> Targets = new List<ArcaneUiActionDropTarget>();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static GameObject _pressedObject;
        private static ArcaneUiActionDropTarget _pressedSlot;
        private static Spell _pressedSpell;
        private static Skill _pressedSkill;
        private static ItemIcon _pressedItem;
        private static int _pressedNative;
        private static Vector2 _pressPosition;
        private static Vector2 _releasePosition;
        private static int _releaseFrame = -1;
        private static int _handledFrame = -1;
        private static bool _pendingRelease;

        internal void Initialize(int bar, int slot) { Bar = bar; Slot = slot; }
        private void OnEnable() { if (!Targets.Contains(this)) Targets.Add(this); }
        private void OnDisable()
        {
            if (OnHover != null) OnHover(Bar, Slot, false);
            Targets.Remove(this);
            if (ReferenceEquals(_pressedSlot, this)) ClearDrag();
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (OnHover != null) OnHover(Bar, Slot, true);
        }
        public void OnPointerExit(PointerEventData data)
        {
            if (OnHover != null) OnHover(Bar, Slot, false);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Left) return;
            RememberSource(gameObject, data.position);
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data == null || data.button != PointerEventData.InputButton.Right) return;
            if (Bar > 0)
            {
                NativeExtraHotkeySlots.Clear(Bar, Slot);
                if (OnCleared != null) OnCleared(Bar, Slot);
                return;
            }
            string message;
            if (!NativeActionBarDiscovery.TryClearPrimarySlot(Slot, out message))
                Plugin.LogWarning("ArcaneUI primary slot clear: " + message);
            if (OnCleared != null) OnCleared(Bar, Slot);
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data != null && data.button == PointerEventData.InputButton.Left)
                RememberSource(gameObject, data.pressPosition);
        }
        public void OnDrag(PointerEventData data) { }
        public void OnEndDrag(PointerEventData data) { }
        public void OnDrop(PointerEventData data)
        {
            if (data == null || Bar <= 0 || !Contains(data.position) ||
                !ReferenceEquals(TargetAt(data.position), this)) return;
            // A real drag is required, never an ordinary click on the slot.
            if ((data.position - data.pressPosition).sqrMagnitude < 144f) return;
            _handledFrame = Time.frameCount;
            string status = "The dragged entry is not a usable action.";
            bool assigned = TryAssign(data.pointerDrag, out status);
            if (!assigned && _pressedObject != null && _pressedObject != data.pointerDrag)
                assigned = TryAssign(_pressedObject, out status);
            if (!assigned && _pressedSpell != null)
                assigned = NativeExtraHotkeySlots.AssignFromDrag(Bar, Slot, "Spell", _pressedSpell);
            if (!assigned && _pressedSkill != null)
                assigned = NativeExtraHotkeySlots.AssignFromDrag(Bar, Slot, "Skill", _pressedSkill);
            if (!assigned && _pressedItem != null)
                assigned = NativeExtraHotkeySlots.AssignFromDrag(Bar, Slot, "Item", _pressedItem);
            if (!assigned && _pressedNative > 0)
                assigned = ActionBarIndependentAssignments.CaptureNative(Bar, Slot, _pressedNative, out status);
            if (!assigned)
                Plugin.LogWarning("ArcaneUI drop onto Bar" + (Bar + 1) + " Slot" + Slot + ": " + status);
            ClearDrag();
        }

        // Called once per action bar frame, not from every slot. Performs a UI
        // raycast only on initial pointer-down, not in the hot HUD render loop.
        internal static void PollPointerDrop(bool blocked)
        {
            if (blocked)
            {
                ClearDrag();
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 point = Input.mousePosition;
                GameObject source = null;
                RaycastAt(point);
                for (int i = 0; i < Hits.Count; ++i)
                {
                    GameObject candidate = Hits[i].gameObject;
                    if (candidate == null) continue;
                    ArcaneUiActionDropTarget slot = candidate.GetComponentInParent<ArcaneUiActionDropTarget>();
                    if (slot != null) { source = slot.gameObject; break; }
                    Spell spell;
                    Skill skill;
                    ItemIcon item;
                    int native;
                    if (NativeActionBarDiscovery.TryGetSpellbookEntry(candidate, out spell) ||
                        NativeActionBarDiscovery.TryGetSkillbookEntry(candidate, out skill) ||
                        NativeActionBarDiscovery.TryGetInventoryDrag(candidate, out item) ||
                        NativeActionBarDiscovery.IsLivePrimarySlotObject(candidate, out native))
                    { source = candidate; break; }
                }
                if (source != null) RememberSource(source, point);
                else ClearDrag();
            }
            if (Input.GetMouseButtonUp(0) &&
                (_pressedObject != null || _pressedSpell != null || _pressedSkill != null ||
                 _pressedItem != null || _pressedNative > 0))
            {
                _releasePosition = Input.mousePosition;
                _releaseFrame = Time.frameCount;
                _pendingRelease = true;
            }
            // Allow Unity's EventSystem to deliver OnDrop during the release
            // frame before falling back to the pointer's precise slot rect.
            if (_pendingRelease && Time.frameCount > _releaseFrame)
            {
                _pendingRelease = false;
                if (_handledFrame == _releaseFrame) { ClearDrag(); return; }
                if ((_releasePosition - _pressPosition).sqrMagnitude < 144f)
                { ClearDrag(); return; }
                ArcaneUiActionDropTarget target = TargetAt(_releasePosition);
                if (target != null)
                {
                    string status = "The dragged entry is not a usable action.";
                    bool success = target.TryAssign(_pressedObject, out status);
                    if (!success && _pressedSpell != null)
                        success = NativeExtraHotkeySlots.AssignFromDrag(target.Bar, target.Slot, "Spell", _pressedSpell);
                    if (!success && _pressedSkill != null)
                        success = NativeExtraHotkeySlots.AssignFromDrag(target.Bar, target.Slot, "Skill", _pressedSkill);
                    if (!success && _pressedItem != null)
                        success = NativeExtraHotkeySlots.AssignFromDrag(target.Bar, target.Slot, "Item", _pressedItem);
                    if (!success && _pressedNative > 0)
                        success = ActionBarIndependentAssignments.CaptureNative(
                            target.Bar, target.Slot, _pressedNative, out status);
                    if (!success)
                        Plugin.LogWarning("ArcaneUI pointer drop Bar" + (target.Bar + 1) +
                            " Slot" + target.Slot + ": " + status);
                }
                ClearDrag();
            }
        }
        private static void ClearDrag()
        {
            _pressedObject = null;
            _pressedSlot = null;
            _pressedSpell = null;
            _pressedSkill = null;
            _pressedItem = null;
            _pressedNative = 0;
            _pendingRelease = false;
        }
        private static void RememberSource(GameObject source, Vector2 at)
        {
            if (source == null) return;
            Spell spell;
            Skill skill;
            ItemIcon item;
            int native;
            ArcaneUiActionDropTarget target = source.GetComponentInParent<ArcaneUiActionDropTarget>();
            bool isSpell = NativeActionBarDiscovery.TryGetSpellbookEntry(source, out spell);
            bool isSkill = NativeActionBarDiscovery.TryGetSkillbookEntry(source, out skill);
            bool isItem = NativeActionBarDiscovery.TryGetInventoryDrag(source, out item);
            bool isNative = NativeActionBarDiscovery.IsLivePrimarySlotObject(source, out native);
            if (target == null && !isSpell && !isSkill && !isItem && !isNative) return;
            _pressedObject = source;
            _pressedSlot = target;
            _pressedSpell = isSpell ? spell : null;
            _pressedSkill = isSkill ? skill : null;
            _pressedItem = isItem ? item : null;
            _pressedNative = isNative ? native : 0;
            _pressPosition = at;
            _pendingRelease = false;
        }
        private static void RaycastAt(Vector2 point)
        {
            Hits.Clear();
            EventSystem system = EventSystem.current;
            if (system == null) return;
            var data = new PointerEventData(system) { position = point };
            system.RaycastAll(data, Hits);
        }
        private bool Contains(Vector2 point)
        {
            if (Bar <= 0 || !isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
            var rect = transform as RectTransform;
            if (rect == null) return false;
            // ArcaneUI's action bars use a ScreenSpaceOverlay canvas.
            var canvas = GetComponentInParent<UnityEngine.Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            CanvasGroup group = GetComponentInParent<CanvasGroup>();
            if (group != null && !group.blocksRaycasts) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, point, camera);
        }
        private static ArcaneUiActionDropTarget TargetAt(Vector2 point)
        {
            RaycastAt(point);
            // First prefer the UI hit actually under the pointer: nested bars
            // and overlapping windows use EventSystem's normal hit order.
            for (int i = 0; i < Hits.Count; ++i)
            {
                var hit = Hits[i].gameObject;
                if (hit == null) continue;
                var slot = hit.GetComponentInParent<ArcaneUiActionDropTarget>();
                if (slot != null && slot.Contains(point)) return slot;
            }
            // Native dragging may place a raycastable ghost over the cursor.
            // Fall back to the exact slot rectangle instead of the ghost's UI.
            for (int i = Targets.Count - 1; i >= 0; --i)
                if (Targets[i] != null && Targets[i].Contains(point)) return Targets[i];
            return null;
        }
        private bool TryAssign(GameObject sourceObject, out string status)
        {
            status = "The drag source did not expose a valid spell, skill or native hotkey.";
            if (Bar <= 0 || sourceObject == null) return false;
            ArcaneUiActionDropTarget source = sourceObject.GetComponentInParent<ArcaneUiActionDropTarget>();
            if (source != null)
            {
                if (source.Bar == Bar && source.Slot == Slot) return false;
                if (source.Bar == 0)
                    return ActionBarIndependentAssignments.CaptureNative(Bar, Slot, source.Slot, out status);
                if (ActionBarIndependentAssignments.Get(source.Bar, source.Slot) != null)
                    return (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                        ? ActionBarIndependentAssignments.Copy(source.Bar, source.Slot, Bar, Slot, out status)
                        : ActionBarIndependentAssignments.Move(source.Bar, source.Slot, Bar, Slot, out status);
                int linked = ActionBarPlanSettings.SourceSlot(source.Bar, source.Slot);
                return linked > 0 && ActionBarIndependentAssignments.CaptureNative(Bar, Slot, linked, out status);
            }
            int native;
            if (NativeActionBarDiscovery.IsLivePrimarySlotObject(sourceObject, out native))
                return ActionBarIndependentAssignments.CaptureNative(Bar, Slot, native, out status);
            Spell spell;
            if (NativeActionBarDiscovery.TryGetSpellbookEntry(sourceObject, out spell))
                return NativeExtraHotkeySlots.AssignFromDrag(Bar, Slot, "Spell", spell);
            Skill skill;
            if (NativeActionBarDiscovery.TryGetSkillbookEntry(sourceObject, out skill))
                return NativeExtraHotkeySlots.AssignFromDrag(Bar, Slot, "Skill", skill);
            ItemIcon item;
            if (NativeActionBarDiscovery.TryGetInventoryDrag(sourceObject, out item))
                return NativeExtraHotkeySlots.AssignFromDrag(Bar, Slot, "Item", item);
            return false;
        }
    }
}
