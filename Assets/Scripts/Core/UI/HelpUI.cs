using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class HelpUI : MonoBehaviour
    {
        public static HelpUI? Instance { get; private set; }

        public const float PanelWidth = 520f;

        private GameObject? _root;

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("HelpPanel", canvas, Vector2.zero, Vector2.zero);
            UIFactory.AnchorCenter(panel.rectTransform);
            _root = panel.gameObject;

            float contentWidth = PanelWidth - 2f * UIStyle.WindowPad;

            var title = UIFactory.CreateLabel("HelpTitle", panel.transform, "Справка",
                UIStyle.FontWindowTitle, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            var body = UIFactory.CreateLabel("HelpText", panel.transform, BodyText,
                UIStyle.FontSection, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            var close = UIFactory.CreateButton("HelpClose", panel.transform, "Закрыть",
                Vector2.zero, Vector2.zero, Close);

            float titleHeight = FitToPreferredWidth(title, contentWidth);
            float bodyHeight = FitToPreferredWidth(body, contentWidth);

            var closeRect = close.GetComponent<RectTransform>();
            var closeLabel = close.GetComponentInChildren<TextMeshProUGUI>();
            var closeText = closeLabel.GetPreferredValues();
            float closeWidth = closeText.x + 2f * UIStyle.GapSection;
            float closeHeight = Mathf.Max(UIStyle.HitTarget, closeText.y + 2f * UIStyle.GapInner);
            closeRect.sizeDelta = new Vector2(closeWidth, closeHeight);

            float panelHeight = 2f * UIStyle.WindowPad + titleHeight + UIStyle.GapSection
                + bodyHeight + UIStyle.GapSection + closeHeight;
            panel.rectTransform.sizeDelta = new Vector2(PanelWidth, panelHeight);

            float half = panelHeight * 0.5f;
            title.rectTransform.anchoredPosition =
                new Vector2(0f, half - UIStyle.WindowPad - titleHeight * 0.5f);
            body.rectTransform.anchoredPosition =
                new Vector2(0f, half - UIStyle.WindowPad - titleHeight - UIStyle.GapSection - bodyHeight * 0.5f);
            closeRect.anchoredPosition =
                new Vector2(0f, -half + UIStyle.WindowPad + closeHeight * 0.5f);

            _root.SetActive(false);
        }

        public void Toggle()
        {
            if (_root == null) return;
            _root.SetActive(!_root.activeSelf);
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        private static float FitToPreferredWidth(TextMeshProUGUI label, float width)
        {
            float height = label.GetPreferredValues(width, 0f).y;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return height;
        }

        private const string BodyText = "" +
            "W A S D  — перемещение камеры (разгон при удержании)\n" +
            "Shift + W A S D  — разгон сразу\n" +
            "< ^ > v  — поворот камеры (если плитка не выбрана в сетке каталога)\n" +
            "+ / -    — вперёд / назад\n" +
            "\n" +
            "ПКМ + движение  — поворот камеры на месте\n" +
            "СКМ / ЛКМ + пусто  — панорамирование\n" +
            "Колёсико мыши  — вперёд / назад\n" +
            "\n" +
            "ЛКМ по детали  — выделение\n" +
            "Ctrl + ЛКМ  — мультивыделение\n" +
            "Ctrl + перетаскивание / ресайз  — прилипание наоборот (вкл/выкл)\n" +
            "Delete  — удалить объект(ы)\n" +
            "Ctrl + D  — дублировать\n" +
            "Ctrl + S  — сохранить\n" +
            "Ctrl + Z  — отмена\n" +
            "Ctrl + Y / Ctrl+Shift+Z  — повтор\n" +
            "Escape  — отменить перетаскивание / закрыть меню / свернуть каталог\n" +
            "E  — открыть/закрыть дверь/окно/фасад\n" +
            "E / двойной клик  — включить/выключить выключатель\n" +
            "\n" +
            "/  — раскрыть каталог деталей и перейти в поиск (не работает при наборе текста "
            + "в другом поле)\n" +
            "В поле поиска, ↓  — перейти в сетку плиток, не теряя набранного\n" +
            "В сетке плиток, ← → ↑ ↓  — соседняя плитка (на границе группы — в соседнюю), "
            + "забирают стрелки у камеры, пока плитка выбрана\n" +
            "В сетке плиток, Enter  — поставить выбранную плитку текущим вариантом\n" +
            "В сетке плиток, [ / ]  — сменить вариант выбранной плитки\n" +
            "В сетке плиток, Escape  — снять выделение плитки\n" +
            "Пока фокус в любом другом поле ввода, каталог не видит ни Enter, ни стрелки, "
            + "ни [ / ], ни Escape — выделение плитки остаётся, но молчит\n" +
            "\n" +
            "F  — фокус камеры на выделенном объекте\n" +
            "1  — вид сверху\n" +
            "2  — вид сбоку\n" +
            "3  — вид спереди\n" +
            "F1 — эта справка\n" +
            "F9 — логирование / профилировка";
    }
}
