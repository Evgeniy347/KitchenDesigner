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
        private TMP_Text? _distanceUnit;
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
            _distanceUnit = UIFactory.CreateLabel("MeasureUnit", body.Content, Loc.T("unit.mm"), UIStyle.FontBody,
                Vector2.zero, new Vector2(0f, _distance.rectTransform.sizeDelta.y), TextAnchor.LowerLeft);
            _distanceUnit.color = UIStyle.TextSecondary;
            _distanceUnit.enableWordWrapping = false;
            _distanceUnit.raycastTarget = false;
            _distanceUnit.margin = new Vector4(0f, 0f, 0f, UIStyle.Space1);
            var unitRect = _distanceUnit.rectTransform;
            unitRect.anchorMin = unitRect.anchorMax = unitRect.pivot = new Vector2(0f, 1f);
            unitRect.anchoredPosition = _distance.rectTransform.anchoredPosition;
            unitRect.sizeDelta = new Vector2(_distanceUnit.GetPreferredValues(_distanceUnit.text).x + 1f,
                _distance.rectTransform.sizeDelta.y);
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

            ShowDistance(seg);
            _points?.SetRows(new List<DataRow>
            {
                PointRow(Loc.T("measure.pointShortA"), seg.A),
                PointRow(Loc.T("measure.pointShortB"), seg.B),
            });

            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        private void ShowDistance(MeasureSegment seg)
        {
            if (_distance == null || _distanceUnit == null) return;
            string number = NumberFormat.Integer(Mathf.RoundToInt(MeasureGeometry.ToMm((seg.B - seg.A).magnitude)));
            _distance.text = seg.Axis >= 0 ? number : UIStyle.GlyphAngle + " " + number;
            var unit = _distanceUnit.rectTransform;
            unit.anchoredPosition = new Vector2(_distance.GetPreferredValues(_distance.text).x + UIStyle.Space2,
                unit.anchoredPosition.y);
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
