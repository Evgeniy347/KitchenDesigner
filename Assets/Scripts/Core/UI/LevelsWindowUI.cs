using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class LevelsWindowUI : MonoBehaviour, IProjectWindow
    {
        public string WindowId => "levels";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        internal const float PanelWidth = 340f;
        internal const float MinPanelHeight = 320f;
        internal const float RowHeight = 28f;
        internal const float RowsTopY = 118f;
        internal const float AddButtonHeight = 30f;
        internal const float HeaderY = 96f;
        private const float BottomPad = 10f;
        private const float AddButtonGap = 8f;

        private const float NameW = 130f;
        private const float ElevationW = 65f;
        private const float HeightW = 65f;
        private const float DeleteW = 24f;
        private const float FieldGap = 3f;

        private GameObject? _root;
        private Transform? _rowsParent;
        private RectTransform? _addButtonRect;
        private readonly List<GameObject> _rowObjects = new();

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("LevelsWindow", canvas, Vector2.zero,
                new Vector2(PanelWidth, MinPanelHeight));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, UIStyle.DragStripHeight);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("LvTitle", panel.transform, "Этажи", 20,
                new Vector2(0, MinPanelHeight * 0.5f - 20f), new Vector2(PanelWidth - 20f, 28),
                TextAnchor.MiddleCenter);

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            BuildColumnHeaders(panel.transform);

            var rowsRect = UIFactory.CreateRect("LvRows", panel.transform);
            _rowsParent = rowsRect;

            var addButton = UIFactory.CreateButton("LvAdd", panel.transform, "+",
                new Vector2(0, -(MinPanelHeight * 0.5f) + AddButtonHeight * 0.5f + BottomPad),
                new Vector2(PanelWidth - 20f, AddButtonHeight), AddLevel);
            _addButtonRect = addButton.GetComponent<RectTransform>();

            _root.SetActive(false);

            LevelRegistry.Changed += OnLevelRegistryChanged;
        }

        private static float RowStartX() =>
            -(NameW + ElevationW + HeightW + DeleteW + FieldGap * 3f) * 0.5f;

        private void BuildColumnHeaders(Transform parent)
        {
            float x = RowStartX();
            float y = MinPanelHeight * 0.5f - HeaderY;

            UIFactory.CreateLabel("LvHeaderName", parent, "Имя", 12,
                new Vector2(x + NameW * 0.5f, y), new Vector2(NameW, 20), TextAnchor.MiddleCenter);
            x += NameW + FieldGap;

            UIFactory.CreateLabel("LvHeaderElevation", parent, "Отметка", 12,
                new Vector2(x + ElevationW * 0.5f, y), new Vector2(ElevationW, 20), TextAnchor.MiddleCenter);
            x += ElevationW + FieldGap;

            UIFactory.CreateLabel("LvHeaderHeight", parent, "Высота", 12,
                new Vector2(x + HeightW * 0.5f, y), new Vector2(HeightW, 20), TextAnchor.MiddleCenter);
        }

        public void Refresh()
        {
            ClearRows();
            if (_rowsParent == null || _root == null) return;

            var levels = new List<Level>(LevelRegistry.Items);
            levels.Sort((a, b) => b.floorElevationMm.CompareTo(a.floorElevationMm));

            float contentHeight = RowsTopY + levels.Count * RowHeight
                + AddButtonGap + AddButtonHeight + BottomPad;
            float panelHeight = Mathf.Max(MinPanelHeight, contentHeight);
            ((RectTransform)_root.transform).sizeDelta = new Vector2(PanelWidth, panelHeight);

            if (_addButtonRect != null)
                _addButtonRect.anchoredPosition = new Vector2(0,
                    -(panelHeight * 0.5f) + AddButtonHeight * 0.5f + BottomPad);

            float y = panelHeight * 0.5f - RowsTopY;
            foreach (var level in levels)
            {
                BuildRow(levels.Count, level, y);
                y -= RowHeight;
            }
        }

        private void BuildRow(int levelCount, Level level, float y)
        {
            if (_rowsParent == null) return;

            float x = RowStartX();

            var nameField = UIFactory.CreateInputField("LvName_" + level.id, _rowsParent, level.name,
                new Vector2(x + NameW * 0.5f, y), new Vector2(NameW, RowHeight - 4f));
            nameField.onEndEdit.AddListener(v => ApplyName(level, v));
            _rowObjects.Add(nameField.gameObject);
            x += NameW + FieldGap;

            var elevationField = UIFactory.CreateNumberField("LvElevation_" + level.id, _rowsParent,
                level.floorElevationMm.ToString(), new Vector2(x + ElevationW * 0.5f, y),
                new Vector2(ElevationW, RowHeight - 4f), "мм");
            elevationField.contentType = TMP_InputField.ContentType.IntegerNumber;
            elevationField.onEndEdit.AddListener(v => ApplyElevation(level, v, elevationField));
            _rowObjects.Add(elevationField.gameObject);
            x += ElevationW + FieldGap;

            var heightField = UIFactory.CreateNumberField("LvHeight_" + level.id, _rowsParent,
                level.heightMm.ToString(), new Vector2(x + HeightW * 0.5f, y),
                new Vector2(HeightW, RowHeight - 4f), "мм");
            heightField.contentType = TMP_InputField.ContentType.IntegerNumber;
            heightField.onEndEdit.AddListener(v => ApplyHeight(level, v, heightField));
            _rowObjects.Add(heightField.gameObject);
            x += HeightW + FieldGap;

            var deleteButton = UIFactory.CreateConfirmDeleteButton("LvDelete_" + level.id, _rowsParent,
                UIStyle.GlyphClose, new Vector2(x + DeleteW * 0.5f, y),
                new Vector2(DeleteW, RowHeight - 4f), () => DeleteLevel(level));
            _rowObjects.Add(deleteButton.gameObject);
            if (levelCount <= 1)
                deleteButton.interactable = false;
        }

        private static void ApplyName(Level level, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || newName == level.name) return;
            CommandStack.Execute(new RenameLevelCommand(level, newName));
        }

        private static void ApplyElevation(Level level, string text, TMP_InputField field)
        {
            if (int.TryParse(text, out int mm) && mm != level.floorElevationMm)
            {
                CommandStack.Execute(new SetLevelElevationCommand(level, mm));
                UIFactory.SetHighlight(field, false);
            }
            else if (!int.TryParse(text, out _))
            {
                UIFactory.SetErrorHighlight(field);
            }
            field.SetTextWithoutNotify(level.floorElevationMm.ToString());
        }

        private static void ApplyHeight(Level level, string text, TMP_InputField field)
        {
            if (int.TryParse(text, out int mm) && mm > 0 && mm != level.heightMm)
            {
                CommandStack.Execute(new SetLevelHeightCommand(level, mm));
                UIFactory.SetHighlight(field, false);
            }
            else if (!int.TryParse(text, out int parsed) || parsed <= 0)
            {
                UIFactory.SetErrorHighlight(field);
            }
            field.SetTextWithoutNotify(level.heightMm.ToString());
        }

        private void AddLevel()
        {
            var current = LevelRegistry.Snapshot();
            var next = LevelPlacement.NextAbove(current, KitchenSettings.Instance.ConstructionFloorHeightMm);

            if (LevelRegistry.Items.Count == 0)
            {
                var seedAndNext = new List<IUndoCommand>
                {
                    new CreateLevelCommand(current[0]),
                    new CreateLevelCommand(next),
                };
                CommandStack.Execute(new CompositeCommand($"Добавить уровень «{next.name}»", seedAndNext));
            }
            else
            {
                CommandStack.Execute(new CreateLevelCommand(next));
            }
        }

        internal void DeleteLevel(Level level)
        {
            if (LevelRegistry.IndexOf(level.id) < 0) return;
            CommandStack.Execute(new DeleteLevelCommand(level.id));
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

        private void OnLevelRegistryChanged()
        {
            if (IsVisible) Refresh();
        }

        private void OnDestroy()
        {
            LevelRegistry.Changed -= OnLevelRegistryChanged;
            ProjectWindows.Unregister(this);
        }

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
