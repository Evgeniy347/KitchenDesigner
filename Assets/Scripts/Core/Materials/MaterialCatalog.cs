using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum TextureState
    {
        NotRequested,
        Loading,
        Loaded,
        Failed,
    }

    public class MaterialDef
    {
        public string id;
        public string displayName;
        public string kind;
        public string? fileName;
        public Texture2D? texture;
        public TextureState textureState = TextureState.NotRequested;
        public Color baseColor = Color.gray;
        public int tileWidthMM = 800;
        public int tileHeightMM = 0;
        public float metallic = 0f;
        public float smoothness = 0.2f;

        public bool HasTextureFile => !string.IsNullOrEmpty(fileName);

        public int TileHeightMmWithoutLookingAtTheImage => tileHeightMM > 0 ? tileHeightMM : tileWidthMM;

        public MaterialDef(string id, string displayName, string kind, Color color,
            string? fileName = null, int tileWidthMM = 800,
            float metallic = 0f, float smoothness = 0.2f)
        {
            this.id = id;
            this.displayName = displayName;
            this.kind = kind;
            this.baseColor = color;
            this.fileName = fileName;
            this.tileWidthMM = tileWidthMM;
            this.metallic = metallic;
            this.smoothness = smoothness;
        }
    }

    public static class MaterialCatalog
    {
        public const string DefaultId = AppConstants.DEFAULT_MATERIAL_ID;

        private static readonly MaterialDef _fallback =
            new MaterialDef(DefaultId, "Серый", "ЛДСП", new Color(0.80f, 0.80f, 0.80f));

        private static readonly List<MaterialDef> _defs = new List<MaterialDef>();

        private static bool _indexReadAttempted;

        private static void EnsureLoaded()
        {
            if (_indexReadAttempted) return;
            _indexReadAttempted = true;
            TextureLibrary.TryLoadIndexSync();
        }

        public static IReadOnlyList<MaterialDef> All
        {
            get { EnsureLoaded(); return _defs; }
        }

        public static MaterialDef Default => Get(DefaultId);

        public static void Load(IEnumerable<MaterialDef> defs)
        {
            _defs.Clear();
            foreach (var d in defs) Put(d);
            _indexReadAttempted = true;
        }

        public static void Register(MaterialDef def)
        {
            EnsureLoaded();
            Put(def);
        }

        private static void Put(MaterialDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            _defs.RemoveAll(d => d.id == def.id);
            _defs.Add(def);
        }

        public static void Reset()
        {
            _defs.Clear();
            _indexReadAttempted = false;
        }

        public static MaterialDef Get(string? id)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id))
                foreach (var d in _defs)
                    if (d.id == id) return d;

            foreach (var d in _defs)
                if (d.id == DefaultId) return d;
            return _fallback;
        }

        public static MaterialDef? Find(string? idOrDisplayName)
        {
            if (string.IsNullOrEmpty(idOrDisplayName)) return null;
            EnsureLoaded();
            foreach (var d in _defs)
                if (string.Equals(d.id, idOrDisplayName, System.StringComparison.OrdinalIgnoreCase)) return d;
            foreach (var d in _defs)
                if (string.Equals(d.displayName, idOrDisplayName, System.StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }
    }
}
