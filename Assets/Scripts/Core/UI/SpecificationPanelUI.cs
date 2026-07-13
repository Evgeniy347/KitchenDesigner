using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Панель спецификации: таблица групп деталей + итог.</summary>
    public class SpecificationPanelUI : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _content;
        private RectTransform _contentRect;
        private ScrollRect _scrollRect;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SpecPanel", canvas, Vector2.zero, new Vector2(560, 640));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;

            UIFactory.CreateLabel("SpecTitle", panel.transform, "Спецификация", 24,
                new Vector2(0, 290), new Vector2(540, 36), TextAnchor.MiddleCenter);

            // Viewport для скролла: область между заголовком и кнопками.
            var viewportRect = UIFactory.CreateRect("SpecViewport", panel.transform);
            viewportRect.sizeDelta = new Vector2(540, 520);
            viewportRect.anchoredPosition = new Vector2(0, -10);

            // Content внутри viewport.
            _contentRect = UIFactory.CreateRect("SpecContent", viewportRect.transform);
            _contentRect.sizeDelta = new Vector2(540, 0);
            _contentRect.anchoredPosition = Vector2.zero;

            _content = _contentRect.gameObject.AddComponent<TextMeshProUGUI>();
            _content.font = UIFactory.FontAsset;
            _content.fontSize = 18;
            _content.color = UIFactory.TextColor;
            _content.alignment = TextAlignmentOptions.TopLeft;
            _content.enableAutoSizing = false;
            _content.overflowMode = TextOverflowModes.Overflow;

            // ScrollRect на viewport.
            _scrollRect = viewportRect.gameObject.AddComponent<ScrollRect>();
            _scrollRect.content = _contentRect;
            _scrollRect.viewport = viewportRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Scrollbar справа от viewport.
            var scrollbarRect = UIFactory.CreateRect("SpecScrollbar", panel.transform);
            scrollbarRect.sizeDelta = new Vector2(16, 520);
            scrollbarRect.anchoredPosition = new Vector2(278, -10);
            var scrollbarImage = scrollbarRect.gameObject.AddComponent<Image>();
            scrollbarImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handleRect = UIFactory.CreateRect("Handle", scrollbarRect.transform);
            handleRect.sizeDelta = new Vector2(14, 100);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleRect.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.5f, 0.5f, 0.55f, 1f);
            scrollbar.targetGraphic = handleImage;
            _scrollRect.verticalScrollbar = scrollbar;

            UIFactory.CreateButton("SpecExport", panel.transform, "Экспорт CSV",
                new Vector2(-140, -300), new Vector2(130, 40), ExportCsv);
            UIFactory.CreateButton("SpecClose", panel.transform, "Закрыть",
                new Vector2(0, -300), new Vector2(160, 40), () => SetVisible(false));

            _root.SetActive(false);
        }

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
            sb.AppendLine("№   Название              Ш×В×Г (мм)        Кол-во   S, м²");
            sb.AppendLine("─────────────────────────────────────────────────────────");
            int n = 1;
            foreach (var line in result.lines)
            {
                string dims = $"{line.dimensionsMM.x}×{line.dimensionsMM.y}×{line.dimensionsMM.z}";
                sb.AppendLine($"{n,-3} {Trim(line.name, 20),-20} {dims,-16} {line.count,5}   {line.totalAreaM2,6:F2}");
                n++;
            }
            sb.AppendLine("─────────────────────────────────────────────────────────");
            sb.AppendLine($"Всего: {result.totalCount} деталей | Площадь: {result.totalAreaM2:F2} м²");
            _content.text = sb.ToString();

            // Вычисляем высоту текста вручную и устанавливаем размер content.
            _content.ForceMeshUpdate();
            var preferred = _content.GetPreferredValues(540f, 0f);
            _contentRect.sizeDelta = new Vector2(540, preferred.y);

            // Форсируем пересчёт Layout чтобы ScrollRect увидел новый размер.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
            _scrollRect.Rebuild(CanvasUpdate.PostLayout);
            
            // Сбрасываем скролл вверх.
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        private static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");

        private void ExportCsv()
        {
            var result = SpecificationManager.Build(PartRegistry.All);
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                $"KitchenSpec_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (SpecificationExport.SaveToFile(result, path))
            {
                if (ToastNotification.Instance != null)
                    ToastNotification.Instance.Show("CSV saved to Desktop", 2f);
            }
            else
            {
                Debug.LogError("[Spec] CSV export failed");
            }
        }
    }
}
