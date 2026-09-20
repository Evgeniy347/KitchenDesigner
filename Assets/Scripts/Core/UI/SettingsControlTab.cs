using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsControlTab
    {
        private const float ReferenceLineH = 20f;
        private const float SectionGap = 16f;

        private static readonly string[] ReferenceLines =
        {
            "ПКМ + движение — поворот камеры на месте",
            "СКМ / ЛКМ по пустому месту — панорамирование",
            "Колёсико мыши — вперёд / назад (зум)",
            "Ctrl + перетаскивание или ресайз — обратная привязка (вкл/выкл)",
            "Escape закрывает по одному хозяину за нажатие: перетаскивание → пикер связи "
                + "со светом → измерение → пипетка → подтверждение удаления → контекстное "
                + "меню → меню группы → плитка каталога → каталог → панель дня/ночи → музыка",
            "Каталог деталей: в поле поиска ↓ переходит в сетку плиток; в сетке ← → ↑ ↓ — "
                + "соседняя плитка, Enter ставит выбранную, [ / ] меняет вариант, Escape "
                + "снимает выделение плитки",
            "Пока фокус в другом поле ввода, каталог не читает ни Enter, ни стрелки, "
                + "ни [ / ], ни Escape — выделение плитки остаётся, но молчит",
        };

        private readonly SettingsRowFactory _rows;
        private KeybindingRowUI? _bindings;

        public SettingsControlTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY,
            KeybindingCaptureGate captureGate, System.Action afterBindingChange)
        {
            float y = topY;

            _rows.AddSpeedSlider(page, ref y, "Чувствительность мыши", s.MouseSensitivity,
                v => s.MouseSensitivity = v, read: () => s.MouseSensitivity);
            Hint("Чувствительность мыши", hint: "settings.control.mouseSensitivity");
            _rows.AddSpeedSlider(page, ref y, "Скорость WASD", s.WasdSpeed,
                v => s.WasdSpeed = v, read: () => s.WasdSpeed);
            Hint("Скорость WASD", hint: "settings.control.wasdSpeed");
            _rows.AddSpeedSlider(page, ref y, "Скорость ←→↑↓", s.ArrowSpeed,
                v => s.ArrowSpeed = v, read: () => s.ArrowSpeed);
            Hint("Скорость ←→↑↓", hint: "settings.control.arrowSpeed");

            y -= SectionGap;
            var header = UIFactory.CreateSectionHeader(
                "KbSection", page, "Горячие клавиши", SettingsRowFactory.ContentW);
            header.anchoredPosition = new Vector2(0f, y - 9f);
            y -= 18f + SettingsRowFactory.GapPx;

            _bindings = new KeybindingRowUI(s, captureGate, afterBindingChange);
            _bindings.Build(page, ref y);

            y -= SectionGap;
            BuildReferenceBlock(page, ref y);
        }

        public void RefreshConflicts() => _bindings?.RefreshAll();

        private void BuildReferenceBlock(Transform page, ref float y)
        {
            var header = UIFactory.CreateSectionHeader(
                "KbRefSection", page, "Мышь и другое, не привязано к клавише",
                SettingsRowFactory.ContentW);
            header.anchoredPosition = new Vector2(0f, y - 9f);
            y -= 18f + SettingsRowFactory.GapPx;

            foreach (var line in ReferenceLines)
            {
                var rect = UIFactory.CreateRect("KbRef_" + line.GetHashCode(), page);
                rect.sizeDelta = new Vector2(SettingsRowFactory.ContentW, ReferenceLineH);

                var label = UIFactory.CreateLabel("KbRefLbl_" + line.GetHashCode(), rect, line,
                    UIStyle.FontSmall, Vector2.zero, new Vector2(SettingsRowFactory.ContentW, ReferenceLineH),
                    TextAnchor.UpperLeft);
                label.color = UIStyle.TextSecondary;
                label.enableWordWrapping = true;

                float height = Mathf.Max(ReferenceLineH,
                    label.GetPreferredValues(line, SettingsRowFactory.ContentW, 0f).y);
                rect.sizeDelta = new Vector2(SettingsRowFactory.ContentW, height);
                rect.anchoredPosition = new Vector2(0f, y - height * 0.5f);
                label.rectTransform.sizeDelta = new Vector2(SettingsRowFactory.ContentW, height);

                y -= height + 4f;
            }
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
