using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SettingsSliderUndo
    {
        private readonly Slider _slider;
        private readonly string _title;
        private readonly Action<float> _write;
        private readonly Action _afterApply;

        private float _committed;
        private bool _dragging;

        private SettingsSliderUndo(Slider slider, string title, Action<float> write, Action afterApply)
        {
            _slider = slider;
            _title = title;
            _write = write;
            _afterApply = afterApply;
            _committed = slider.value;
        }

        public static SettingsSliderUndo Attach(Slider slider, string title,
            Action<float> write, Action afterApply)
        {
            var undo = new SettingsSliderUndo(slider, title, write, afterApply);

            var trigger = slider.gameObject.GetComponent<EventTrigger>()
                ?? slider.gameObject.AddComponent<EventTrigger>();
            Listen(trigger, EventTriggerType.PointerDown, undo.BeginDrag);
            Listen(trigger, EventTriggerType.PointerUp, undo.EndDrag);

            slider.onValueChanged.AddListener(_ => undo.ValueChanged());
            return undo;
        }

        public void Sync() => _committed = _slider.value;

        private void BeginDrag()
        {
            _dragging = true;
            _committed = _slider.value;
        }

        private void EndDrag()
        {
            _dragging = false;
            PushOneStep();
        }

        private void ValueChanged()
        {
            if (_dragging) return;
            PushOneStep();
        }

        private void PushOneStep()
        {
            if (_slider == null) return;

            float from = _committed;
            float to = _slider.value;
            if (Mathf.Approximately(from, to)) return;

            _committed = to;
            SetSettingCommand.Push(_title, _write, from, to, _afterApply);
        }

        private static void Listen(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
