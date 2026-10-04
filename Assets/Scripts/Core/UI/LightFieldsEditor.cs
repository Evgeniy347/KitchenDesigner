using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class LightFieldsEditor : ElementFieldsEditor
    {
        public const string AdvancedSectionId = "LightAdvanced";

        private static string AdvancedCaption => Loc.T("element.light.advanced");

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

        public LightFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element is LightSourceElement;

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            foreach (var binding in _bindings) yield return binding.Field;
        }

        public override void Build()
        {
            var plain = RowVisibility.For(ElementFacet.Light);

            Bind(Loc.T("element.light.temperature"), "K", plain, l => l.TemperatureK, (l, v) => l.TemperatureK = v,
                LampSpec.DEFAULT_TEMPERATURE_K, hint: "element.light.temperature");
            Bind(Loc.T("element.light.power"), Loc.T("unit.w"), plain, l => l.PowerW, (l, v) => l.PowerW = v,
                LampSpec.DEFAULT_POWER_W, hint: "element.light.power");
            Bind(Loc.T("element.light.diffusion"), "%", plain, l => l.DiffusionPct, (l, v) => l.DiffusionPct = v,
                LampSpec.DEFAULT_DIFFUSION_PCT, hint: "element.light.diffusion");
            Bind(Loc.T("element.light.beamAngle"), "°", plain, l => l.BeamAngleDeg, (l, v) => l.BeamAngleDeg = v,
                LampSpec.DEFAULT_BEAM_DEG, hint: "element.light.beamAngle");
            Bind(Loc.T("element.light.softness"), "%", plain, l => l.SoftnessPct, (l, v) => l.SoftnessPct = v,
                LampSpec.DEFAULT_SOFTNESS_PCT, hint: "element.light.softness");
            Bind(Loc.T("element.light.upLight"), "%", plain, l => l.UpLightPct, (l, v) => l.UpLightPct = v,
                LampSpec.DEFAULT_UP_PCT, hint: "element.light.upLight");

            _shape = Rows.Dropdown(Loc.T("element.light.shape"), new List<string> { Loc.T("element.light.shapeShade"), Loc.T("element.light.shapeGlobe") },
                OnShapeSelected, plain, "CtxLightShape", hint: "element.light.shape");
            _shadow = Rows.Dropdown(Loc.T("element.light.shadow"), new List<string> { Loc.T("element.light.shadowNone"), Loc.T("element.light.shadowHard"), Loc.T("element.light.shadowSoft") },
                OnShadowSelected, plain, "CtxLightShadow", hint: "element.light.shadow");

            Bind(Loc.T("element.light.shadowStrength"), "%", plain, l => l.ShadowStrengthPct, (l, v) => l.ShadowStrengthPct = v,
                LampSpec.DEFAULT_SHADOW_STRENGTH_PCT, hint: "element.light.shadowStrength");

            Rows.BeginSection(AdvancedSectionId, AdvancedCaption, false);

            Bind(Loc.T("element.light.glow"), "%", plain, l => l.GlowPct, (l, v) => l.GlowPct = v,
                LampSpec.DEFAULT_GLOW_PCT, hint: "element.light.glow");
            Bind(Loc.T("element.light.drop"), Loc.T("unit.mm"), plain, l => l.DropMM, (l, v) => l.DropMM = v,
                LampSpec.DEFAULT_DROP_MM, hint: "element.light.drop");
            Bind(Loc.T("element.light.upCone"), "%", plain, l => l.UpConePct, (l, v) => l.UpConePct = v,
                LampSpec.DEFAULT_UP_CONE_PCT, hint: "element.light.upCone");
            Bind(Loc.T("element.light.upRange"), "%", plain, l => l.UpRangePct, (l, v) => l.UpRangePct = v,
                LampSpec.DEFAULT_UP_RANGE_PCT, hint: "element.light.upRange");
            Bind(Loc.T("element.light.range"), Loc.T("unit.mm"), plain, l => l.RangeMinMM, (l, v) => l.RangeMinMM = v,
                LampSpec.DEFAULT_RANGE_MIN_MM, hint: "element.light.range");
            Bind(Loc.T("element.light.rangeMax"), Loc.T("unit.mm"), plain, l => l.RangeMaxMM, (l, v) => l.RangeMaxMM = v,
                LampSpec.DEFAULT_RANGE_MAX_MM, hint: "element.light.rangeMax");
            Bind(Loc.T("element.light.efficacy"), Loc.T("unit.lmPerW"), plain, l => l.EfficacyLmPerW,
                (l, v) => l.EfficacyLmPerW = v, LampSpec.DEFAULT_EFFICACY_LM_PER_W,
                hint: "element.light.efficacy");
            Bind(Loc.T("element.light.lumensPerUnit"), Loc.T("unit.lmPerUnit"), plain, l => l.LumensPerUnit,
                (l, v) => l.LumensPerUnit = v, LampSpec.DEFAULT_LUMENS_PER_UNIT,
                hint: "element.light.lumensPerUnit");
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
            Func<LightSourceElement, int> read, Action<LightSourceElement, int> write, int fallback,
            string? hint = null)
        {
            var field = Rows.NumberField(label, visibility, unit, null, hint);
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
                ChoiceRowUndo.Commit(lamp,
                    () => lamp.Shape = index == 1 ? LampShape.Sphere : LampShape.Plafond);
        }

        private void OnShadowSelected(int index)
        {
            if (Host.Target is LightSourceElement lamp)
                ChoiceRowUndo.Commit(lamp,
                    () => lamp.Shadow = (LampShadow)Mathf.Clamp(index, 0, 2));
        }
    }
}
