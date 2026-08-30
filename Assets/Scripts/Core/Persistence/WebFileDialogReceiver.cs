using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
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

        public void OnWebGLFileOpened(string payload)
        {
            var cb = PendingOpen;
            PendingOpen = null;
            if (cb == null) return;
            if (WebFileDialog.TryParseOpenPayload(payload, out var fileName, out var content))
                cb(fileName!, content!);
        }

        public void OnWebGLFileSaved(string fileName)
        {
            var cb = PendingSaved;
            PendingSaved = null;
            cb?.Invoke(fileName);
        }
    }
}
