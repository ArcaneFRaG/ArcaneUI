using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.Nameplates.Configuration;

namespace ArcanesGroupFrames.Modules.Nameplates.Native
{
    /// <summary>
    /// Never disables a character, renderer hierarchy, GameObject or game script.
    /// Native hiding requires an explicitly chosen child path relative to a
    /// Character and only touches text/Unity UI components within that child.
    /// Original component values are restored on disable, scene change and exit.
    /// </summary>
    internal static class NativeOverheadNameplateBridge
    {
        private struct HiddenGraphic { internal Graphic Component; internal bool WasEnabled; }
        private struct HiddenRenderer { internal Renderer Component; internal bool WasEnabled; }
        private static readonly Dictionary<int, List<HiddenGraphic>> HiddenGraphics = new Dictionary<int, List<HiddenGraphic>>();
        private static readonly Dictionary<int, List<HiddenRenderer>> HiddenRenderers = new Dictionary<int, List<HiddenRenderer>>();
        private static readonly Dictionary<int, List<Transform>> HiddenRoots = new Dictionary<int, List<Transform>>();
        private static readonly Dictionary<int, float> NextActorScan = new Dictionary<int, float>();
        private static readonly List<string> Candidates = new List<string>();
        // Standalone native NamePlate(Clone) objects are sometimes spawned at
        // the scene root rather than under Character. Associate these ONLY by
        // matching explicit displayed NPC names, never by proximity alone.
        private static readonly Dictionary<string, List<Transform>> StandaloneByName =
            new Dictionary<string, List<Transform>>(StringComparer.OrdinalIgnoreCase);
        private static float NextStandaloneScan;
        private static Character _candidateOwner;
        private static int _candidateIndex;
        private static string _lastPath = "";
        private static string _cloneDiagnostics = "Not scanned yet. Target an NPC and click Scan Target Nameplate.";
        internal static string CloneDiagnostics { get { return _cloneDiagnostics; } }
        internal static string Status
        {
            get
            {
                if (!NameplateSettings.HideNative) return "Native nameplates are not hidden.";
                return "NPC native labels hidden: " + HiddenRoots.Count +
                    "; automatic owner/name matching active";
            }
        }
        internal static string SelectedCandidate { get { return Candidates.Count == 0 ? "No candidates — scan with an NPC targeted" : Candidates[_candidateIndex]; } }
        internal static int CandidateCount { get { return Candidates.Count; } }
        internal static void NextCandidate()
        {
            if (Candidates.Count > 0) _candidateIndex = (_candidateIndex + 1) % Candidates.Count;
        }
        internal static void ScanTarget()
        {
            Candidates.Clear(); _candidateIndex = 0;
            _candidateOwner = GameData.PlayerControl != null ? GameData.PlayerControl.CurrentTarget : null;
            if (_candidateOwner == null)
            {
                Plugin.LogWarning("ArcaneUI nameplates: target an NPC or SimPlayer before scanning.");
                return;
            }
            Transform root = _candidateOwner.transform;
            ScanCloneObjects(root);
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform node in all)
            {
                if (node == null || node == root) continue;
                string token = node.name.ToLowerInvariant();
                bool named = token.Contains("nameplate") || token.Contains("overhead") ||
                    token.Contains("name") || token.Contains("nametag") || token.Contains("healthbar");
                if (!named) continue;
                // Reject unrelated character parts. A potential nameplate needs
                // some actual text/UI in this subtree.
                bool hasVisual = node.GetComponentInChildren<Text>(true) != null ||
                    node.GetComponentInChildren<TextMesh>(true) != null ||
                    node.GetComponentInChildren<CanvasRenderer>(true) != null ||
                    HasTmpText(node) ||
                    (string.Equals(node.name, "NamePlate(Clone)", StringComparison.OrdinalIgnoreCase) &&
                     node.GetComponentInChildren<SpriteRenderer>(true) != null);
                if (!hasVisual) continue;
                string path = Relative(root, node);
                if (!string.IsNullOrEmpty(path) && !Candidates.Contains(path)) Candidates.Add(path);
                if (Candidates.Count >= 40) break;
            }
            Plugin.LogInfo("ArcaneUI nameplates: found " + Candidates.Count + " candidate native children on " + _candidateOwner.name + ". Discovery only; no native objects modified.");
            foreach (string entry in Candidates) Plugin.LogInfo("ArcaneUI overhead candidate: " + entry);
        }
        // Discovery only; standalone native nameplate clones can be outside
        // Character.transform. Never disable a spatially nearby object based
        // on distance or name alone: another actor may own that clone.
        private static void ScanCloneObjects(Transform target)
        {
            int total = 0;
            int underTarget = 0;
            Transform closest = null;
            float closestSq = float.MaxValue;
            foreach (Transform node in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (node == null || !string.Equals(node.name, "NamePlate(Clone)", StringComparison.OrdinalIgnoreCase))
                    continue;
                total++;
                bool nested = node == target || node.IsChildOf(target);
                if (nested) underTarget++;
                float distanceSq = (node.position - target.position).sqrMagnitude;
                if (distanceSq < closestSq) { closest = node; closestSq = distanceSq; }
            }
            string nearest = closest != null ? HierarchyPath(closest) : "none";
            _cloneDiagnostics = "Scene NamePlate(Clone): " + total + "; under target: " + underTarget +
                "; nearest hierarchy: " + nearest +
                ". Only verified child paths are eligible for suppression.";
            Plugin.LogInfo("ArcaneUI nameplates: " + _cloneDiagnostics);
        }
        private static string HierarchyPath(Transform node)
        {
            var names = new List<string>();
            for (Transform current = node; current != null && names.Count < 16; current = current.parent)
                names.Add(current.name);
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static bool HasTmpText(Transform root)
        {
            var behaviours = root.GetComponentsInChildren<Behaviour>(true);
            foreach (Behaviour component in behaviours)
                if (component != null && component.GetType().Name.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
        private static string Relative(Transform root, Transform leaf)
        {
            var parts = new List<string>();
            for (Transform node = leaf; node != null && node != root; node = node.parent) parts.Add(node.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
        internal static void UseCandidate()
        {
            if (Candidates.Count == 0) return;
            NameplateSettings.SetNativeRelativePath(Candidates[_candidateIndex]);
            Restore();
        }
        // Restore only actors that have left the discovery set; do not reset
        // every visible native label during the 1.25-second nameplate scan.
        internal static void RestoreActorsNotIn(IList<Character> visible, IList<Character> filtered)
        {
            if (HiddenRoots.Count == 0) return;
            var retained = new HashSet<int>();
            if (visible != null)
                foreach (Character actor in visible)
                    if (actor != null) retained.Add(actor.GetInstanceID());
            if (filtered != null)
                foreach (Character actor in filtered)
                    if (actor != null) retained.Add(actor.GetInstanceID());
            var expired = new List<int>();
            foreach (int key in HiddenRoots.Keys)
                if (!retained.Contains(key) || HiddenRoots[key] == null || HiddenRoots[key].Count == 0) expired.Add(key);
            foreach (int key in expired) RestoreActor(key);
        }

        private static void RestoreActor(int key)
        {
            List<HiddenGraphic> graphics;
            if (HiddenGraphics.TryGetValue(key, out graphics))
                foreach (HiddenGraphic item in graphics)
                    if (item.Component != null) item.Component.enabled = item.WasEnabled;
            List<HiddenRenderer> renderers;
            if (HiddenRenderers.TryGetValue(key, out renderers))
                foreach (HiddenRenderer item in renderers)
                    if (item.Component != null) item.Component.enabled = item.WasEnabled;
            HiddenGraphics.Remove(key);
            HiddenRenderers.Remove(key);
            HiddenRoots.Remove(key);
            NextActorScan.Remove(key);
        }

        // A label is hidden only when its ownership can be established from
        // the Character hierarchy. Never suppress global world objects by
        // proximity; multiple NPCs can stand at the same position.
        private static bool IsOverheadName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            return n.Contains("nameplate") || n.Contains("nametag") ||
                n.Contains("overhead") || n.Contains("nameboard") ||
                n == "npcname" || n == "nametext" || n == "namelabel";
        }

        private static string ReadTmpText(Behaviour component)
        {
            if (component == null ||
                component.GetType().Name.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) < 0)
                return "";
            try
            {
                var property = component.GetType().GetProperty("text");
                return property != null ? property.GetValue(component, null) as string ?? "" : "";
            }
            catch { return ""; }
        }

        private static bool IsActorName(string content, Character actor)
        {
            if (string.IsNullOrEmpty(content) || actor == null) return false;
            string name = actor.MyNPC != null ? actor.MyNPC.NPCName : null;
            if (string.IsNullOrEmpty(name) && actor.MyStats != null) name = actor.MyStats.MyName;
            return !string.IsNullOrEmpty(name) &&
                string.Equals(content.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string ActorDisplayName(Character actor)
        {
            if (actor == null) return "";
            string name = actor.MyNPC != null ? actor.MyNPC.NPCName : null;
            if (string.IsNullOrEmpty(name) && actor.MyStats != null) name = actor.MyStats.MyName;
            return name == null ? "" : name.Trim();
        }

        private static void RefreshStandaloneLookup()
        {
            if (Time.unscaledTime < NextStandaloneScan) return;
            NextStandaloneScan = Time.unscaledTime + 2.5f;
            StandaloneByName.Clear();
            foreach (Transform node in UnityEngine.Object.FindObjectsOfType<Transform>())
            {
                if (node == null ||
                    !string.Equals(node.name, "NamePlate(Clone)", StringComparison.OrdinalIgnoreCase))
                    continue;
                // Already parented overheads use the faster actor-local path.
                if (node.GetComponentInParent<Character>() != null) continue;
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (TextMesh label in node.GetComponentsInChildren<TextMesh>(true))
                    if (label != null && !string.IsNullOrEmpty(label.text)) names.Add(label.text.Trim());
                foreach (Text label in node.GetComponentsInChildren<Text>(true))
                    if (label != null && !string.IsNullOrEmpty(label.text)) names.Add(label.text.Trim());
                foreach (Behaviour component in node.GetComponentsInChildren<Behaviour>(true))
                {
                    string text = ReadTmpText(component);
                    if (!string.IsNullOrEmpty(text)) names.Add(text.Trim());
                }
                foreach (string name in names)
                {
                    List<Transform> items;
                    if (!StandaloneByName.TryGetValue(name, out items))
                        StandaloneByName[name] = items = new List<Transform>();
                    items.Add(node);
                }
            }
        }

        private static List<Transform> FindOwnedOverheads(Character actor)
        {
            var result = new List<Transform>();
            if (actor == null) return result;
            Transform owner = actor.transform;
            string selectedPath = NameplateSettings.NativeRelativePath;
            if (!string.IsNullOrEmpty(selectedPath))
            {
                Transform selected = owner.Find(selectedPath);
                if (selected != null) result.Add(selected);
            }
            Transform[] children = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 1; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || !IsOverheadName(child.name)) continue;
                if (child.GetComponentInChildren<TextMesh>(true) == null &&
                    child.GetComponentInChildren<Graphic>(true) == null &&
                    !HasTmpText(child) &&
                    child.GetComponentInChildren<SpriteRenderer>(true) == null) continue;
                if (!result.Contains(child)) result.Add(child);
                if (result.Count >= 16) break;
            }
            // Some FOTF/vendor prefabs attach native NamePlate directly to
            // the NPC root, with an ordinary TextMesh as its visual child.
            // Suppress ONLY text that exactly identifies this NPC, never the
            // renderer on the character root or an arbitrary named child.
            if (result.Count == 0)
            {
                foreach (TextMesh text in actor.GetComponentsInChildren<TextMesh>(true))
                    if (text != null && IsActorName(text.text, actor) && !result.Contains(text.transform))
                        result.Add(text.transform);
                foreach (Behaviour b in actor.GetComponentsInChildren<Behaviour>(true))
                    if (b != null && IsActorName(ReadTmpText(b), actor) && !result.Contains(b.transform))
                        result.Add(b.transform);
            }
            // A standalone clone is eligible only when a unique native text
            // exactly matches a uniquely named active Character. This avoids
            // hiding another Guard/Vendor's label when NPC names repeat.
            if (result.Count == 0)
            {
                string name = ActorDisplayName(actor);
                if (!string.IsNullOrEmpty(name))
                {
                    RefreshStandaloneLookup();
                    List<Transform> clones;
                    if (StandaloneByName.TryGetValue(name, out clones) && clones.Count == 1)
                    {
                        int owners = 0;
                        foreach (Character possible in UnityEngine.Object.FindObjectsOfType<Character>())
                            if (possible != null && possible.gameObject.activeInHierarchy &&
                                string.Equals(ActorDisplayName(possible), name,
                                    StringComparison.OrdinalIgnoreCase)) owners++;
                        if (owners == 1 && clones[0] != null) result.Add(clones[0]);
                    }
                }
            }
            return result;
        }

        private static void HideOwnedVisuals(Character character, List<Transform> roots)
        {
            int id = character.GetInstanceID();
            var graphics = new List<HiddenGraphic>();
            var renderers = new List<HiddenRenderer>();
            var graphicIds = new HashSet<int>();
            var rendererIds = new HashSet<int>();
            foreach (Transform root in roots)
            {
                if (root == null || !root.gameObject.scene.IsValid()) continue;
                foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
                {
                    if (g == null || !graphicIds.Add(g.GetInstanceID())) continue;
                    graphics.Add(new HiddenGraphic { Component = g, WasEnabled = g.enabled });
                    g.enabled = false;
                }
                foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
                    if (text != null) HideRenderer(text.GetComponent<Renderer>(), renderers, rendererIds);
                foreach (Behaviour component in root.GetComponentsInChildren<Behaviour>(true))
                    if (component != null && component.GetType().Name.IndexOf("TextMeshPro", StringComparison.OrdinalIgnoreCase) >= 0)
                        HideRenderer(component.GetComponent<Renderer>(), renderers, rendererIds);
                if (root.name.IndexOf("NamePlate", StringComparison.OrdinalIgnoreCase) >= 0)
                    foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                        HideRenderer(sprite, renderers, rendererIds);
            }
            if (graphics.Count == 0 && renderers.Count == 0) return;
            HiddenRoots[id] = roots;
            HiddenGraphics[id] = graphics;
            HiddenRenderers[id] = renderers;
        }

        private static void HideRenderer(Renderer renderer, List<HiddenRenderer> list, HashSet<int> ids)
        {
            if (renderer == null || !ids.Add(renderer.GetInstanceID())) return;
            list.Add(new HiddenRenderer { Component = renderer, WasEnabled = renderer.enabled });
            renderer.enabled = false;
        }

        internal static void Apply(Character character)
        {
            if (character == null) return;
            // The native SimPlayer name and guild are independently useful even
            // when the custom SimPlayer health nameplate is disabled. Restore
            // anything this bridge hid earlier, and never suppress it again
            // while ShowSims is false (including via future call sites).
            if (character.MyNPC != null && character.MyNPC.SimPlayer && !NameplateSettings.ShowSims)
            {
                RestoreActor(character.GetInstanceID());
                return;
            }
            if (!NameplateSettings.Enabled || !NameplateSettings.HideNative) return;
            string path = NameplateSettings.NativeRelativePath;
            if (_lastPath != path) { Restore(); _lastPath = path; }
            int id = character.GetInstanceID();
            List<Transform> roots;
            if (HiddenRoots.TryGetValue(id, out roots))
            {
                if (Time.unscaledTime < NextScanFor(id))
                {
                    List<HiddenGraphic> graphics;
                    if (HiddenGraphics.TryGetValue(id, out graphics))
                        foreach (HiddenGraphic item in graphics)
                            if (item.Component != null) item.Component.enabled = false;
                    List<HiddenRenderer> renderers;
                    if (HiddenRenderers.TryGetValue(id, out renderers))
                        foreach (HiddenRenderer item in renderers)
                            if (item.Component != null) item.Component.enabled = false;
                    return;
                }
                RestoreActor(id);
            }
            if (Time.unscaledTime < NextScanFor(id)) return;
            NextActorScan[id] = Time.unscaledTime + 1.0f;
            roots = FindOwnedOverheads(character);
            if (roots.Count > 0) HideOwnedVisuals(character, roots);
        }

        private static float NextScanFor(int id)
        {
            float until;
            return NextActorScan.TryGetValue(id, out until) ? until : 0f;
        }
        internal static bool HasHiddenVisuals { get { return HiddenRoots.Count > 0; } }
        internal static void Restore()
        {
            foreach (var group in HiddenGraphics.Values)
                foreach (HiddenGraphic item in group)
                    if (item.Component != null) item.Component.enabled = item.WasEnabled;
            foreach (var group in HiddenRenderers.Values)
                foreach (HiddenRenderer item in group)
                    if (item.Component != null) item.Component.enabled = item.WasEnabled;
            HiddenGraphics.Clear(); HiddenRenderers.Clear(); HiddenRoots.Clear(); NextActorScan.Clear();
            StandaloneByName.Clear(); NextStandaloneScan = 0f;
            _lastPath = NameplateSettings.NativeRelativePath;
        }
    }
}
