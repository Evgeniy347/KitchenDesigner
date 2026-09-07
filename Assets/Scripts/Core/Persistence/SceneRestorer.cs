using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class SceneRestorer
    {
        public static List<GameObject> Restore(ProjectData data)
        {
            using var batch = HighlightBatch.Open();

            var created = new List<GameObject>();
            if (data == null || data.elements == null) return created;

            ElementNameNormalizer.NormalizeAndRemapLinks(data.elements);
            RestoreGroups(data.groups);

            var resolved = new List<KitchenElement?>();
            foreach (var ed in data.elements)
            {
                if (ed == null) { resolved.Add(null); continue; }
                var go = ElementRestorers.Restore(ElementFactory.Instance, ed);
                created.Add(go);
                resolved.Add(go.GetComponent<KitchenElement>());
            }

            RestoreProjectState(data);
            SnapElementEdgesToMillimetreGrid(resolved);
            ScrewLegHostLink.ApplyAll(PartRegistry.GetAll());

            MigrateEdgeStates(data, resolved);

            CommandStack.Instance.Import(data.undoHistory, data.redoHistory,
                i => (i >= 0 && i < resolved.Count) ? resolved[i]! : null!);

            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();

            return created;
        }

        private static void MigrateEdgeStates(ProjectData data, List<KitchenElement?> resolved)
        {
            int migrated = EdgeStateMigration.Apply(data.elements, resolved);
            if (migrated > 0)
                Debug.Log($"[Кромки] Явные состояния торцов перенесены по расчёту: {migrated} дет.");
        }

        private static void RestoreGroups(GroupData[]? groups)
        {
            GroupManager.Clear();
            if (groups == null) return;
            foreach (var gd in groups)
                if (gd != null)
                    GroupManager.Register(gd.id, gd.name, gd.movable,
                        string.IsNullOrEmpty(gd.widthAxis) ? "x" : gd.widthAxis);
        }

        private static void RestoreProjectState(ProjectData data)
        {
            if (data.camera.valid && CameraController.Instance != null)
                CameraController.Instance.SetState(data.camera);

            if (!string.IsNullOrEmpty(data.handleMode) &&
                System.Enum.TryParse<ResizeHandleManager.HandleMode>(data.handleMode, out var mode))
                ResizeHandleManager.SetMode(mode);

            if (data.basePlateValid && data.basePlate != null)
                RestoreBasePlate(data.basePlate);

            if (data.settings != null)
                KitchenSettings.Instance.ApplyFrom(data.settings);

            ElementHighlighter.TintEnabled = data.tintEnabled;
            LightSourceElement.SetGlobalOn(data.lightsOn);
            Audio.MusicState.Track = data.musicTrack;
            Audio.MusicState.VolumePct = data.musicVolumePct;
            ProjectWindows.Apply(data.windows);

            ProjectInstructions.Text = data.projectInstructions ?? "";
            ProjectRooms.Set(data.rooms);
            ProjectFloorplans.Set(data.floorplans);
        }

        private static void SnapElementEdgesToMillimetreGrid(List<KitchenElement?> elements)
        {
            int snapped = 0;
            foreach (var el in elements)
                if (el != null && MmGrid.Snap(el)) snapped++;
            if (snapped > 0)
                Debug.Log($"[MmGrid] Выровнено по миллиметровой сетке: {snapped} дет.");
        }

        private static void RestoreBasePlate(ElementData data)
        {
            if (data == null) return;
            var floorGo = GameObject.FindWithTag("Floor");
            var element = floorGo != null
                ? ExistingBasePlateElement(floorGo)
                : BasePlate.Create().Element;
            if (element == null) return;
            element.DimensionsMM = data.Dimensions;
            element.transform.position = data.Position;
            element.transform.rotation = data.Rotation;
            element.Movable = data.movable;
            element.Transparent = data.transparent;
            MaterialManager.ApplyById(element, data.materialId);
        }

        private static KitchenElement? ExistingBasePlateElement(GameObject floorGo)
        {
            var bp = floorGo.GetComponent<BasePlate>();
            var element = bp != null ? (bp.Element ?? bp.GetComponent<KitchenElement>()) : null;
            return element != null ? element : floorGo.GetComponent<KitchenElement>();
        }
    }
}
