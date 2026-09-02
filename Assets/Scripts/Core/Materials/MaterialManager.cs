using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class MaterialManager
    {
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        private static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

        private static readonly Dictionary<string, Vector2Int> _resolvedTileMM = new Dictionary<string, Vector2Int>();

        private static readonly MaterialPropertyBlock _reusedPropertyBlock = new MaterialPropertyBlock();

        public static Vector4 ComputeTileST(Vector3Int dimsMM, int squareTileMM)
            => ComputeTileST(dimsMM, squareTileMM, squareTileMM);

        public static Vector4 ComputeTileST(Vector3Int dimsMM, int tileWidthMM, int tileHeightMM)
            => ComputeTileST(new Vector2Int(dimsMM.x, dimsMM.y), tileWidthMM, tileHeightMM);

        public static Vector4 ComputeTileST(Vector2Int surfaceMM, int tileWidthMM, int tileHeightMM)
        {
            float tw = Mathf.Max(1, tileWidthMM);
            float th = Mathf.Max(1, tileHeightMM);
            return new Vector4(surfaceMM.x / tw, surfaceMM.y / th, 0f, 0f);
        }

        public static int TileHeightFromAspect(int tileWidthMM, int texWidthPx, int texHeightPx)
        {
            if (texWidthPx <= 0 || texHeightPx <= 0) return Mathf.Max(1, tileWidthMM);
            return Mathf.Max(1, Mathf.RoundToInt(tileWidthMM * (float)texHeightPx / texWidthPx));
        }

        public static Vector2Int TileMM(MaterialDef def)
        {
            if (def == null) return new Vector2Int(1, 1);

            int w = Mathf.Max(1, def.tileWidthMM);
            if (def.tileHeightMM > 0) return new Vector2Int(w, def.tileHeightMM);

            if (!string.IsNullOrEmpty(def.id) && _resolvedTileMM.TryGetValue(def.id, out var cached))
                return cached;

            var tex = ResolveTexture(def);
            var size = new Vector2Int(w, tex != null
                ? TileHeightFromAspect(w, tex.width, tex.height)
                : w);

            bool sizeIsFinalNotAPlaceholder = tex != null || !def.HasTextureFile;
            if (!string.IsNullOrEmpty(def.id) && sizeIsFinalNotAPlaceholder)
                _resolvedTileMM[def.id] = size;
            return size;
        }

        public static Texture2D? ResolveTexture(MaterialDef def)
        {
            if (def == null) return null;
            if (def.texture != null) return def.texture;
            TextureLibrary.Request(def);
            return def.texture;
        }

        public static void OnTextureArrived(MaterialDef def)
        {
            if (def == null || def.texture == null) return;

            if (_cache.TryGetValue(def.id, out var mat) && mat != null)
            {
                mat.SetTexture(BaseMap, def.texture);
                mat.mainTexture = def.texture;
                ApplyAlbedo(mat, def);
            }

            if (def.tileHeightMM > 0) return;

            _resolvedTileMM.Remove(def.id);
            var all = PartRegistry.GetAll();
            if (all == null) return;

            foreach (var el in all)
            {
                if (el == null) continue;

                if (el.MaterialId == def.id
                    || MaterialIdOf(el, MaterialSlot.Tabletop) == def.id)
                    RefreshTiling(el, def);

                if (UsesInOverlay(el, def.id)) TextureOverlayRenderer.Refresh(el);
            }
        }

        private static bool UsesInOverlay(KitchenElement element, string materialId)
        {
            foreach (var overlay in element.TextureOverlays)
                if (overlay.MaterialId == materialId) return true;
            return false;
        }

        public static void ApplyById(KitchenElement element, string materialId)
        {
            if (element == null) return;
            var def = MaterialCatalog.Get(materialId);
            Apply(element, def);
            if (!string.IsNullOrEmpty(materialId) && def.id != materialId)
                element.MaterialId = materialId;
        }

        public static void ApplySlot(KitchenElement element, MaterialSlot slot, MaterialDef def)
        {
            if (element == null || def == null) return;
            switch (slot)
            {
                case MaterialSlot.Tabletop:
                    if (element is ITabletop top) ApplyTabletop(top, def);
                    break;
                case MaterialSlot.Legs:
                    if (element is ITabletop legs) ApplyLegs(legs, def);
                    break;
                default:
                    Apply(element, def);
                    break;
            }
        }

        public static string MaterialIdOf(KitchenElement element, MaterialSlot slot)
        {
            if (element == null) return MaterialCatalog.DefaultId;
            switch (slot)
            {
                case MaterialSlot.Tabletop:
                    return element is ITabletop top ? top.TabletopMaterialId : element.MaterialId;
                case MaterialSlot.Legs:
                    return element is ITabletop legs ? legs.LegsMaterialId : element.MaterialId;
                default:
                    return element.MaterialId;
            }
        }

        public static void ApplyTabletop(ITabletop tabletop, MaterialDef def)
        {
            if (tabletop == null || def == null) return;
            tabletop.TabletopMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) tabletop.SetTabletopMaterial(mat);
            if (tabletop is KitchenElement element) RefreshTiling(element, def);
        }

        public static void ApplyLegs(ITabletop tabletop, MaterialDef def)
        {
            if (tabletop == null || def == null) return;
            tabletop.LegsMaterialId = def.id;
            var mat = GetSharedMaterial(def);
            if (mat != null) tabletop.SetLegsMaterial(mat);
        }

        public static bool HasCustomDecor(KitchenElement element)
            => element != null
               && !string.IsNullOrEmpty(element.MaterialId)
               && element.MaterialId != MaterialCatalog.DefaultId;

        public static void ApplyOwnDecor(KitchenElement element)
            => Apply(element, MaterialCatalog.Get(element != null ? element.MaterialId : null));

        public static void Apply(KitchenElement element, MaterialDef def)
        {
            if (element == null || def == null) return;
            element.MaterialId = def.id;

            var mat = GetSharedMaterial(def);
            if (mat == null) return;

            if (element is ITabletop tabletop)
            {
                tabletop.SetTabletopMaterial(mat);
                RefreshTiling(element, def);
                return;
            }

            if (element is IPaintsItself painter)
            {
                painter.SetMaterial(mat);
                RefreshTiling(element, def);
                return;
            }

            if (element is CooktopElement cooktop)
            {
                cooktop.ApplyMaterials();
                RefreshTiling(element, def);
                return;
            }

            var r = element.GetComponentInChildren<MeshRenderer>();
            if (r == null) return;

            var mats = r.sharedMaterials;
            if (mats.Length > 1)
            {
                mats[0] = mat;
                r.sharedMaterials = mats;
                element.RefreshSubmeshMaterials();
            }
            else
            {
                r.sharedMaterial = mat;
            }
            RefreshTiling(element, def);
        }

        public static void RefreshTiling(KitchenElement element)
            => RefreshTiling(element, MaterialCatalog.Get(element != null ? element.MaterialId : null));

        public static void RefreshTiling(KitchenElement element, MaterialDef def)
        {
            if (element == null || def == null) return;

            var tile = TileMM(def);
            var st = ComputeTileST(element.DecorSurfaceMM, tile.x, tile.y);

            if (element is CooktopElement)
            {
                foreach (var cr in element.GetComponentsInChildren<MeshRenderer>())
                {
                    if (cr == null) continue;
                    cr.GetPropertyBlock(_reusedPropertyBlock);
                    _reusedPropertyBlock.SetVector(BaseMapST, st);
                    cr.SetPropertyBlock(_reusedPropertyBlock);
                }
                return;
            }

            var r = element.DecorRenderer;
            if (r == null) return;

            r.GetPropertyBlock(_reusedPropertyBlock);
            _reusedPropertyBlock.SetVector(BaseMapST, st);
            r.SetPropertyBlock(_reusedPropertyBlock);
        }

        public static Material? GetSharedMaterial(MaterialDef def)
        {
            if (def == null) return null;
            if (_cache.TryGetValue(def.id, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.SetFloat(Metallic, def.metallic);
            mat.SetFloat(Smoothness, def.smoothness);

            var tex = ResolveTexture(def);
            if (tex != null)
            {
                ConfigureTexture(tex);
                mat.SetTexture(BaseMap, tex);
                mat.mainTexture = tex;
            }
            ApplyAlbedo(mat, def);

            _cache[def.id] = mat;
            return mat;
        }

        private static void ApplyAlbedo(Material mat, MaterialDef def)
        {
            var color = def.texture != null ? Color.white : def.baseColor;
            mat.SetColor(BaseColor, color);
            mat.color = color;
        }

        public static void ConfigureTexture(Texture2D tex)
        {
            if (tex == null) return;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _resolvedTileMM.Clear();
        }
    }
}
