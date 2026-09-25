using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class LevelsWindowUI : MonoBehaviour, IProjectWindow
    {
        public static LevelsWindowUI? Instance { get; private set; }

        public string WindowId => "levels";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        internal const float PanelWidth = 340f;
        internal const float PanelHeight = 320f;
        internal const float RowHeight = 28f;
        internal const float RowsTopY = 118f;

        private GameObject? _root;
        private Transform? _rowsParent;
        private readonly List<GameObject> _rowObjects = new();

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("LevelsWindow", canvas, Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, UIStyle.DragStripHeight);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("LvTitle", panel.transform, "Этажи", 20,
                new Vector2(0, PanelHeight * 0.5f - 20f), new Vector2(PanelWidth - 20f, 28),
                TextAnchor.MiddleCenter);

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            var rowsRect = UIFactory.CreateRect("LvRows", panel.transform);
            _rowsParent = rowsRect;

            _root.SetActive(false);
        }

        public void Refresh()
        {
            ClearRows();
            if (_rowsParent == null) return;

            var levels = new List<Level>(LevelRegistry.Items);
            levels.Sort((a, b) => b.floorElevationMm.CompareTo(a.floorElevationMm));

            float y = PanelHeight * 0.5f - RowsTopY;
            foreach (var level in levels)
            {
                BuildRow(level, y);
                y -= RowHeight;
            }
        }

        private void BuildRow(Level level, float y)
        {
            if (_rowsParent == null) return;
            var label = UIFactory.CreateLabel("LvRow_" + level.id, _rowsParent,
                $"{level.name} · {level.floorElevationMm} мм", 15,
                new Vector2(0, y), new Vector2(PanelWidth - 20f, RowHeight - 4f), TextAnchor.MiddleLeft);
            _rowObjects.Add(label.gameObject);
        }

        private void ClearRows()
        {
            foreach (var go in _rowObjects)
                DestroyNow.The(go);
            _rowObjects.Clear();
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.SetActive(visible);
            if (visible) Refresh();
        }

        private void OnDestroy() => ProjectWindows.Unregister(this);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && IsVisible)
                TryHideFromEscape();
        }

        internal void TryHideFromEscape()
        {
            if (OwnsEscape()) SetVisible(false);
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                LevelsWindowOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
                HintOpen = HintBubbleUI.IsOpen,
                ConfirmArmed = ConfirmDeleteButton.AnyArmed,
                ContextMenuOpen = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen,
                GroupMenuOpen = GroupMenuUI.Instance != null && GroupMenuUI.Instance.IsOpen,
                CatalogTileSelected = SidebarUI.CatalogClaimsEscape,
                DayNightOpen = DayNightPanelUI.Instance != null && DayNightPanelUI.Instance.IsVisible,
                MusicOpen = MusicPanelUI.Instance != null && MusicPanelUI.Instance.IsVisible,
            }) == EscapeOwner.LevelsWindow;
    }
}
