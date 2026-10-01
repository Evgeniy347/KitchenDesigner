using System.Collections;
using UnityEngine;
using KitchenDesigner.Core.Update;

namespace KitchenDesigner.Core
{
    public class AutoSaveManager : MonoBehaviour
    {
        public const string AutoSaveName = "autosave";

        private string _lastSavedJson = string.Empty;
        private static readonly AutoSaveRevisionGate Gate = new AutoSaveRevisionGate(0);

        public static void MarkSaved() => Gate.MarkSaved(ProjectRevision());

        internal static int ProjectRevision()
        {
            var settings = KitchenSettings.Instance;
            if (settings != null) ProjectDirty.NoteSettings(JsonUtility.ToJson(settings.ToData()));
            return unchecked(SceneRevision.Version + ProjectDirty.Version);
        }

        private void OnEnable()
        {
            _lastSavedJson = SaveLoadManager.CaptureCurrentJson();
            MarkSaved();
            StartCoroutine(AutoSaveLoop());
        }

        private void OnApplicationQuit()
        {
            SaveOnQuit();
        }

        public static bool SaveOnQuit()
        {
            if (!AutoSaveIsOn()) return false;
            return SaveIntoTheOpenProjectOrAutosave(SaveLoadManager.CaptureCurrentJson());
        }

        private static bool AutoSaveIsOn()
        {
            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.AutoSave) return false;
            return DemoMode.Current.AutoSaveAllowed;
        }

        private static bool SaveIntoTheOpenProjectOrAutosave(string json)
        {
            if (SaveLoadManager.HasLastPath)
                return SaveLoadManager.SaveCapturedJsonToPath(SaveLoadManager.LastPath, json);
            return SaveLoadManager.SaveCapturedJsonAsProject(AutoSaveName, json, backup: false);
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

                var outcome = Gate.Run(
                    ProjectRevision(),
                    SaveLoadManager.CaptureCurrentJson,
                    SaveIntoTheOpenProjectOrAutosave,
                    _lastSavedJson);
                _lastSavedJson = outcome.LastSavedJson;

                if (outcome.Wrote)
                    UI.StatusBarUI.Instance?.ShowTransient(
                        Loc.T("status.saved") + System.DateTime.Now.ToString("HH:mm:ss"),
                        StatusLevel.Success);
            }
        }
    }
}
