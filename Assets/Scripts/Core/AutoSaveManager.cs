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

        /// <summary>При закрытии программы, если включено автосохранение —
        /// гарантированно пишем текущую сцену в файл (корутина могла не успеть).</summary>
        private void OnApplicationQuit()
        {
            SaveOnQuit();
        }

        /// <summary>Сохранить проект автосохранения при выходе, если включена
        /// настройка AutoSave. Возвращает true, если файл записан. Чистый метод —
        /// тестируется без жизненного цикла MonoBehaviour.</summary>
        public static bool SaveOnQuit()
        {
            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.AutoSave) return false;
            return SaveLoadManager.SaveProject(AutoSaveName, backup: false);
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
                    if (UI.AutoSaveIndicator.Instance != null)
                        UI.AutoSaveIndicator.Instance.NotifySaved();
                }
            }
        }
    }
}
