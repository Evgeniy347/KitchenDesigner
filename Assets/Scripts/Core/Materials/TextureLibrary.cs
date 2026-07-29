using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace KitchenDesigner.Core
{
    /// <summary>Папка декоров и ленивая загрузка их картинок.
    ///
    /// Источник — <c>StreamingAssets/Textures</c>: Unity кладёт эту папку в ЛЮБУЮ
    /// сборку как есть (Windows → <c>&lt;exe&gt;/&lt;Product&gt;_Data/StreamingAssets</c>,
    /// WebGL → корень сборки), поэтому один и тот же набор файлов работает и на
    /// десктопе, и в браузере, и в редакторе. Картинки НЕ проходят импорт Unity —
    /// это обычные файлы, которые можно подменить в установленной сборке и
    /// перечитать через MCP <c>reload_textures</c>.
    ///
    /// Метаданные (<c>index.json</c>) читаются целиком и заранее — до восстановления
    /// сцены, иначе сохранённые materialId не нашли бы свой декор. Сами картинки
    /// грузятся ЛЕНИВО, по первому обращению к декору: полный прогон всех файлов
    /// стоит десятки миллисекунд на картинку, а сцена использует единицы декоров.
    ///
    /// Загрузка синхронная там, где папка доступна как файловая (Standalone,
    /// редактор) — это делает поведение детерминированным для тестов и скриншотов.
    /// В WebGL файловой системы нет, поэтому индекс и картинки тянутся
    /// UnityWebRequest'ом, по одной картинке за кадр.</summary>
    public static class TextureLibrary
    {
        public const string FolderName = "Textures";
        public const string IndexFileName = "index.json";

        // Картинки, созданные библиотекой: их надо уничтожить при Reload, иначе
        // каждая перезагрузка папки оставляла бы прежний набор в VRAM.
        private static readonly List<Texture2D> _owned = new List<Texture2D>();

        // Очередь для WebGL: там синхронного пути нет, а разбирать её надо по одной
        // картинке за кадр — декод идёт на главном потоке.
        private static readonly Queue<MaterialDef> _pending = new Queue<MaterialDef>();
        private static bool _pumping;

        /// <summary>Папка декоров. На Standalone и в редакторе — обычный путь на
        /// диске, в WebGL — URL.</summary>
        public static string DirectoryPath => Application.streamingAssetsPath + "/" + FolderName;

        /// <summary>Индекс лежит рядом с картинками.</summary>
        public static string IndexPath => DirectoryPath + "/" + IndexFileName;

        /// <summary>Папка доступна как файловая — можно читать синхронно. Ложь в
        /// WebGL (там StreamingAssets — это адрес на сервере).</summary>
        public static bool HasFileAccess
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                try { return Directory.Exists(DirectoryPath); }
                catch (Exception) { return false; }
#endif
            }
        }

        // ── Индекс ────────────────────────────────────────────────────

        /// <summary>Прочитать индекс синхронно. Возвращает false, если файловой
        /// системы нет (WebGL) — тогда индекс надо тянуть <see cref="LoadIndexAsync"/>.
        /// Никогда не бросает: на любой сбой логирует и оставляет каталог с одним
        /// дефолтным декором.</summary>
        public static bool TryLoadIndexSync()
        {
            if (!HasFileAccess) return false;

            string json;
            try
            {
                if (!System.IO.File.Exists(IndexPath))
                {
                    Debug.LogWarning($"[Textures] Индекс не найден: {IndexPath}");
                    return true; // файловая система есть, читать нечего — повторять нечем
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

        /// <summary>Прочитать индекс запросом — путь WebGL. Ждать эту корутину надо
        /// ДО загрузки сцены.</summary>
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

        // ── Картинки ──────────────────────────────────────────────────

        /// <summary>Затребовать картинку декора. Возврат мгновенный: там, где есть
        /// файловая система, картинка читается тут же, иначе встаёт в очередь и
        /// приезжает через несколько кадров. Повторные вызовы бесплатны.</summary>
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

        /// <summary>Затребовать картинки декоров, которые уже стоят в сцене. Зовётся
        /// после восстановления проекта: в WebGL это убирает «вспышку» базового
        /// цвета на первых кадрах.</summary>
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

        /// <summary>Перечитать папку целиком: забыть картинки и каталог, прочитать
        /// индекс заново, пере-надеть декоры на сцену. Возвращает число декоров
        /// в новом каталоге.
        ///
        /// Пере-надеть обязательно: старые картинки уничтожены, и рендереры
        /// остались бы с материалами, ссылающимися в пустоту.</summary>
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

        /// <summary>Байты → готовая к показу текстура.
        ///
        /// Мипмапы обязательны (без них мелкий рисунок на дальних деталях кипит),
        /// поэтому картинка собирается через <c>LoadImage</c>, а не через
        /// <c>UnityWebRequestTexture</c> — тот мип-цепочку не строит.
        ///
        /// Сжатие в рантайме экономит ×8 VRAM (RGBA32 1080×1080 с мипами — 6,2 МБ,
        /// DXT — 0,8 МБ). Требует сторон, кратных 4, и не поддерживается в WebGL;
        /// в обоих случаях картинка просто остаётся несжатой.
        ///
        /// <c>makeNoLongerReadable</c> освобождает копию пикселей в системной
        /// памяти — она нужна была только для сжатия.</summary>
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

#if !UNITY_WEBGL || UNITY_EDITOR
            if (tex.width % 4 == 0 && tex.height % 4 == 0) tex.Compress(highQuality: false);
#endif
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private static void DestroySafe(Texture2D? tex)
        {
            if (tex == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
            else UnityEngine.Object.DestroyImmediate(tex);
        }

        // ── Очередь (только там, где нет файловой системы) ─────────────

        /// <summary>Поднять разбор очереди, если он ещё не идёт. Флаг ставится
        /// СИНХРОННО: корутина начнёт выполняться только на следующем кадре, а
        /// запросы за это время придут ещё — второй разборщик тут не нужен.</summary>
        private static void EnsurePumping()
        {
            if (_pumping) return;
            _pumping = true;
            if (!TextureLibraryHost.Run(PumpQueue())) _pumping = false;
        }

        /// <summary>Разбирает очередь по ОДНОЙ картинке за кадр: декод идёт на
        /// главном потоке, и пачка сразу дала бы видимый рывок.</summary>
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

    /// <summary>Хозяин корутин библиотеки: статике нужен MonoBehaviour, чтобы
    /// что-то ждать. Создаётся сам при первой нужде и живёт между сценами.</summary>
    internal class TextureLibraryHost : MonoBehaviour
    {
        private static TextureLibraryHost? _instance;

        /// <summary>Запустить корутину. Ложь — запускать негде (EditMode), звавший
        /// должен откатить своё состояние.</summary>
        internal static bool Run(IEnumerator routine)
        {
            if (!Application.isPlaying) return false; // в EditMode корутин нет

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
