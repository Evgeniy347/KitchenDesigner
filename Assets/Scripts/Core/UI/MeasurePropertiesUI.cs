using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core.UI
{
    public class MeasurePropertiesUI : MonoBehaviour
    {
        private const float PanelWidth = 300f;
        private const float PanelHeight = 236f;
        internal const float RowWidth = PanelWidth - UIStyle.WindowPad * 2f;
        private const float RowHeight = 22f;
        internal const float DeleteButtonWidth = 120f;
        private static readonly Vector2 LeftOfTheDayNightPanel = new Vector2(-320, -60);

        private GameObject? _root;
        private TMP_Text? _pointA;
        private TMP_Text? _pointB;
        private TMP_Text? _distance;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("MeasurePanel", canvas, Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = LeftOfTheDayNightPanel;
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, UIStyle.DragStripHeight);

            UIFactory.CreateLabel("MeasureTitle", panel.transform, "Замер", UIStyle.FontTitle,
                new Vector2(0, PanelHeight * 0.5f - 26f), new Vector2(RowWidth, 28f),
                TextAnchor.MiddleCenter);

            float y = PanelHeight * 0.5f - 62f;
            _pointA = Row(panel.transform, "MeasureA", ref y);
            _pointB = Row(panel.transform, "MeasureB", ref y);

            y -= UIStyle.GapSection;
            _distance = Row(panel.transform, "MeasureDistance", ref y);

            UIFactory.CreateDangerButton("MeasureDelete", panel.transform, "Удалить",
                new Vector2(RowWidth * 0.5f - DeleteButtonWidth * 0.5f, y - UIStyle.GapSection),
                new Vector2(DeleteButtonWidth, UIStyle.HitTarget), DeleteSelected);

            UIFactory.CreateCloseButton(panel.transform, () => MeasureStore.Select(null));

            MeasureStore.Changed += Refresh;
            _root.SetActive(false);
        }

        private static TMP_Text Row(Transform parent, string name, ref float y)
        {
            var label = UIFactory.CreateLabel(name, parent, "", UIStyle.FontBody,
                new Vector2(0, y), new Vector2(RowWidth, RowHeight), TextAnchor.MiddleLeft);
            y -= RowHeight + UIStyle.GapInner;
            return label;
        }

        private void OnDestroy() => MeasureStore.Changed -= Refresh;

        private void Refresh()
        {
            var seg = MeasureStore.Selected;
            if (_root == null) return;

            if (seg == null)
            {
                _root.SetActive(false);
                return;
            }

            if (_pointA != null) _pointA.text = "Точка A: " + CoordsInWholeMm(seg.A);
            if (_pointB != null) _pointB.text = "Точка B: " + CoordsInWholeMm(seg.B);
            if (_distance != null)
                _distance.text = "Расстояние: " +
                    MeasureGeometry.FormatMm((seg.B - seg.A).magnitude, seg.Axis >= 0);

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        private static string CoordsInWholeMm(Vector3 world) =>
            $"X {RoundToWholeMm(world.x)}, Y {RoundToWholeMm(world.y)}, Z {RoundToWholeMm(world.z)} мм";

        private static int RoundToWholeMm(float units) => Mathf.RoundToInt(MeasureGeometry.ToMm(units));

        private void DeleteSelected()
        {
            var seg = MeasureStore.Selected;
            if (seg != null) MeasureStore.Remove(seg);
        }
    }
}
