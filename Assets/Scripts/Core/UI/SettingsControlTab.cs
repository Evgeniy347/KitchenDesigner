using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsControlTab
    {
        private const float ReferenceLineH = 20f;
        private const float SectionGap = 16f;

        private static readonly string[] ReferenceLines =
        {
            "Панорамирование начинается и с ЛКМ по пустому месту, а не только своим жестом",
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
        private readonly List<RectTransform> _referenceRects = new();
        private KeybindingRowUI? _bindings;
        private float _referenceTop;

        public SettingsControlTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY,
            KeybindingCaptureGate captureGate, KeybindingGestureGate gestureGate,
            System.Action afterBindingChange)
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

            _bindings = new KeybindingRowUI(s, captureGate, gestureGate, afterBindingChange);
            _bindings.Build(page, ref y);
            _bindings.AfterRelayout = ShiftReferenceBlock;

            y -= SectionGap;
            _referenceTop = y;
            BuildReferenceBlock(page, ref y);
        }

        private void ShiftReferenceBlock(float listBottom)
        {
            float delta = listBottom - SectionGap - _referenceTop;
            if (Mathf.Approximately(delta, 0f)) return;

            foreach (var rect in _referenceRects)
                rect.anchoredPosition += new Vector2(0f, delta);

            _referenceTop += delta;
        }

        public void RefreshConflicts() => _bindings?.RefreshAll();

        private void BuildReferenceBlock(Transform page, ref float y)
        {
            var header = UIFactory.CreateSectionHeader(
                "KbRefSection", page, "Мышь и другое, не привязано к клавише",
                SettingsRowFactory.ContentW);
            header.anchoredPosition = new Vector2(0f, y - 9f);
            _referenceRects.Add(header);
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
                _referenceRects.Add(rect);

                y -= height + 4f;
            }
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
