using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core.UI
{
    public class MeasurePropertiesUI : MonoBehaviour
    {
        internal const float FirstColumnW = 40f;
        private const float PointRows = 2f;

        private WindowChrome? _chrome;
        private TMP_Text? _distance;
        private DataTable? _points;

        internal float BodyWidth => _chrome?.BodyWidth ?? 0f;
        internal DataTable? Points => _points;

        public bool IsVisible => _chrome != null && _chrome.Panel.gameObject.activeSelf;

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "MeasurePanel", Loc.T("measure.title"),
                new Vector2(UIStyle.ToolPanelW, UIStyle.ToolPanelW), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = () => MeasureStore.Select(null),
                    HasFooter = true,
                    RuledHeader = true,
                });
            var panel = _chrome.Panel;
            UIFactory.AnchorTopRight(panel);
            panel.anchoredPosition = new Vector2(-(UIStyle.ToolPanelW + 2f * UIStyle.Space3),
                -(UIStyle.ToolbarH + UIStyle.Space3));

            var body = _chrome.CreateBody();
            var stack = new VerticalStack(body.Content, _chrome.BodyWidth);
            _distance = UIFactory.CreateLabel("MeasureDistance", body.Content, "", UIStyle.FontDisplay, Vector2.zero,
                new Vector2(_chrome.BodyWidth, UIStyle.FontDisplay + UIStyle.Space2), TextAnchor.MiddleLeft);
            _distance.enableWordWrapping = false;
            stack.Place(_distance.rectTransform);
            stack.Gap(UIStyle.Space2);

            _points = DataTable.Create(body.Content, "MeasurePoints",
                new Vector2(_chrome.BodyWidth, UIStyle.TableHeaderH + PointRows * UIStyle.TableRowH), new[]
                {
                    new DataColumn("point", "", FirstColumnW),
                    new DataColumn("x", "X", 0f, CellAlign.Right),
                    new DataColumn("y", "Y", 0f, CellAlign.Right),
                    new DataColumn("z", "Z", 0f, CellAlign.Right),
                });
            stack.Place(_points.Root);

            _chrome.Footer!.AddRight("MeasureDelete", Loc.T("measure.delete"), DeleteSelected,
                ButtonRole.DangerOutline);
            _chrome.FitHeightTo(stack.Height);
            body.Fit();

            MeasureStore.Changed += Refresh;
            panel.gameObject.SetActive(false);
        }

        private void OnDestroy() => MeasureStore.Changed -= Refresh;

        private static bool IsGone(RectTransform panel) => panel == null;

        private void Refresh()
        {
            var seg = MeasureStore.Selected;
            if (_chrome == null || IsGone(_chrome.Panel)) return;
            var root = _chrome.Panel.gameObject;

            if (seg == null)
            {
                root.SetActive(false);
                return;
            }

            if (_distance != null)
                _distance.text = MeasureGeometry.FormatMm((seg.B - seg.A).magnitude, seg.Axis >= 0);
            _points?.SetRows(new List<DataRow>
            {
                PointRow(Loc.T("measure.pointShortA"), seg.A),
                PointRow(Loc.T("measure.pointShortB"), seg.B),
            });

            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        private static DataRow PointRow(string name, Vector3 world) => DataRow.Item(name,
            WholeMm(world.x), WholeMm(world.y), WholeMm(world.z));

        private static string WholeMm(float units) =>
            NumberFormat.Integer(Mathf.RoundToInt(MeasureGeometry.ToMm(units)));

        private void DeleteSelected()
        {
            var seg = MeasureStore.Selected;
            if (seg != null) MeasureStore.Remove(seg);
        }
    }
}
