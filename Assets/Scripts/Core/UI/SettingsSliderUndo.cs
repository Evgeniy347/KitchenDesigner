using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SettingsSliderUndo : MonoBehaviour, IPointerUpHandler, IDeselectHandler
    {
        private Slider _slider = null!;
        private string _title = "";
        private Action<float> _write = _ => { };
        private Action _afterApply = () => { };

        private float _from;
        private bool _changed;

        public static SettingsSliderUndo Attach(Slider slider, string title,
            Action<float> write, Action afterApply)
        {
            var undo = slider.gameObject.AddComponent<SettingsSliderUndo>();
            undo._slider = slider;
            undo._title = title;
            undo._write = write;
            undo._afterApply = afterApply;
            undo._from = slider.value;

            slider.onValueChanged.AddListener(_ => undo._changed = true);
            return undo;
        }

        public void Sync()
        {
            if (_slider == null) return;
            _from = _slider.value;
            _changed = false;
        }

        public void Flush()
        {
            if (_slider == null || !_changed) return;

            float from = _from;
            float to = _slider.value;
            _changed = false;
            _from = to;

            if (Mathf.Approximately(from, to)) return;
            SetSettingCommand.Push(_title, _write, from, to, _afterApply);
        }

        public void OnPointerUp(PointerEventData eventData) => Flush();

        public void OnDeselect(BaseEventData eventData) => Flush();
    }
}
