using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SpecificationPanelUI : MonoBehaviour, IProjectWindow
    {
        private const float ColName = 40f;
        // Ш/В/Г — отдельные колонки: цифры выравниваются друг под другом,
        // а не сливаются в строку «600×720×18».
        private const float ColW = 250f;
        private const float ColH = 305f;
        private const float ColD = 360f;
        private const float ColCount = 420f;
        private const float ColArea = 490f;
        private const float ContentWidth = 540f;
        private const float ViewportHeight = 480f;
        private const float ViewportCenterY = -30f;
        private const int MaxNameChars = 22;

        private GameObject? _root;
        private TMP_Text? _content;
        private RectTransform? _contentRect;
        private ScrollRect? _scrollRect;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SpecPanel", canvas, Vector2.zero, new Vector2(560, 640));
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
            // Вторичный цвет текста, а не жёлтый: жёлтая рамка в проекте значит
            // «значение изменено» (правило 10 — один цвет, один смысл).
            headerText.color = UIStyle.TextSecondary;
            headerText.alignment = TextAlignmentOptions.TopLeft;
            headerText.enableWordWrapping = false;
            headerText.overflowMode = TextOverflowModes.Overflow;
            headerText.text =
                $"№<pos={ColName}>Название" +
                $"<pos={ColW}>Ш<pos={ColH}>В<pos={ColD}>Г, мм" +
                $"<pos={ColCount}>Кол-во" +
                $"<pos={ColArea}>S, м²";
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

            // Узкий (8px) тёмный скроллбар; прячется, когда список помещается
            // (та же схема, что в HierarchyPanelUI).
            var scrollbarRect = UIFactory.CreateRect("SpecScrollbar", parent);
            scrollbarRect.sizeDelta = new Vector2(8, ViewportHeight);
            scrollbarRect.anchoredPosition = new Vector2(282, ViewportCenterY);
            var scrollbarImage = scrollbarRect.gameObject.AddComponent<Image>();
            scrollbarImage.color = new Color(0.10f, 0.10f, 0.13f, 0.6f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handleRect = UIFactory.CreateRect("Handle", scrollbarRect.transform);
            handleRect.sizeDelta = new Vector2(8, 100);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleRect.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.38f, 0.40f, 0.46f, 1f);
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

            // Главное действие окна — экспорт: выделено акцентным цветом.
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

            var sb = new StringBuilder();
            int n = 1;
            foreach (var line in result.lines)
            {
                sb.Append($"{n}<pos={ColName}>{Trim(line.name, MaxNameChars)}" +
                          $"<pos={ColW}>{line.dimensionsMM.x}" +
                          $"<pos={ColH}>{line.dimensionsMM.y}" +
                          $"<pos={ColD}>{line.dimensionsMM.z}" +
                          $"<pos={ColCount}>{line.count}" +
                          $"<pos={ColArea}>{line.totalAreaM2:F2}");
                sb.AppendLine();
                n++;
            }

            if (result.lines.Count > 0)
                sb.AppendLine();

            sb.Append($"<pos={ColName}>Всего:" +
                      $"<pos={ColCount}>{result.totalCount}" +
                      $"<pos={ColArea}>{result.totalAreaM2:F2}");
            _content!.text = sb.ToString();

            _content!.ForceMeshUpdate();
            var preferred = _content!.GetPreferredValues(ContentWidth, 0f);
            _contentRect!.sizeDelta = new Vector2(ContentWidth, Mathf.Max(preferred.y, 10f));

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect!);
            _scrollRect!.Rebuild(CanvasUpdate.PostLayout);
            _scrollRect!.verticalNormalizedPosition = 1f;
        }

        private static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");

        private void ExportCsv()
        {
            var result = SpecificationManager.Build(PartRegistry.All);
            string defaultName = $"KitchenSpec_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string? path = NativeFileDialog.SaveCSVDialog("Экспорт спецификации", defaultName,
                Application.persistentDataPath);
            if (string.IsNullOrEmpty(path))
                return; // пользователь отменил диалог

            if (SpecificationExport.SaveToFile(result, path))
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
