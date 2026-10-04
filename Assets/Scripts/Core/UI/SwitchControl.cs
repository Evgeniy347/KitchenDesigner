using System;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SwitchControl : MonoBehaviour
    {
        public const string TrackNode = "Track";
        public const string KnobNode = "Knob";

        private Toggle? _toggle;
        private Image? _track;
        private Image? _rim;
        private Image? _knob;

        public Toggle? Toggle => _toggle;

        public static Toggle Create(string name, Transform parent, bool value, Action<bool>? onChanged)
        {
            var rect = UIFactory.CreateRect(name, parent);
            rect.sizeDelta = new Vector2(UIStyle.SwitchW, UIStyle.SwitchH);

            var track = UIFactory.CreatePanel(TrackNode, rect, Vector2.zero,
                new Vector2(UIStyle.SwitchW, UIStyle.SwitchH), UIStyle.Field);
            RoundedRectSprites.Apply(track, RoundedRectSprites.Fill(UIStyle.SwitchH * 0.5f));
            var rim = UIFactory.AddFieldStroke(track.rectTransform);
            RoundedRectSprites.Apply(rim, RoundedRectSprites.Ring(UIStyle.SwitchH * 0.5f, UIStyle.DividerPx));
            rim.color = UIStyle.FieldStroke;

            var knob = UIFactory.CreatePanel(KnobNode, track.rectTransform, Vector2.zero,
                new Vector2(UIStyle.SwitchKnob, UIStyle.SwitchKnob), UIStyle.TextSecondary);
            RoundedRectSprites.Apply(knob, RoundedRectSprites.Fill(UIStyle.SwitchKnob * 0.5f));
            knob.raycastTarget = false;

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = track;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = UIFactory.InteractiveColors();
            toggle.isOn = value;

            var self = rect.gameObject.AddComponent<SwitchControl>();
            self._toggle = toggle;
            self._track = track;
            self._rim = rim;
            self._knob = knob;
            toggle.onValueChanged.AddListener(_ => self.Sync());
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));
            self.Sync();
            return toggle;
        }

        public void Sync()
        {
            if (_toggle == null || _track == null || _knob == null || _rim == null) return;
            bool on = _toggle.isOn;
            _track.color = on ? UIStyle.Accent : UIStyle.Field;
            _rim.color = on ? UIStyle.Accent : UIStyle.FieldStroke;
            _knob.color = on ? UIStyle.TextOnAccent : UIStyle.TextSecondary;
            float travel = (UIStyle.SwitchW - UIStyle.SwitchH) * 0.5f;
            _knob.rectTransform.anchoredPosition = new Vector2(on ? travel : -travel, 0f);
        }

        public static void SetWithoutNotify(Toggle toggle, bool value)
        {
            toggle.SetIsOnWithoutNotify(value);
            var self = toggle.GetComponent<SwitchControl>();
            if (self != null) self.Sync();
        }
    }
}
