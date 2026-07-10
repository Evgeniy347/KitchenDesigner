using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Панель спецификации: таблица групп деталей + итог.</summary>
    public class SpecificationPanelUI : MonoBehaviour
    {
        private GameObject _root;
        private Text _content;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SpecPanel", canvas, Vector2.zero, new Vector2(560, 640));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;

            UIFactory.CreateLabel("SpecTitle", panel.transform, "Спецификация", 24,
                new Vector2(0, 290), new Vector2(540, 36), TextAnchor.MiddleCenter);

            _content = UIFactory.CreateLabel("SpecContent", panel.transform, "", 18,
                new Vector2(0, -10), new Vector2(540, 540), TextAnchor.UpperLeft);

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
