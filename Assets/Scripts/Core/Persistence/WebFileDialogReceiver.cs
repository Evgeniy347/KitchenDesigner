using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Скрытый приёмник колбэков от FileDialog.jslib: JS вызывает
    /// SendMessage(ReceiverName, "OnWebGLFileOpened"/"OnWebGLFileSaved", …).
    /// Колбэки одноразовые — сбрасываются сразу после вызова.
    /// </summary>
    public class WebFileDialogReceiver : MonoBehaviour
    {
        public Action<string, string>? PendingOpen;
        public Action<string>? PendingSaved;

        private static WebFileDialogReceiver? _instance;

        internal static WebFileDialogReceiver GetOrCreate()
        {
            if (_instance != null) return _instance;
            var go = new GameObject(WebFileDialog.ReceiverName);
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WebFileDialogReceiver>();
            return _instance;
        }

        /// <summary>Вызывается из JS: полезная нагрузка «имя\x1Fсодержимое».</summary>
        public void OnWebGLFileOpened(string payload)
        {
            var cb = PendingOpen;
            PendingOpen = null;
            if (cb == null) return;
            if (WebFileDialog.TryParseOpenPayload(payload, out var fileName, out var content))
                cb(fileName!, content!);
        }

        /// <summary>Вызывается из JS после успешного сохранения: имя файла.</summary>
        public void OnWebGLFileSaved(string fileName)
        {
            var cb = PendingSaved;
            PendingSaved = null;
            cb?.Invoke(fileName);
        }
    }
}
