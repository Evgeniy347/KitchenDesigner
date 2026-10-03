using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class LoadProjectWindowUI : MonoBehaviour, IProjectWindow
    {
        private const float PanelW = 720f;
        private const float PreferredPanelH = 480f;
        private const float TitleH = 40f;
        private const float TitleTopPad = 6f;
        private const float BelowTitleGap = UIStyle.GapSection;
        private const float ButtonColumnW = 180f;
        private const float ColumnGap = 16f;
        private const float ButtonH = 40f;
        private const float ButtonGap = 10f;
        private const float BottomPad = UIStyle.WindowPad;
        private const float RowHeightFloor = LoadProjectRowsView.RowHeight;
        private const float ScrollbarReserve = WindowBody.BarW + ScrollArea.BarInset;

        private readonly ProjectFileActions _actions = new();
        private readonly LoadProjectRowsView _rows = new();

        private GameObject? _root;
        private RectTransform? _panel;
        private RectTransform? _body;
        private Canvas? _canvas;

        public string WindowId => "loadProject";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;
        public bool IsVisible => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            _canvas = canvas.GetComponentInParent<Canvas>();

            var panel = UIFactory.CreatePanel("LoadProjectWindow", canvas, Vector2.zero,
                new Vector2(PanelW, PreferredPanelH));
            _panel = panel.rectTransform;
            UIFactory.AnchorCenter(_panel);
            _panel.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            WindowDrag.Attach(_panel, UIStyle.DragStripHeight);

            WindowTitle.Create(panel.transform, "LoadTitle", Loc.T("window.load.title"),
                UIStyle.FontWindowTitle, PanelW - 2 * UIStyle.WindowPad, TitleH);

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            BuildButtonColumn(panel.transform);

            _body = UIFactory.CreateRect("LoadBody", panel.transform);

            _root.SetActive(false);
        }

        private void BuildButtonColumn(Transform parent)
        {
            float top = -(TitleTopPad + TitleH + BelowTitleGap);
            float buttonColumnX = PanelW - UIStyle.WindowPad - ButtonColumnW;

            var newProjectBtn = UIFactory.CreateButton("LoadNewProject", parent, Loc.T("window.load.newProject"),
                Vector2.zero, new Vector2(ButtonColumnW, ButtonH), OnNewProject);
            PlaceInButtonColumn(newProjectBtn.GetComponent<RectTransform>(), buttonColumnX, top);

            var loadBtn = UIFactory.CreateButton("LoadOpenFile", parent, Loc.T("window.load.open"),
                Vector2.zero, new Vector2(ButtonColumnW, ButtonH), OnLoadFile);
            PlaceInButtonColumn(loadBtn.GetComponent<RectTransform>(), buttonColumnX,
                top - ButtonH - ButtonGap);

            float separatorX = buttonColumnX - ColumnGap * 0.5f;
            var separator = UIFactory.CreatePanel("LoadColumnSeparator", parent, Vector2.zero,
                new Vector2(1, 0), UIStyle.Separator);
            var sepRt = separator.rectTransform;
            sepRt.anchorMin = new Vector2(0, 0);
            sepRt.anchorMax = new Vector2(0, 1);
            sepRt.pivot = new Vector2(0, 0.5f);
            sepRt.offsetMin = new Vector2(separatorX, BottomPad);
            sepRt.offsetMax = new Vector2(separatorX, top);
            separator.raycastTarget = false;
        }

        private static void PlaceInButtonColumn(RectTransform rt, float x, float topOffset)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, topOffset);
        }

        private void OnNewProject() => _actions.NewProjectDialog(ok => { if (ok) SetVisible(false); });

        private void OnLoadFile() => _actions.LoadDialog(ok => { if (ok) SetVisible(false); });

        private void OpenRecent(string path) =>
            _actions.OpenExisting(path, ok => { if (ok) SetVisible(false); });

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root == null || _panel == null || _body == null) return;
            if (visible) Refresh();
            _root.SetActive(visible);
        }

        private void Refresh()
        {
            float availableScreenHeight = _canvas != null
                ? ((RectTransform)_canvas.transform).rect.height
                : PreferredPanelH * 2f;
            float height = LoadWindowLayout.HeightFor(PreferredPanelH, availableScreenHeight);
            _panel!.sizeDelta = new Vector2(PanelW, height);

            float buttonColumnX = PanelW - UIStyle.WindowPad - ButtonColumnW;
            float listRightEdge = buttonColumnX - ColumnGap;
            float rowWidth = listRightEdge - UIStyle.WindowPad - ScrollbarReserve;
            float topOfBody = TitleTopPad + TitleH + BelowTitleGap;
            float columnHeight = Mathf.Max(RowHeightFloor, height - topOfBody - BottomPad);

            _body!.anchorMin = new Vector2(0, 1);
            _body.anchorMax = new Vector2(0, 1);
            _body.pivot = new Vector2(0, 1);
            _body.sizeDelta = new Vector2(rowWidth + ScrollbarReserve, columnHeight);
            _body.anchoredPosition = new Vector2(UIStyle.WindowPad, -topOfBody);

            for (int i = _body.childCount - 1; i >= 0; i--)
                DestroyNow.The(_body.GetChild(i).gameObject);

            var body = WindowBody.Create(_body, 0f, 0f, 0f);
            _rows.Rebuild(body.Content, rowWidth, RecentProjects.Paths(), OpenRecent);
            body.Fit();
        }
    }
}
