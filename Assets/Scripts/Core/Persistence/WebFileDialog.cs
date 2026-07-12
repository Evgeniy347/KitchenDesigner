using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Браузерные файловые диалоги для WebGL-плеера (мост в FileDialog.jslib).
    ///   • <see cref="Save"/> — окно сохранения/скачивания файла
    ///     (showSaveFilePicker, иначе download-ссылка).
    ///   • <see cref="Open"/> — окно выбора файла
    ///     (showOpenFilePicker, иначе &lt;input type=file&gt;).
    /// Результат приходит асинхронно в <see cref="WebFileDialogReceiver"/>.
    /// На desktop/в редакторе используется <see cref="NativeFileDialog"/> —
    /// эти методы там только пишут предупреждение.
    /// </summary>
    public static class WebFileDialog
    {
        /// <summary>Разделитель «имя\x1Fсодержимое» в колбэке открытия (см. jslib).</summary>
        internal const char PayloadSeparator = '\x1F';

        /// <summary>Имя GameObject-приёмника, по которому JS шлёт SendMessage.</summary>
        internal const string ReceiverName = "WebFileDialogReceiver";

        private const string DefaultName = "kitchen.json";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FileDialogOpen(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void FileDialogSave(string gameObjectName, string content, string defaultName);
#endif

        /// <summary>Открыть браузерный выбор файла. Колбэк: (имя файла, содержимое).</summary>
        public static void Open(Action<string, string> onOpened)
        {
            WebFileDialogReceiver.GetOrCreate().PendingOpen = onOpened;
#if UNITY_WEBGL && !UNITY_EDITOR
            FileDialogOpen(ReceiverName);
#else
            Debug.LogWarning("[WebFileDialog] Доступно только в WebGL-плеере");
#endif
        }

        /// <summary>Открыть браузерное окно сохранения/скачивания файла.</summary>
        public static void Save(string content, string defaultName, Action<string> onSaved = null)
        {
            WebFileDialogReceiver.GetOrCreate().PendingSaved = onSaved;
#if UNITY_WEBGL && !UNITY_EDITOR
            FileDialogSave(ReceiverName, content ?? "",
                string.IsNullOrEmpty(defaultName) ? DefaultName : defaultName);
#else
            Debug.LogWarning("[WebFileDialog] Доступно только в WebGL-плеере");
#endif
        }

        /// <summary>
        /// Разбирает полезную нагрузку колбэка открытия вида «имя\x1Fсодержимое».
        /// Возвращает false, если строка пустая или в ней нет разделителя.
        /// </summary>
        internal static bool TryParseOpenPayload(string payload, out string fileName, out string content)
        {
            fileName = null;
            content = null;
            if (string.IsNullOrEmpty(payload)) return false;

            int sep = payload.IndexOf(PayloadSeparator);
            if (sep < 0) return false;

            fileName = payload.Substring(0, sep);
            content = payload.Substring(sep + 1);
            return true;
        }

        /// <summary>
        /// Имя файла по умолчанию для «Сохранить как»: имя последнего файла,
        /// если оно похоже на .json, иначе «kitchen.json».
        /// </summary>
        internal static string SuggestedFileName(string lastPath)
        {
            if (!string.IsNullOrEmpty(lastPath))
            {
                var name = System.IO.Path.GetFileName(lastPath);
                if (!string.IsNullOrEmpty(name) &&
                    name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            return DefaultName;
        }
    }
}
