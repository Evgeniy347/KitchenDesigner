#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class PublisherMigrationStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void MoveBeforeAnythingReadsTheStores()
        {
            var log = new PublisherMigrationLog(Debug.Log, Debug.LogWarning, Debug.LogError);
            var product = Application.productName;
            PublisherMigration.Run(
                new RegistryPublisherStore("Software", PublisherRename.OldCompany, Application.companyName, product), log);
            var localLow = Directory.GetParent(Application.persistentDataPath)?.Parent?.FullName;
            if (localLow == null) return;
            PublisherMigration.Run(
                new FolderPublisherStore(localLow, PublisherRename.OldCompany, Application.companyName, product), log);
        }
    }
}
#endif
