using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Автосохранение в проект "autosave" с интервалом из настроек
    /// (`KitchenSettings.AutoSaveInterval`, сек), только если сцена изменилась с
    /// прошлого автосохранения (сравнение сериализованного состояния).
    /// </summary>
    public class AutoSaveManager : MonoBehaviour
    {
        public const string AutoSaveName = "autosave";

        private string _lastSavedJson;

        private void OnEnable()
        {
            _lastSavedJson = SaveLoadManager.CaptureCurrentJson();
            StartCoroutine(AutoSaveLoop());
        }

        private IEnumerator AutoSaveLoop()
        {
            while (true)
            {
                // Интервал перечитывается каждую итерацию — изменения настройки
                // применяются на лету.
                var settings = KitchenSettings.Instance;
                float interval = settings != null ? Mathf.Max(1f, settings.AutoSaveInterval) : 60f;
                yield return new WaitForSeconds(interval);

                settings = KitchenSettings.Instance;
                if (settings == null || !settings.AutoSave) continue;

                string current = SaveLoadManager.CaptureCurrentJson();
                if (current == _lastSavedJson) continue; // нет изменений

                // Автосохранение без архивации (иначе zip плодились бы каждые 2с).
                if (SaveLoadManager.SaveProject(AutoSaveName, backup: false))
                {
                    _lastSavedJson = current;
                    if (UI.ToastNotification.Instance != null)
                        UI.ToastNotification.Instance.Show("Auto-saved", 1.5f);
                }
            }
        }
    }
}
