using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class HelpUI : MonoBehaviour
    {
        public static HelpUI Instance { get; private set; } = null!;

        private GameObject _root = null!;

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("HelpPanel", canvas, Vector2.zero, new Vector2(520, 580));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;

            UIFactory.CreateLabel("HelpTitle", panel.transform, "Справка", 24,
                new Vector2(0, 236), new Vector2(480, 36), TextAnchor.MiddleCenter);

            var text = "" +
                "W A S D  — перемещение камеры\n" +
                "< ^ > v  — поворот камеры\n" +
                "+ / -    — приближение / отдаление\n" +
                "\n" +
                "ПКМ + движение  — орбита камеры\n" +
                "СКМ / ЛКМ + пусто  — панорамирование\n" +
                "Колёсико мыши  — зум\n" +
                "\n" +
                "ЛКМ по детали  — выделение\n" +
                "Ctrl + ЛКМ  — мультивыделение\n" +
                "Delete  — удалить объект(ы)\n" +
                "Ctrl + D  — дублировать\n" +
                "Ctrl + Z  — отмена\n" +
                "Ctrl + Y / Ctrl+Shift+Z  — повтор\n" +
                "Escape  — отменить перетаскивание / закрыть меню\n" +
                "\n" +
                "F  — фокус камеры на выделенном объекте\n" +
                "1  — вид сверху\n" +
                "2  — вид сбоку\n" +
                "3  — вид спереди\n" +
                "F1 — эта справка";

            UIFactory.CreateLabel("HelpText", panel.transform, text, 15,
                new Vector2(0, 20), new Vector2(480, 430), TextAnchor.UpperLeft);

            UIFactory.CreateButton("HelpClose", panel.transform, "Закрыть",
                new Vector2(0, -260), new Vector2(160, 40), Close);

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
    }
}
