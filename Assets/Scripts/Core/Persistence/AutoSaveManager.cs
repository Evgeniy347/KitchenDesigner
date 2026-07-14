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

        private string _lastSavedJson = null!;

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

        /// <summary>Сохранить при выходе, если включена настройка AutoSave.
        /// Возвращает true, если файл записан. Чистый метод — тестируется без
        /// жизненного цикла MonoBehaviour.</summary>
        public static bool SaveOnQuit()
        {
            var settings = KitchenSettings.Instance;
            if (settings == null || !settings.AutoSave) return false;
            return SaveActiveTarget();
        }

        /// <summary>Записать текущую сцену в АКТИВНУЮ цель сохранения: открытый
        /// пользователем файл (его и грузит <see cref="SaveLoadManager.LoadLastSession"/>
        /// при старте), иначе — в проект автосохранения. Без этого правки при
        /// закрытии уходили в отдельный autosave-файл и не подхватывались при
        /// следующем запуске (грузился исходный открытый файл).</summary>
        private static bool SaveActiveTarget()
        {
#if UNITY_WEBGL
            var api = Object.FindAnyObjectByType<Networking.ProjectApiClient>();
            if (api != null && Networking.ProjectApiClient.Enabled && api.HasCurrentProject)
            {
                string json = SaveLoadManager.CaptureCurrentJson();
                api.SaveCurrent(json, null, null);
                return true;
            }
            // Server save disabled or no project — fall back to local persistent save.
            if (SaveLoadManager.HasLastPath)
                return SaveLoadManager.SaveToLastPath();
            return SaveLoadManager.SaveProject(AutoSaveName, backup: false);
#else
            if (SaveLoadManager.HasLastPath)
                return SaveLoadManager.SaveToLastPath();
            return SaveLoadManager.SaveProject(AutoSaveName, backup: false);
#endif
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

                // Пишем в открытый файл (его грузит старт), иначе — в проект
                // автосохранения. Без архивации (иначе zip плодились бы постоянно).
                if (SaveActiveTarget())
                {
                    _lastSavedJson = current;
                    if (UI.AutoSaveIndicator.Instance != null)
                        UI.AutoSaveIndicator.Instance.NotifySaved();
                }
            }
        }
    }
}
