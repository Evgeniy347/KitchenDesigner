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
            if (!AutoSaveIsOn()) return false;
            return SaveIntoTheOpenProjectOrAutosave();
        }

        private static bool AutoSaveIsOn()
        {
            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.AutoSave) return false;
            return DemoMode.Current.AutoSaveAllowed;
        }

        private static bool SaveIntoTheOpenProjectOrAutosave()
        {
            if (SaveLoadManager.HasLastPath)
                return SaveLoadManager.SaveToLastPath();
            return SaveLoadManager.SaveProject(AutoSaveName, backup: false);
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

                if (!AutoSaveIsOn()) continue;

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
