using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Подгрузка декоров ЛДСП из ВНЕШНЕЙ папки рядом с приложением
    /// (<c>&lt;папка_exe&gt;/Resources/Textures</c>), чтобы добавлять новые текстуры
    /// БЕЗ пересборки: положил картинку в папку → перезапустил (или reload_textures) →
    /// декор появился в списке. Имя файла задаёт физ. размер плитки:
    /// <c>&lt;имя&gt;_&lt;ширинаММ&gt;_&lt;высотаММ&gt;.jpg</c> (напр.
    /// <c>abrikos_ba_03_cd_100_100.jpg</c> → «abrikos ba 03 cd», плитка 100×100 мм).
    /// Без суффикса размера — плитка 800×800 мм по умолчанию.</summary>
    public static class ExternalTextureCatalog
    {
        // Суффикс "_Ш_В" в конце имени (2–5 цифр на размер).
        private static readonly Regex SizeSuffix = new Regex(@"^(.*?)_(\d{2,5})_(\d{2,5})$");

        private const int DefaultTileMM = 800;

        /// <summary>Папка с внешними текстурами: рядом с exe в билде
        /// (<c>Build/Resources/Textures</c>), в корне проекта в редакторе.
        /// <c>Application.dataPath</c> = <c>*_Data</c> в билде и <c>Assets</c> в редакторе,
        /// поэтому родитель — папка exe / корень проекта.</summary>
        public static string DirectoryPath
        {
            get
            {
                var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(root, "Resources", "Textures");
            }
        }

        /// <summary>Пере-сканировать папку: сбрасывает прежние динамические декоры и
        /// кэш материалов, грузит все картинки заново. Возвращает число загруженных.
        /// Никогда не бросает — на любой сбой просто логирует и продолжает.</summary>
        public static int LoadAll()
        {
            MaterialCatalog.ClearDynamic();
            MaterialManager.ClearCache();

            var dir = DirectoryPath;
            int loaded = 0;
            try
            {
                if (!Directory.Exists(dir))
                {
                    // Создаём папку, чтобы пользователю было куда класть файлы.
                    Directory.CreateDirectory(dir);
                    Debug.Log($"[Textures] Папка внешних текстур создана: {dir}");
                    return 0;
                }

                foreach (var path in Directory.GetFiles(dir))
                {
                    if (!IsImage(path)) continue;
                    var def = LoadOne(path);
                    if (def != null) { MaterialCatalog.RegisterDynamic(def); loaded++; }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Textures] Сканирование '{dir}' не удалось: {e.Message}");
            }

            Debug.Log($"[Textures] Загружено внешних декоров: {loaded} (из {dir})");
            return loaded;
        }

        private static bool IsImage(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private static MaterialDef LoadOne(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!tex.LoadImage(bytes)) // ImageConversion: подхватывает PNG/JPG
                {
                    Debug.LogWarning($"[Textures] Не картинка/битый файл: {Path.GetFileName(path)}");
                    return null;
                }
                tex.wrapMode = TextureWrapMode.Repeat;

                var baseName = Path.GetFileNameWithoutExtension(path);
                tex.name = baseName;
                ParseName(baseName, out string display, out int tileW, out int tileH);

                // id = имя файла (стабильно для сейвов); цвет белый — текстура без тонировки.
                return new MaterialDef(baseName, display, "ЛДСП", Color.white, null, tileW)
                {
                    tileHeightMM = tileH,
                    texture = tex,
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Textures] {Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        }

        /// <summary>Разобрать имя файла (без расширения) на отображаемое имя и размер
        /// плитки. Суффикс <c>_Ш_В</c> отрезается; подчёркивания → пробелы. Чистая
        /// функция (тестируется без файловой системы).</summary>
        public static void ParseName(string baseName, out string display, out int tileWidthMM, out int tileHeightMM)
        {
            tileWidthMM = DefaultTileMM;
            tileHeightMM = DefaultTileMM;
            string name = baseName;

            var m = SizeSuffix.Match(baseName);
            if (m.Success)
            {
                name = m.Groups[1].Value;
                if (int.TryParse(m.Groups[2].Value, out int w) && w > 0) tileWidthMM = w;
                if (int.TryParse(m.Groups[3].Value, out int h) && h > 0) tileHeightMM = h;
            }

            display = name.Replace('_', ' ').Trim();
            if (string.IsNullOrEmpty(display)) display = baseName;
        }
    }
}
