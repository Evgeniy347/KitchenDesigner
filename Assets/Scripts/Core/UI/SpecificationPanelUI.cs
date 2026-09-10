using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SpecificationPanelUI : MonoBehaviour, IProjectWindow
    {
        internal const float ColName = 40f;
        internal const float ColW = 250f;
        internal const float ColH = 305f;
        internal const float ColD = 360f;
        internal const float ColMaterial = 420f;
        internal const float ColPieces = 580f;
        internal const float ColQty = 630f;
        internal const float ColUnit = 730f;
        private const float ContentWidth = 840f;
        private const float ViewportHeight = 480f;
        private const float ViewportCenterY = -30f;
        internal const int MaxNameChars = 22;
        internal const int MaxMaterialChars = 15;
        internal const int MaxSectionChars = 60;
        internal const float ScrollbarWidth = 8f;

        private GameObject? _root;
        private TMP_Text? _content;
        private RectTransform? _contentRect;
        private ScrollRect? _scrollRect;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SpecPanel", canvas, Vector2.zero, new Vector2(860, 640));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("SpecTitle", panel.transform, "Спецификация", UIStyle.FontWindowTitle,
                new Vector2(0, 290), new Vector2(ContentWidth, 36), TextAnchor.MiddleCenter);

            BuildStaticHeaders(panel.transform);
            BuildSeparator(panel.transform, 225f);
            BuildScrollArea(panel.transform);
            BuildButtons(panel.transform);

            _root!.SetActive(false);
        }

        private void BuildStaticHeaders(Transform parent)
        {
            var headerRect = UIFactory.CreateRect("SpecHeaders", parent);
            headerRect.sizeDelta = new Vector2(ContentWidth, 28);
            headerRect.anchoredPosition = new Vector2(0, 250);
            var headerText = headerRect.gameObject.AddComponent<TextMeshProUGUI>();
            headerText.font = UIFactory.FontAsset;
            headerText.fontSize = 16;
            headerText.color = UIStyle.TextSecondary;
            headerText.alignment = TextAlignmentOptions.TopLeft;
            headerText.enableWordWrapping = false;
            headerText.overflowMode = TextOverflowModes.Overflow;
            headerText.text =
                $"№<pos={ColName}>Название" +
                $"<pos={ColW}>Ш<pos={ColH}>В<pos={ColD}>Г, мм" +
                $"<pos={ColMaterial}>Материал" +
                $"<pos={ColPieces}>Дет." +
                $"<pos={ColQty}>Кол-во" +
                $"<pos={ColUnit}>Ед.";
        }

        private static void BuildSeparator(Transform parent, float y)
        {
            var sepRect = UIFactory.CreateRect("SpecSeparator", parent);
            sepRect.sizeDelta = new Vector2(ContentWidth, 2);
            sepRect.anchoredPosition = new Vector2(0, y);
            var sepImg = sepRect.gameObject.AddComponent<Image>();
            sepImg.color = new Color(0.4f, 0.4f, 0.45f, 1f);
        }

        private void BuildScrollArea(Transform parent)
        {
            var viewportRect = UIFactory.CreateRect("SpecViewport", parent);
            viewportRect.sizeDelta = new Vector2(ContentWidth, ViewportHeight);
            viewportRect.anchoredPosition = new Vector2(0, ViewportCenterY);

            var viewportImage = viewportRect.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0.01f);
            var viewportMask = viewportRect.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            _contentRect = UIFactory.CreateRect("SpecContent", viewportRect.transform);
            _contentRect.sizeDelta = new Vector2(ContentWidth, 0);
            _contentRect.anchoredPosition = Vector2.zero;

            _content = _contentRect.gameObject.AddComponent<TextMeshProUGUI>();
            _content.font = UIFactory.FontAsset;
            _content.fontSize = 18;
            _content.color = UIFactory.TextColor;
            _content.alignment = TextAlignmentOptions.TopLeft;
            _content.enableAutoSizing = false;
            _content.enableWordWrapping = false;
            _content.overflowMode = TextOverflowModes.Overflow;

            _scrollRect = viewportRect.gameObject.AddComponent<ScrollRect>();
            _scrollRect.content = _contentRect;
            _scrollRect.viewport = viewportRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var scrollbarRect = UIFactory.CreateRect("SpecScrollbar", parent);
            scrollbarRect.sizeDelta = new Vector2(ScrollbarWidth, ViewportHeight);
            scrollbarRect.anchoredPosition = new Vector2(ContentWidth / 2f + 12f, ViewportCenterY);
            var scrollbarImage = scrollbarRect.gameObject.AddComponent<Image>();
            scrollbarImage.color = new Color(0.10f, 0.10f, 0.13f, 0.6f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handleRect = UIFactory.CreateRect("Handle", scrollbarRect.transform);
            handleRect.sizeDelta = new Vector2(ScrollbarWidth, 100);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleRect.gameObject.AddComponent<Image>();
            handleImage.color = UIStyle.ScrollHandle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            _scrollRect.verticalScrollbar = scrollbar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void BuildButtons(Transform parent)
        {
            const float btnGap = 20f;
            const float exportW = 130f;
            const float closeW = 160f;
            float totalW = exportW + btnGap + closeW;
            float exportX = -totalW * 0.5f + exportW * 0.5f;
            float closeX = totalW * 0.5f - closeW * 0.5f;
            const float btnY = -296f;

            var export = UIFactory.CreateButton("SpecExport", parent, "Экспорт CSV",
                new Vector2(exportX, btnY), new Vector2(exportW, 40), ExportCsv);
            export.GetComponent<Image>().color = UIStyle.Accent;
            UIFactory.CreateButton("SpecClose", parent, "Закрыть",
                new Vector2(closeX, btnY), new Vector2(closeW, 40), () => SetVisible(false));
            UIFactory.CreateCloseButton(parent, () => SetVisible(false));
        }

        public string WindowId => "specification";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        private void OnDestroy() => ProjectWindows.Unregister(this);

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible) Refresh();
            _root.SetActive(visible);
        }

        private void Refresh()
        {
            var result = SpecificationManager.Build(PartRegistry.All);
            _content!.text = BuildDisplayText(result);

            _content!.ForceMeshUpdate();
            var preferred = _content!.GetPreferredValues(ContentWidth, 0f);
            _contentRect!.sizeDelta = new Vector2(ContentWidth, Mathf.Max(preferred.y, 10f));

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect!);
            _scrollRect!.Rebuild(CanvasUpdate.PostLayout);
            _scrollRect!.verticalNormalizedPosition = 1f;
        }

        internal static string BuildDisplayText(SpecResult result)
        {
            var sb = new StringBuilder();
            int n = 1;
            bool firstSection = true;

            var bySection = result.lines
                .GroupBy(l => l.section)
                .OrderBy(g => g.Key, System.StringComparer.Ordinal);

            foreach (var section in bySection)
            {
                if (!firstSection) sb.AppendLine();
                firstSection = false;

                sb.Append($"<b>{Trim(SectionLabel(section.Key), MaxSectionChars)}</b>");
                sb.AppendLine();

                var byMaterial = section
                    .GroupBy(l => l.material)
                    .OrderBy(g => g.Key, System.StringComparer.Ordinal);

                foreach (var materialGroup in byMaterial)
                {
                    var lines = materialGroup.ToList();
                    foreach (var line in lines)
                    {
                        sb.Append($"{n}<pos={ColName}>{Trim(line.name, MaxNameChars)}" +
                                  $"<pos={ColW}>{DimCell(line.dimensionsMM.x, line.hasDims)}" +
                                  $"<pos={ColH}>{DimCell(line.dimensionsMM.y, line.hasDims)}" +
                                  $"<pos={ColD}>{DimCell(line.dimensionsMM.z, line.hasDims)}" +
                                  $"<pos={ColMaterial}>{Trim(line.material, MaxMaterialChars)}" +
                                  $"<pos={ColPieces}>{PiecesCell(line)}" +
                                  $"<pos={ColQty}>{FormatQty(line.qtyTotal, line.unit)}" +
                                  $"<pos={ColUnit}>{line.unit.Label()}");
                        sb.AppendLine();
                        n++;
                    }

                    sb.Append($"<pos={ColMaterial}>{MaterialSubtotalText(materialGroup.Key, lines)}");
                    sb.AppendLine();
                }
            }

            if (result.lines.Count > 0)
                sb.AppendLine();

            sb.Append($"<pos={ColName}>Всего, досок:<pos={ColQty}>{result.totalCount}");
            foreach (var unit in SortedUnits(result))
            {
                sb.AppendLine();
                sb.Append($"<pos={ColName}>Всего, {unit.Label()}:" +
                          $"<pos={ColQty}>{FormatQty(result.totalsByUnit[unit], unit)}" +
                          $"<pos={ColUnit}>{unit.Label()}");
            }
            return sb.ToString();
        }

        private static string SectionLabel(string section) =>
            string.IsNullOrEmpty(section) ? "Без раздела" : section;

        private static string DimCell(int valueMM, bool hasDims) => hasDims ? valueMM.ToString() : "";

        private static string PiecesCell(SpecLine line) => line.hasDims ? line.count.ToString() : "";

        private static string FormatQty(float qtyTotal, SpecUnit unit) =>
            unit == SpecUnit.Pieces ? Mathf.RoundToInt(qtyTotal).ToString() : qtyTotal.ToString("F2");

        private static string MaterialSubtotalText(string material, IReadOnlyList<SpecLine> lines)
        {
            var totals = SpecTotals.ByUnit(lines.Select(l => (l.unit, l.qtyTotal)));
            var parts = totals.Keys.OrderBy(u => (int)u)
                .Select(u => $"{FormatQty(totals[u], u)} {u.Label()}");
            string label = string.IsNullOrEmpty(material) ? "без материала" : material;
            return $"Итого, {label}: {string.Join(", ", parts)}";
        }

        private static IEnumerable<SpecUnit> SortedUnits(SpecResult result)
        {
            if (result.totalsByUnit == null) yield break;
            foreach (var unit in result.totalsByUnit.Keys.OrderBy(u => (int)u))
                yield return unit;
        }

        private static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");

        private void ExportCsv()
        {
            var result = SpecificationManager.Build(PartRegistry.All);
            string defaultName = $"KitchenSpec_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string? path = NativeFileDialog.SaveCSVDialog("Экспорт спецификации", defaultName,
                Application.persistentDataPath);
            bool userCancelledTheDialog = string.IsNullOrEmpty(path);
            if (userCancelledTheDialog)
                return;

            if (SpecificationExport.SaveToFile(result, path!))
            {
                ToastNotification.ShowIfAvailable("CSV сохранён", 2f);
            }
            else
            {
                Debug.LogError("[Spec] CSV export failed");
            }
        }
    }
}
