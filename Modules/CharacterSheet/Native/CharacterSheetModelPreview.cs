using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Native
{
    /// <summary>
    /// An isolated, visual-only equipment/model preview. A SkinnedMeshRenderer
    /// cannot safely be copied with its original bones still in the world
    /// hierarchy: its bounds and matrices frequently disappear from the
    /// preview camera. Instead each pose is baked into a local Mesh and rendered
    /// by a MeshFilter on the preview layer. No gameplay scripts are cloned.
    /// </summary>
    internal sealed class CharacterSheetModelPreview
    {
        private const int PreviewLayer = 30;
        // Keep mesh-only proxies away from the game world so a camera with a
        // broad culling mask cannot show the preview objects beside the player.
        private static readonly Vector3 PreviewOrigin = new Vector3(4096f, -4096f, 4096f);
        private sealed class Proxy
        {
            internal Renderer Source;
            internal GameObject Copy;
            internal SkinnedMeshRenderer Skin;
            internal Mesh Baked;
            internal Material[] PreviewMaterials;
        }
        private readonly List<Proxy> _proxies = new List<Proxy>();
        private readonly RawImage _image;
        private RenderTexture _texture;
        private GameObject _cameraObject;
        private Camera _camera;
        private Light _light;
        private Transform _actor;
        private Animator _rigAnimator;
        private Transform _rigHead;
        private Transform _rigHips;
        private float _nextScan;
        private float _nextRender;
        private bool _loggedUnavailable;
        private int _lastVisible = -1;
        private string _lastFailure = "";
        private Transform _lastFailureActor;
        private bool _loggedMissingPreviewShader;
        private int _lastCameraVisible = -1;

        internal CharacterSheetModelPreview(RawImage image)
        {
            _image = image;
            if (_image == null) return;
            _texture = new RenderTexture(512, 768, 24, RenderTextureFormat.ARGB32);
            _texture.name = "ArcaneUI_CharacterPreview_RT";
            _texture.useMipMap = false;
            _texture.autoGenerateMips = false;
            _texture.Create();
            _image.texture = _texture;
            _image.color = Color.white;
            _cameraObject = new GameObject("ArcaneUI_CharacterPreviewCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            // An opaque preview background prevents the entire RawImage from
            // disappearing if Erenshor's native UI uses nonstandard alpha blending.
            _camera.backgroundColor = new Color(.055f, .085f, .12f, 1f);
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.targetTexture = _texture;
            _camera.nearClipPlane = .03f;
            _camera.farClipPlane = 30f;
            // Orthographic framing is stable for segmented Synty avatars and
            // avoids perspective cropping in the narrow native model column.
            _camera.orthographic = true;
            _camera.orthographicSize = 1.1f;
            _camera.allowHDR = false;
            var lightObject = new GameObject("ArcaneUI_CharacterPreviewLight");
            lightObject.transform.SetParent(_cameraObject.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35f, -25f, 0f);
            _light = lightObject.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.15f;
            _light.cullingMask = 1 << PreviewLayer;
            _light.shadows = LightShadows.None;
        }

        private static bool HasMeshes(Transform root)
        {
            if (root == null) return false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer && ((SkinnedMeshRenderer)r).sharedMesh != null) return true;
                MeshRenderer m = r as MeshRenderer;
                if (m != null && m.GetComponent<MeshFilter>() != null &&
                    m.GetComponent<MeshFilter>().sharedMesh != null) return true;
            }
            return false;
        }
        private static int AvatarEvidence(Transform root)
        {
            if (root == null) return 0;
            int score = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                MeshRenderer mesh = renderer as MeshRenderer;
                if (skin == null && (mesh == null || mesh.GetComponent<MeshFilter>() == null)) continue;
                score += BodyNamed(renderer) ? 5 : 1;
                if (skin != null && skin.bones != null && skin.bones.Length > 0) score += 2;
            }
            return score;
        }
        private static bool TryHumanoidRig(Animator animator, out Transform hips, out Transform head)
        {
            hips = null;
            head = null;
            if (animator == null || !animator.isActiveAndEnabled) return false;
            try
            {
                if (!animator.isHuman || animator.avatar == null) return false;
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                return hips != null && head != null;
            }
            catch (Exception) { return false; }
        }
        private Transform FindActor()
        {
            _rigAnimator = null;
            _rigHips = null;
            _rigHead = null;
            // Prefer a rig that belongs to the actual local player. The old
            // renderer-count score frequently selected a weapon/FX hierarchy.
            var roots = new List<Transform>();
            Stats stats = GameData.PlayerStats;
            if (stats != null && stats.Myself != null)
            {
                Component actor = stats.Myself as Component;
                if (actor != null) roots.Add(actor.transform);
            }
            if (GameData.PlayerControl != null)
            {
                Component controller = GameData.PlayerControl as Component;
                if (controller != null && !roots.Contains(controller.transform))
                    roots.Add(controller.transform);
                if (GameData.PlayerControl.Myself != null)
                {
                    Component actor = GameData.PlayerControl.Myself as Component;
                    if (actor != null && !roots.Contains(actor.transform))
                        roots.Add(actor.transform);
                }
            }
            Transform bestRoot = null;
            int bestScore = 0;
            for (int i = 0; i < roots.Count; i++)
            {
                Transform root = roots[i];
                if (root == null) continue;
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    Transform hips, head;
                    if (!TryHumanoidRig(animator, out hips, out head)) continue;
                    int candidateScore = 500 + AvatarEvidence(animator.transform);
                    if (candidateScore <= bestScore) continue;
                    bestScore = candidateScore;
                    bestRoot = animator.transform;
                    _rigAnimator = animator;
                    _rigHips = hips;
                    _rigHead = head;
                }
            }
            if (bestRoot != null) return bestRoot;
            // Unity generic rigs/segmented Synty prefabs need not have a
            // humanoid Avatar. Keep them eligible, but only if body geometry
            // later passes the anatomical grouping checks.
            for (int i = 0; i < roots.Count; i++)
            {
                Transform root = roots[i];
                if (root == null) continue;
                int score = AvatarEvidence(root);
                if (score > bestScore) { bestRoot = root; bestScore = score; }
            }
            return bestScore > 0 ? bestRoot : null;
        }
        private bool ConnectedToRig(SkinnedMeshRenderer skin)
        {
            if (skin == null || _rigHips == null) return false;
            if (skin.rootBone != null &&
                (skin.rootBone == _rigHips || skin.rootBone.IsChildOf(_rigHips))) return true;
            Transform[] bones = skin.bones;
            if (bones == null) return false;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] == _rigHips || bones[i] == _rigHead ||
                    (bones[i] != null && bones[i].IsChildOf(_rigHips)))
                    return true;
            return false;
        }

        private void ClearProxies()
        {
            for (int i = 0; i < _proxies.Count; i++)
            {
                Proxy p = _proxies[i];
                if (p.Copy != null) UnityEngine.Object.Destroy(p.Copy);
                if (p.Baked != null) UnityEngine.Object.Destroy(p.Baked);
                if (p.PreviewMaterials != null)
                    foreach (Material m in p.PreviewMaterials)
                        if (m != null) UnityEngine.Object.Destroy(m);
            }
            _proxies.Clear();
        }

        // Native shaders can depend on environment keywords, lighting and
        // per-object property blocks. Their materials often draw transparent
        // in an isolated RenderTexture even when the mesh is valid. Build
        // visual-only, unlit materials; never modify the source renderer.
        private Material[] BuildPreviewMaterials(Renderer source)
        {
            Material[] originals = source.sharedMaterials;
            int count = originals != null && originals.Length > 0 ? originals.Length : 1;
            Material[] materials = new Material[count];
            Shader textured = Shader.Find("Unlit/Texture");
            Shader flat = Shader.Find("Unlit/Color");
            Shader sprite = Shader.Find("Sprites/Default");
            Shader standard = Shader.Find("Standard");
            for (int i = 0; i < count; i++)
            {
                Material original = originals != null && i < originals.Length ? originals[i] : null;
                Texture texture = null;
                if (original != null)
                {
                    try
                    {
                        if (original.HasProperty("_MainTex")) texture = original.GetTexture("_MainTex");
                        if (texture == null && original.HasProperty("_BaseMap"))
                            texture = original.GetTexture("_BaseMap");
                    }
                    catch (Exception) { }
                }
                Shader selected = texture != null ? (textured != null ? textured : sprite) :
                    (flat != null ? flat : sprite);
                if (selected == null) selected = standard;
                if (selected == null && original != null) selected = original.shader;
                if (selected == null)
                {
                    if (!_loggedMissingPreviewShader)
                    {
                        Plugin.LogWarning("ArcaneUI character model: no compatible preview shader available.");
                        _loggedMissingPreviewShader = true;
                    }
                    continue;
                }
                Material preview = new Material(selected);
                preview.name = "ArcaneUI_AvatarPreviewMaterial";
                if (texture != null && preview.HasProperty("_MainTex"))
                    preview.SetTexture("_MainTex", texture);
                // Keep missing-texture regions visible rather than translucent.
                Color tint = texture != null ? Color.white : new Color(.78f, .83f, .87f, 1f);
                if (preview.HasProperty("_Color")) preview.SetColor("_Color", tint);
                if (preview.HasProperty("_BaseColor")) preview.SetColor("_BaseColor", tint);
                materials[i] = preview;
            }
            return materials;
        }

        private static bool Eligible(Renderer r, Transform actor)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) return false;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
            string name = r.name.ToLowerInvariant();
            string[] excluded = { "particle", "projectile", "beam", "spell", "trail", "effect",
                "weapon", "sword", "shield", "staff", "bow", "arrow", "spark",
                "hitbox", "nameplate", "target", "glow", "aura", "torch" };
            for (int i = 0; i < excluded.Length; i++)
                if (name.Contains(excluded[i])) return false;
            SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
            MeshRenderer mesh = r as MeshRenderer;
            if ((skin == null || skin.sharedMesh == null) &&
                (mesh == null || mesh.GetComponent<MeshFilter>() == null ||
                mesh.GetComponent<MeshFilter>().sharedMesh == null)) return false;
            Bounds b = r.bounds;
            if (b.size.y <= .015f || b.size.y >= 4.5f ||
                Vector3.Distance(b.center, actor.position) > 3.5f) return false;
            return true;
        }
        private static bool BodyNamed(Renderer r)
        {
            // Erenshor's Synty models can use independent renderers for body
            // sections rather than a single 18-bone skinned mesh.
            string n = r.name.ToLowerInvariant();
            string[] hints = { "body", "torso", "chest", "head", "face", "hair",
                "arm", "leg", "hand", "foot", "skin", "pelvis", "hips",
                "beard", "character", "synt", "shirt", "pants", "boot" };
            for (int i = 0; i < hints.Length; i++)
                if (n.Contains(hints[i])) return true;
            return false;
        }
        private static bool HasHumanoidRig(SkinnedMeshRenderer skin)
        {
            if (skin == null || skin.bones == null) return false;
            bool spine = false, hips = false, head = false;
            foreach (Transform bone in skin.bones)
            {
                if (bone == null) continue;
                string n = bone.name.ToLowerInvariant();
                if (n.Contains("spine") || n.Contains("chest")) spine = true;
                if (n.Contains("hips") || n.Contains("pelvis")) hips = true;
                if (n.Contains("head") || n.Contains("neck")) head = true;
            }
            return spine && hips && head;
        }
        private static bool HasSegmentedAnatomy(List<Renderer> parts)
        {
            bool torso = false, head = false, legs = false;
            foreach (Renderer renderer in parts)
            {
                string n = renderer.name.ToLowerInvariant();
                if (n.Contains("body") || n.Contains("torso") || n.Contains("chest")) torso = true;
                if (n.Contains("head") || n.Contains("face")) head = true;
                if (n.Contains("leg") || n.Contains("pants") || n.Contains("foot")) legs = true;
            }
            return torso && head && legs;
        }
        private void WarnNoBody(Transform actor, Renderer[] renderers, string reason)
        {
            if (reason == _lastFailure && actor == _lastFailureActor) return;
            _lastFailure = reason;
            _lastFailureActor = actor;
            Plugin.LogWarning("ArcaneUI character model: " + reason +
                ". actor=" + (actor != null ? actor.name : "none") +
                " rig=" + (_rigAnimator != null ? _rigAnimator.name : "none") +
                ". Preview remains blank rather than displaying loose equipment.");
            int shown = 0;
            for (int i = 0; i < renderers.Length && shown < 16; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
                MeshRenderer mesh = r as MeshRenderer;
                if (skin == null && mesh == null) continue;
                Plugin.LogInfo("ArcaneUI character model candidate " + (++shown) +
                    ": " + r.name + " type=" + r.GetType().Name +
                    " bones=" + (skin != null && skin.bones != null ? skin.bones.Length : 0) +
                    " bounds=" + r.bounds.size);
            }
        }
        private void Scan()
        {
            Transform actor = FindActor();
            _actor = actor;
            ClearProxies();
            if (actor == null)
            {
                if (!_loggedUnavailable)
                {
                    Plugin.LogWarning("ArcaneUI character model: cannot locate player avatar renderer hierarchy.");
                    _loggedUnavailable = true;
                }
                return;
            }
            _loggedUnavailable = false;
            Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true);
            var eligible = new List<Renderer>();
            var named = new List<Renderer>();
            SkinnedMeshRenderer strictBody = null;
            int best = -1;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (!Eligible(r, actor)) continue;
                eligible.Add(r);
                if (BodyNamed(r)) named.Add(r);
                SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
                if (skin == null || !HasHumanoidRig(skin)) continue;
                Bounds b = skin.bounds;
                if (b.size.y < .75f || b.size.y > 4f) continue;
                int score = skin.bones.Length * 100 + skin.sharedMesh.vertexCount;
                if (score > best) { best = score; strictBody = skin; }
            }
            var selected = new List<Renderer>();
            if (_rigAnimator != null && _rigHips != null && _rigHead != null)
            {
                // Use the real local player's Animator bone references. Do
                // not accept a random nearby glowing mesh as an avatar.
                for (int i = 0; i < eligible.Count; i++)
                {
                    Renderer renderer = eligible[i];
                    SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                    if (skin != null && ConnectedToRig(skin))
                        selected.Add(renderer);
                    else if (skin == null && BodyNamed(renderer) &&
                        Vector3.Distance(renderer.bounds.center,
                            (_rigHips.position + _rigHead.position) * .5f) < 1.5f)
                        selected.Add(renderer);
                }
                if (selected.Count > 0)
                {
                    Bounds body = selected[0].bounds;
                    for (int i = 1; i < selected.Count; i++)
                        body.Encapsulate(selected[i].bounds);
                    // A body spanning the hips and head must actually cover
                    // the standing character; never accept a floating item.
                    float rigHeight = Vector3.Distance(_rigHips.position, _rigHead.position);
                    if (body.size.y < Mathf.Max(.70f, rigHeight * .75f) ||
                        body.size.y > 3.8f || selected.Count < 2)
                        selected.Clear();
                }
            }
            if (selected.Count == 0 && strictBody != null)
            {
                // Keep character rig neighbourhood, not non-skinned effects.
                foreach (Renderer r in eligible)
                {
                    SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
                    if (skin != null && skin.bones != null && skin.bones.Length >= 1 &&
                        Vector3.Distance(skin.bounds.center, strictBody.bounds.center) < 1.8f &&
                        (BodyNamed(skin) || skin == strictBody || HasHumanoidRig(skin)))
                        selected.Add(r);
                }
            }
            if (selected.Count == 0)
            {
                // Segmented avatar fallback: a verified group of body sections,
                // or a single body-named renderer with full humanoid proportions.
                List<Renderer> candidate = named.Count >= 2 ? named : eligible;
                if (candidate.Count > 0)
                {
                    Bounds b = candidate[0].bounds;
                    for (int i = 1; i < candidate.Count; i++) b.Encapsulate(candidate[i].bounds);
                    bool plausible = b.size.y >= 1.05f && b.size.y <= 3.6f &&
                        b.size.x >= .25f && b.size.x <= 2.5f &&
                        b.size.z <= 2.5f &&
                        Vector3.Distance(b.center, actor.position) <= 2.5f;
                    bool anatomy = named.Count >= 3 && HasSegmentedAnatomy(named);
                    if (plausible && anatomy)
                        selected.AddRange(candidate);
                }
            }
            if (selected.Count == 0)
            {
                WarnNoBody(actor, renderers, "no verified whole-body or segmented humanoid mesh group");
                _lastVisible = -1;
                return;
            }
            for (int i = 0; i < selected.Count && _proxies.Count < 64; i++)
            {
                Renderer src = selected[i];
                SkinnedMeshRenderer skin = src as SkinnedMeshRenderer;
                MeshRenderer originalMesh = src as MeshRenderer;
                MeshFilter originalFilter = originalMesh != null ? originalMesh.GetComponent<MeshFilter>() : null;
                if (skin == null && (originalFilter == null || originalFilter.sharedMesh == null)) continue;
                GameObject copy = new GameObject("ArcaneUI_CharacterPart_" + i);
                copy.layer = PreviewLayer;
                MeshFilter destination = copy.AddComponent<MeshFilter>();
                Mesh baked = null;
                if (skin != null)
                {
                    baked = new Mesh();
                    baked.name = "ArcaneUI_BakedBodyPart";
                    destination.sharedMesh = baked;
                }
                else destination.sharedMesh = originalFilter.sharedMesh;
                Material[] previewMaterials = BuildPreviewMaterials(src);
                copy.AddComponent<MeshRenderer>().sharedMaterials = previewMaterials;
                _proxies.Add(new Proxy { Source = src, Skin = skin, Copy = copy,
                    Baked = baked, PreviewMaterials = previewMaterials });
            }
            if (_proxies.Count == 0)
                WarnNoBody(actor, renderers, "could not build preview meshes");
            else
            {
                _lastFailure = "";
                _lastVisible = -1;
                Plugin.LogInfo("ArcaneUI character model: captured " + _proxies.Count +
                    " verified avatar sections (skinned/static; no scripts cloned).");
            }
        }

        internal void Update()
        {
            if (_camera == null || _image == null) return;
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 8f;
                Scan();
            }
            if (_proxies.Count == 0 || Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + .10f;
            Bounds bounds = new Bounds();
            int count = 0;
            for (int i = 0; i < _proxies.Count; i++)
            {
                Proxy p = _proxies[i];
                if (p.Copy == null || p.Source == null) continue;
                bool visible = p.Source.enabled && p.Source.gameObject.activeInHierarchy;
                p.Copy.SetActive(visible);
                if (!visible) continue;
                Transform original = p.Source.transform;
                p.Copy.transform.position = PreviewOrigin +
                    (original.position - (_actor != null ? _actor.position : Vector3.zero));
                p.Copy.transform.rotation = original.rotation;
                p.Copy.transform.localScale = original.lossyScale;
                if (p.Skin != null && p.Baked != null)
                {
                    try { p.Skin.BakeMesh(p.Baked); }
                    catch (Exception) { p.Copy.SetActive(false); continue; }
                }
                Renderer r = p.Copy.GetComponent<Renderer>();
                if (r == null) continue;
                if (count++ == 0) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
            }
            if (count == 0) return;
            // Camera framing must include a plausible standing humanoid, not
            // individual detached weapons, particle accessories, or rig gizmos.
            if (bounds.size.y < .9f || bounds.size.x < .30f || bounds.size.y > 5f)
            {
                if (_lastVisible != -2)
                    Plugin.LogWarning("ArcaneUI character model: invalid avatar bounds " +
                        bounds.size + "; leaving preview blank.");
                _lastVisible = -2;
                return;
            }
            if (_lastVisible != count)
            {
                _lastVisible = count;
                Plugin.LogInfo("ArcaneUI character model: " + count + " visible mesh parts, bounds " + bounds.size);
            }
            Vector3 aim = bounds.center;
            RectTransform displayRect = _image.rectTransform;
            if (displayRect != null && displayRect.rect.height > 1f)
                _camera.aspect = Mathf.Max(.12f, displayRect.rect.width / displayRect.rect.height);
            Vector3 facing = _actor != null ? _actor.forward : Vector3.forward;
            // Frame the *captured proxy bounds*, not the actor root, which can
            // have a different pivot or origin from its skinned geometry.
            _camera.orthographicSize = Mathf.Max(.60f,
                bounds.extents.y * 1.18f,
                bounds.extents.x * 1.18f / Mathf.Max(.12f, _camera.aspect));
            _camera.transform.position = aim + facing * 5f;
            _camera.transform.rotation = Quaternion.LookRotation(aim - _camera.transform.position, Vector3.up);
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_camera);
            int inside = 0;
            foreach (Proxy proxy in _proxies)
            {
                if (proxy.Copy == null || !proxy.Copy.activeSelf) continue;
                Renderer proxyRenderer = proxy.Copy.GetComponent<Renderer>();
                if (proxyRenderer != null && GeometryUtility.TestPlanesAABB(planes, proxyRenderer.bounds))
                    inside++;
            }
            if (inside != _lastCameraVisible)
            {
                _lastCameraVisible = inside;
                Plugin.LogInfo("ArcaneUI character model: preview camera sees " + inside +
                    "/" + count + " mesh bounds; orthographicSize=" +
                    _camera.orthographicSize.ToString("F2") +
                    " materials=" + (_proxies.Count > 0 && _proxies[0].PreviewMaterials != null ?
                    _proxies[0].PreviewMaterials.Length : 0));
            }
            try { _camera.Render(); }
            catch (Exception ex)
            {
                Plugin.LogWarning("ArcaneUI character model render unavailable: " + ex.GetType().Name);
                _nextRender = Time.unscaledTime + 5f;
            }
        }

        internal void Destroy()
        {
            ClearProxies();
            if (_image != null) _image.texture = null;
            if (_cameraObject != null) UnityEngine.Object.Destroy(_cameraObject);
            if (_texture != null) { _texture.Release(); UnityEngine.Object.Destroy(_texture); }
            _camera = null;
            _cameraObject = null;
            _texture = null;
            _actor = null;
            _rigAnimator = null;
            _rigHead = null;
            _rigHips = null;
        }
    }
}
