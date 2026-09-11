using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsControlTab
    {
        private readonly SettingsRowFactory _rows;

        public SettingsControlTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY)
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
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
