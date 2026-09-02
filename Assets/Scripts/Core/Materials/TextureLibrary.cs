using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core
{
    public static class TextureLibrary
    {
        public const string FolderName = "Textures";
        public const string IndexFileName = "index.json";

        private static readonly List<Texture2D> _owned = new List<Texture2D>();

        private static readonly Queue<MaterialDef> _pending = new Queue<MaterialDef>();
        private static bool _pumping;

        public static string DirectoryPath => Application.streamingAssetsPath + "/" + FolderName;

        public static string IndexPath => DirectoryPath + "/" + IndexFileName;

        public static bool HasFileAccess
        {
            get
            {
                try { return Directory.Exists(DirectoryPath); }
                catch (Exception) { return false; }
            }
        }

        public static bool TryLoadIndexSync()
        {
            if (!HasFileAccess) return false;

            string json;
            try
            {
                if (!System.IO.File.Exists(IndexPath))
                {
                    Debug.LogWarning($"[Textures] Индекс не найден: {IndexPath}");
                    return true;
                }
                json = System.IO.File.ReadAllText(IndexPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Textures] Индекс не прочитан ({IndexPath}): {e.Message}");
                return true;
            }

            ApplyIndex(json);
            return true;
        }

        public static IEnumerator LoadIndexAsync()
        {
            using var req = UnityWebRequest.Get(IndexPath);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Textures] Индекс не загружен ({IndexPath}): {req.error}");
                yield break;
            }

            ApplyIndex(req.downloadHandler.text);
        }

        private static void ApplyIndex(string json)
        {
            var defs = TextureIndex.Parse(json, out var errors);
            foreach (var err in errors) Debug.LogWarning($"[Textures] index.json: {err}");

            MaterialCatalog.Load(defs);
            Debug.Log($"[Textures] Декоров в каталоге: {defs.Count} (из {IndexPath})");
        }

        public static void Request(MaterialDef? def)
        {
            if (def == null || !def.HasTextureFile) return;
            if (def.textureState != TextureState.NotRequested) return;

            if (HasFileAccess)
            {
                def.textureState = TextureState.Loading;
                LoadSync(def);
                return;
            }

            def.textureState = TextureState.Loading;
            _pending.Enqueue(def);
            EnsurePumping();
        }

        public static void PrefetchScene()
        {
            var all = PartRegistry.GetAll();
            if (all == null) return;

            foreach (var el in all)
            {
                if (el == null) continue;
                Request(MaterialCatalog.Get(el.MaterialId));
                Request(MaterialCatalog.Get(MaterialManager.MaterialIdOf(el, MaterialSlot.Tabletop)));
                Request(MaterialCatalog.Get(MaterialManager.MaterialIdOf(el, MaterialSlot.Legs)));
                foreach (var overlay in el.TextureOverlays)
                    Request(MaterialCatalog.Get(overlay.MaterialId));
            }
        }

        public static int Reload()
        {
            _pending.Clear();
            foreach (var tex in _owned) DestroySafe(tex);
            _owned.Clear();

            MaterialManager.ClearCache();
            MaterialCatalog.Reset();

            if (!TryLoadIndexSync()) TextureLibraryHost.Run(LoadIndexAsync());

            int count = MaterialCatalog.All.Count;

            var all = PartRegistry.GetAll();
            if (all != null)
                foreach (var el in all)
                    if (el != null) MaterialManager.ApplyById(el, el.MaterialId);

            PrefetchScene();
            return count;
        }

        private static void LoadSync(MaterialDef def)
        {
            var path = Path.Combine(DirectoryPath, def.fileName!);
            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Fail(def, $"{def.fileName}: {e.Message}");
                return;
            }

            Accept(def, bytes);
        }

        private static IEnumerator LoadAsync(MaterialDef def)
        {
            var url = DirectoryPath + "/" + def.fileName;
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Fail(def, $"{def.fileName}: {req.error}");
                yield break;
            }

            Accept(def, req.downloadHandler.data);
        }

        private static void Accept(MaterialDef def, byte[] bytes)
        {
            var tex = Decode(bytes, def.id);
            if (tex == null)
            {
                Fail(def, $"{def.fileName}: не картинка или битый файл");
                return;
            }

            _owned.Add(tex);
            def.texture = tex;
            def.textureState = TextureState.Loaded;
            MaterialManager.OnTextureArrived(def);
        }

        private static void Fail(MaterialDef def, string message)
        {
            def.textureState = TextureState.Failed;
            Debug.LogWarning($"[Textures] {message}");
        }

        private static Texture2D? Decode(byte[] bytes, string name)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
            if (!tex.LoadImage(bytes))
            {
                DestroySafe(tex);
                return null;
            }

            tex.name = name;
            MaterialManager.ConfigureTexture(tex);

            if (tex.width % 4 == 0 && tex.height % 4 == 0) tex.Compress(highQuality: false);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private static void DestroySafe(Texture2D? tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
            else UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void EnsurePumping()
        {
            if (_pumping) return;
            _pumping = true;
            if (!TextureLibraryHost.Run(PumpQueue())) _pumping = false;
        }

        private static IEnumerator PumpQueue()
        {
            while (_pending.Count > 0)
            {
                var def = _pending.Dequeue();
                yield return LoadAsync(def);
            }
            _pumping = false;
        }
    }

    internal class TextureLibraryHost : MonoBehaviour
    {
        private static TextureLibraryHost? _instance;

        internal static bool Run(IEnumerator routine)
        {
            if (!Application.isPlaying) return false;

            if (_instance == null)
            {
                var go = new GameObject("TextureLibraryHost") { hideFlags = HideFlags.HideAndDontSave };
                _instance = go.AddComponent<TextureLibraryHost>();
                DontDestroyOnLoad(go);
            }

            _instance.StartCoroutine(routine);
            return true;
        }
    }
}
