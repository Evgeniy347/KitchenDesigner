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

        public static string? ShownKey => State.ShownKey;

        public static bool IsShowing => State.IsShowing;

        public static GameObject? Ghost => _ghost;

        public static IReadOnlyList<MeshRenderer> MutedRenderers => Muted;

        public static void Hover(string key, Func<GameObject?> spawn, KitchenElement? replaced)
        {
            var step = State.Hover(key);
            if (step == ScenePreviewStep.None) return;

            Teardown();
            Build(spawn, replaced);
        }

        public static void Leave()
        {
            if (State.Leave() == ScenePreviewStep.None) return;
            Teardown();
        }

        public static string? Commit()
        {
            var key = State.ShownKey;
            State.Commit();
            Teardown();
            return key;
        }

        private static void Build(Func<GameObject?> spawn, KitchenElement? replaced)
        {
            if (spawn == null)
            {
                State.Leave();
                return;
            }

            using (ElementFactorySandbox.Enter()) _ghost = spawn();

            if (_ghost == null)
            {
                State.Leave();
                return;
            }

            _ghost.hideFlags = HideFlags.DontSave;
            Tint(_ghost);
            Mute(replaced);
        }

        private static void Teardown()
        {
            foreach (var renderer in Muted)
                if (renderer != null) renderer.enabled = true;
            Muted.Clear();

            foreach (var material in Painted)
                if (material != null) DestroyNow(material);
            Painted.Clear();

            if (_ghost != null) DestroyNow(_ghost);
            _ghost = null;
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

        private static void Tint(GameObject ghost)
        {
            var color = UI.UIStyle.PreviewGhost;
            foreach (var renderer in ghost.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                var source = renderer.sharedMaterial;
                var painted = source != null ? new Material(source) : NeutralGhost();
                if (painted == null) continue;

                painted.name = TintName;
                painted.hideFlags = HideFlags.DontSave;
                painted.SetFloat("_Surface", 1f);
                painted.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                painted.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                painted.color = color;
                if (painted.HasProperty("_BaseColor")) painted.SetColor("_BaseColor", color);

                renderer.sharedMaterial = painted;
                Painted.Add(painted);
            }
        }

        private static Material? NeutralGhost()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader == null ? null : new Material(shader);
        }

        private static void DestroyNow(UnityEngine.Object obj)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
