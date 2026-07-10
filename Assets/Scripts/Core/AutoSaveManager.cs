using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Периодическое автосохранение в проект "autosave", если включено в настройках.
    /// </summary>
    public class AutoSaveManager : MonoBehaviour
    {
        public const string AutoSaveName = "autosave";

        private void OnEnable()
        {
            StartCoroutine(AutoSaveLoop());
        }

        private IEnumerator AutoSaveLoop()
        {
            while (true)
            {
                var settings = KitchenSettings.Instance;
                int interval = settings != null ? settings.AutoSaveInterval : 60;
                yield return new WaitForSeconds(Mathf.Max(10, interval));

                if (settings != null && settings.AutoSave)
                {
                    if (SaveLoadManager.SaveProject(AutoSaveName))
                        Debug.Log("[AutoSave] 💾 " + AutoSaveName);
                }
            }
        }
    }
}
