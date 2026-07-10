using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Автосохранение в проект "autosave" каждые 2 секунды, только если сцена
    /// изменилась с прошлого автосохранения (сравнение сериализованного состояния).
    /// </summary>
    public class AutoSaveManager : MonoBehaviour
    {
        public const string AutoSaveName = "autosave";
        private const float Interval = 2f;

        private string _lastSavedJson;

        private void OnEnable()
        {
            _lastSavedJson = SaveLoadManager.CaptureCurrentJson();
            StartCoroutine(AutoSaveLoop());
        }

        private IEnumerator AutoSaveLoop()
        {
            var wait = new WaitForSeconds(Interval);
            while (true)
            {
                yield return wait;

                var settings = KitchenSettings.Instance;
                if (settings == null || !settings.AutoSave) continue;

                string current = SaveLoadManager.CaptureCurrentJson();
                if (current == _lastSavedJson) continue; // нет изменений

                // Автосохранение без архивации (иначе zip плодились бы каждые 2с).
                if (SaveLoadManager.SaveProject(AutoSaveName, backup: false))
                {
                    _lastSavedJson = current;
                    Debug.Log("[AutoSave] Auto-saved changes");
                    if (UI.ToastNotification.Instance != null)
                        UI.ToastNotification.Instance.Show("Auto-saved", 1.5f);
                }
            }
        }
    }
}
