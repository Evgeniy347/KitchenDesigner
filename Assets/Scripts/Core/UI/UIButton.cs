using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class UIButton : Button
    {
        private readonly List<Graphic> _content = new List<Graphic>();
        private readonly List<Color> _bright = new List<Color>();
        private bool _dimmed;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            SyncContent();
        }

        private void SyncContent()
        {
            bool dim = !UIRowEnabled.IsEnabled(this);
            if (dim == _dimmed) return;
            _dimmed = dim;
            if (dim) Dim();
            else Restore();
        }

        private void Dim()
        {
            _content.Clear();
            _bright.Clear();
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || graphic.gameObject == gameObject) continue;
                _content.Add(graphic);
                _bright.Add(graphic.color);
                graphic.color = UIStyle.TextDisabled;
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _content.Count; i++)
                if (_content[i] != null) _content[i].color = _bright[i];
            _content.Clear();
            _bright.Clear();
        }
    }
}
