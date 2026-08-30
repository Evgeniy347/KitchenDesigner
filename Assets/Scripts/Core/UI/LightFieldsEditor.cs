using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class LightFieldsEditor : ElementFieldsEditor
    {
        private const string AdvancedCaption = "Тонкая настройка";

        private readonly struct Binding
        {
            public Binding(TMP_InputField field, Func<LightSourceElement, int> read,
                Action<LightSourceElement, int> write, int fallback)
            {
                Field = field;
                Read = read;
                Write = write;
                Fallback = fallback;
            }

            public TMP_InputField Field { get; }
            public Func<LightSourceElement, int> Read { get; }
            public Action<LightSourceElement, int> Write { get; }
            public int Fallback { get; }
        }

        private readonly List<Binding> _bindings = new();

        private TMP_Dropdown? _shape, _shadow;
        private TMP_Text? _advancedLabel;
        private bool _advancedExpanded;

        public LightFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is LightSourceElement;

        public void Collapse()
        {
            _advancedExpanded = false;
            UpdateAdvancedCaption();
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            foreach (var binding in _bindings) yield return binding.Field;
        }

        public override void Build()
        {
            var plain = RowVisibility.For(ElementFacet.Light);
            var advanced = RowVisibility.For(ElementFacet.Light, () => _advancedExpanded);

            Bind("Температура", "K", plain, l => l.TemperatureK, (l, v) => l.TemperatureK = v,
                LightSourceElement.DEFAULT_TEMPERATURE_K);
            Bind("Мощность", "Вт", plain, l => l.PowerW, (l, v) => l.PowerW = v,
                LightSourceElement.DEFAULT_POWER_W);
            Bind("Рассеивание", "%", plain, l => l.DiffusionPct, (l, v) => l.DiffusionPct = v,
                LightSourceElement.DEFAULT_DIFFUSION_PCT);
            Bind("Угол пучка", "°", plain, l => l.BeamAngleDeg, (l, v) => l.BeamAngleDeg = v,
                LightSourceElement.DEFAULT_BEAM_DEG);
            Bind("Мягкость края", "%", plain, l => l.SoftnessPct, (l, v) => l.SoftnessPct = v,
                LightSourceElement.DEFAULT_SOFTNESS_PCT);
            Bind("Свет вверх", "%", plain, l => l.UpLightPct, (l, v) => l.UpLightPct = v,
                LightSourceElement.DEFAULT_UP_PCT);

            _shape = Rows.Dropdown("Форма потока", new List<string> { "Плафон", "Шар" },
                OnShapeSelected, plain, "CtxLightShape");
            _shadow = Rows.Dropdown("Тени лампы", new List<string> { "Нет", "Жёсткие", "Мягкие" },
                OnShadowSelected, plain, "CtxLightShadow");

            Bind("Сила тени", "%", plain, l => l.ShadowStrengthPct, (l, v) => l.ShadowStrengthPct = v,
                LightSourceElement.DEFAULT_SHADOW_STRENGTH_PCT);

            _advancedLabel = Rows.WideButton("CtxLightAdv",
                $"{AdvancedCaption}  {UIStyle.GlyphCollapsed}", ToggleAdvanced, plain, RowGap);

            Bind("Свечение плафона", "%", advanced, l => l.GlowPct, (l, v) => l.GlowPct = v,
                LightSourceElement.DEFAULT_GLOW_PCT);
            Bind("Отступ вниз", "мм", advanced, l => l.DropMM, (l, v) => l.DropMM = v,
                LightSourceElement.DEFAULT_DROP_MM);
            Bind("Верхний конус", "%", advanced, l => l.UpConePct, (l, v) => l.UpConePct = v,
                LightSourceElement.DEFAULT_UP_CONE_PCT);
            Bind("Верхний радиус", "%", advanced, l => l.UpRangePct, (l, v) => l.UpRangePct = v,
                LightSourceElement.DEFAULT_UP_RANGE_PCT);
            Bind("Радиус при 0 %", "мм", advanced, l => l.RangeMinMM, (l, v) => l.RangeMinMM = v,
                LightSourceElement.DEFAULT_RANGE_MIN_MM);
            Bind("Радиус при 100 %", "мм", advanced, l => l.RangeMaxMM, (l, v) => l.RangeMaxMM = v,
                LightSourceElement.DEFAULT_RANGE_MAX_MM);
            Bind("Светоотдача", "лм/Вт", advanced, l => l.EfficacyLmPerW,
                (l, v) => l.EfficacyLmPerW = v, LightSourceElement.DEFAULT_EFFICACY_LM_PER_W);
            Bind("Калибровка", "лм/ед", advanced, l => l.LumensPerUnit,
                (l, v) => l.LumensPerUnit = v, LightSourceElement.DEFAULT_LUMENS_PER_UNIT);
        }

        public override void Show(KitchenElement element)
        {
            if (!(element is LightSourceElement lamp)) return;
            foreach (var binding in _bindings)
                binding.Field.text = binding.Read(lamp).ToString();
            ShowDropdowns(lamp);
        }

        public override void Refresh(KitchenElement element)
        {
            if (!(element is LightSourceElement lamp)) return;
            foreach (var binding in _bindings)
                Fields.RefreshUnfocused(binding.Field, binding.Read(lamp).ToString());
            ShowDropdowns(lamp);
        }

        public override void Apply(KitchenElement element)
        {
            if (!(element is LightSourceElement lamp)) return;
            foreach (var binding in _bindings)
            {
                binding.Write(lamp, Fields.ParseInt(binding.Field, binding.Read(lamp)));
                binding.Field.text = binding.Read(lamp).ToString();
            }
        }

        public override void Track(KitchenElement element)
        {
            var lamp = element as LightSourceElement;
            foreach (var binding in _bindings)
                Fields.Track(binding.Field,
                    lamp != null ? binding.Read(lamp).ToString() : binding.Fallback.ToString());
        }

        private void Bind(string label, string unit, RowVisibility visibility,
            Func<LightSourceElement, int> read, Action<LightSourceElement, int> write, int fallback)
        {
            var field = Rows.NumberField(label, visibility, unit);
            _bindings.Add(new Binding(field, read, write, fallback));
        }

        private void ShowDropdowns(LightSourceElement lamp)
        {
            _shape?.SetValueWithoutNotify((int)lamp.Shape);
            _shadow?.SetValueWithoutNotify((int)lamp.Shadow);
        }

        private void OnShapeSelected(int index)
        {
            if (Host.Target is LightSourceElement lamp)
                lamp.Shape = index == 1 ? LampShape.Sphere : LampShape.Plafond;
        }

        private void OnShadowSelected(int index)
        {
            if (Host.Target is LightSourceElement lamp)
                lamp.Shadow = (LampShadow)Mathf.Clamp(index, 0, 2);
        }

        private void ToggleAdvanced()
        {
            _advancedExpanded = !_advancedExpanded;
            UpdateAdvancedCaption();
            Host.Relayout();
        }

        private void UpdateAdvancedCaption()
        {
            if (_advancedLabel == null) return;
            _advancedLabel.text =
                $"{AdvancedCaption}  {(_advancedExpanded ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed)}";
        }
    }
}
