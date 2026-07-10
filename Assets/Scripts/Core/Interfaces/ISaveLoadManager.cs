using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface ISaveLoadManager
    {
        string LastPath { get; set; }
        bool HasLastPath { get; }
        string LastDirectory { get; }
        bool SaveToPath(string path);
        bool SaveToLastPath();
        bool LoadFromPath(string path);
        bool LoadLastSession();
        ProjectData CaptureScene(IEnumerable<KitchenElement> elements);
        string Serialize(ProjectData data);
        ProjectData Deserialize(string json);
        bool IsVersionCompatible(ProjectData data);
        List<GameObject> RestoreScene(ProjectData data);
        bool SaveToFile(string path, ProjectData data);
        ProjectData LoadFromFile(string path);
        bool SaveProject(string name, bool backup = true);
        string CaptureCurrentJson();
        bool LoadProject(string name);
        string[] GetSaveFiles();
        string PathForName(string name);
        void ClearBoards(System.Collections.Generic.IEnumerable<KitchenElement> elements);
    }
}
