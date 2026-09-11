using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ScenePreview
    {
        public const string TintName = "KD Preview Tint";

        private static readonly ScenePreviewState State = new ScenePreviewState();
        private static readonly List<MeshRenderer> Muted = new List<MeshRenderer>();
        private static readonly List<Material> Painted = new List<Material>();
        private static GameObject? _ghost;

        private static Func<GameObject?>? _spawn;
        private static KitchenElement? _replaced;
        private static KitchenElement? _owner;
        private static bool _watchesOwner;
        private static Vector3 _ownerPos;
        private static Quaternion _ownerRot;
        private static Vector3Int _ownerDims;

        public static string? ShownKey => State.ShownKey;

        public static bool IsShowing => State.IsShowing;

        public static GameObject? Ghost => _ghost;

        public static IReadOnlyList<MeshRenderer> MutedRenderers => Muted;

        public static void Hover(string key, Func<GameObject?> spawn, KitchenElement? replaced,
            KitchenElement? owner = null)
        {
            var step = State.Hover(key);
            if (step == ScenePreviewStep.None) return;

            Teardown();
            _spawn = spawn;
            _replaced = replaced;
            _owner = owner;
            _watchesOwner = owner != null;
            Build(spawn, replaced);
            RememberOwnerPose();
        }

        public static void Leave()
        {
            if (State.Leave() == ScenePreviewStep.None) return;
            Teardown();
            Forget();
        }

        public static string? Commit()
        {
            var key = State.ShownKey;
            State.Commit();
            Teardown();
            Forget();
            return key;
        }

        public static void Sync()
        {
            if (!State.IsShowing || _spawn == null) return;
            if (!_watchesOwner) return;

            if (HoverAnchor.IsGone(_owner))
            {
                Leave();
                return;
            }

            if (_owner!.transform.position == _ownerPos
                && _owner.transform.rotation == _ownerRot
                && _owner.DimensionsMM == _ownerDims) return;

            var spawn = _spawn;
            var replaced = _replaced;
            Teardown();
            Build(spawn, replaced);
            RememberOwnerPose();
        }

        private static void Build(Func<GameObject?> spawn, KitchenElement? replaced)
        {
            if (spawn != null)
                using (ElementFactorySandbox.Enter()) _ghost = spawn();

            if (_ghost != null)
            {
                _ghost.hideFlags = HideFlags.DontSave;
                Intangible(_ghost);
                Tint(_ghost);
            }

            Mute(replaced);
        }

        private static void RememberOwnerPose()
        {
            if (_owner == null) return;
            _ownerPos = _owner.transform.position;
            _ownerRot = _owner.transform.rotation;
            _ownerDims = _owner.DimensionsMM;
        }

        private static void Teardown()
        {
            if (Muted.Count > 0)
            {
                foreach (var renderer in Muted)
                    if (renderer != null) renderer.enabled = true;
                Muted.Clear();
                SceneVisibilityManager.Invalidate();
            }

            foreach (var material in Painted)
                if (material != null) DestroyNow.The(material);
            Painted.Clear();

            if (_ghost != null) DestroyNow.The(_ghost);
            _ghost = null;
        }

        private static void Forget()
        {
            _spawn = null;
            _replaced = null;
            _owner = null;
            _watchesOwner = false;
            _ownerPos = default;
            _ownerRot = default;
            _ownerDims = default;
        }

        private static void Mute(KitchenElement? replaced)
        {
            if (replaced == null) return;
            foreach (var renderer in ElementRenderers.BodyOf(replaced))
            {
                if (renderer == null || !renderer.enabled) continue;
                renderer.enabled = false;
                Muted.Add(renderer);
            }
        }

        private static void Intangible(GameObject ghost)
        {
            foreach (var collider in ghost.GetComponentsInChildren<Collider>(true))
                if (collider != null) collider.enabled = false;

            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void Tint(GameObject ghost)
        {
            var color = UI.UIStyle.PreviewGhost;
            foreach (var renderer in ghost.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                var source = renderer.sharedMaterial;
                var painted = source != null ? TransparentMaterial.Make(source, color) : NeutralGhost(color);
                if (painted == null) continue;

                painted.name = TintName;
                painted.hideFlags = HideFlags.DontSave;

                renderer.sharedMaterial = painted;
                Painted.Add(painted);
            }
        }

        private static Material? NeutralGhost(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader == null ? null : TransparentMaterial.Make(shader, color);
        }
    }
}
