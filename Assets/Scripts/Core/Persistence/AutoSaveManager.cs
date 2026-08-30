using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class AutoSaveManager : MonoBehaviour
    {
        public const string AutoSaveName = "autosave";

        private string _lastSavedJson = string.Empty;

        private void OnEnable()
        {
            _lastSavedJson = SaveLoadManager.CaptureCurrentJson();
            StartCoroutine(AutoSaveLoop());
        }

        private void OnApplicationQuit()
        {
            SaveOnQuit();
        }

        public static bool SaveOnQuit()
        {
            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.AutoSave) return false;
            return SaveIntoTheOpenProjectOrAutosave();
        }

        private static bool SaveIntoTheOpenProjectOrAutosave()
        {
#if UNITY_WEBGL
            var api = Object.FindAnyObjectByType<Networking.ProjectApiClient>();
            if (api != null && Networking.ProjectApiClient.Enabled && api.HasCurrentProject)
            {
                string json = SaveLoadManager.CaptureCurrentJson();
                api.SaveCurrent(json, () => { }, _ => { });
                return true;
            }
            if (SaveLoadManager.HasLastPath)
                return SaveLoadManager.SaveToLastPath();
            return SaveLoadManager.SaveProject(AutoSaveName, backup: false);
#else
            if (SaveLoadManager.HasLastPath)
                return SaveLoadManager.SaveToLastPath();
            return SaveLoadManager.SaveProject(AutoSaveName, backup: false);
#endif
        }

        private static float IntervalSecondsRightNow()
        {
            var settings = KitchenSettings.Instance;
            return settings != null ? Mathf.Max(1f, settings.AutoSaveInterval) : 60f;
        }

        private IEnumerator AutoSaveLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(IntervalSecondsRightNow());

                var settings = KitchenSettings.Instance;
                if (settings == null || !settings.AutoSave) continue;

                string current = SaveLoadManager.CaptureCurrentJson();
                if (current == _lastSavedJson) continue;

                if (SaveIntoTheOpenProjectOrAutosave())
                {
                    _lastSavedJson = current;
                    UI.StatusBarUI.Instance?.ShowTransient(
                        "Сохранено: " + System.DateTime.Now.ToString("HH:mm:ss"),
                        new Color(0.45f, 0.85f, 0.45f, 1f));
                }
            }
        }
    }
}
