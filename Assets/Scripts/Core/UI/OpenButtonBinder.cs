using System;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class OpenButtonBinder
    {
        private readonly Func<IOpenable?> _resolve;
        private TMP_Text? _label;

        public OpenButtonBinder(Func<IOpenable?> resolve)
        {
            _resolve = resolve;
        }

        public void Bind(TMP_Text label)
        {
            _label = label;
            Refresh();
        }

        public void Toggle()
        {
            _resolve()?.CycleOpenState();
            Refresh();
        }

        public void Refresh()
        {
            if (_label == null) return;
            _label.text = _resolve()?.OpenActionLabel ?? OpenLabels.Open;
        }
    }
}
