using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class UIRowRegistry
    {
        private readonly List<(TMP_Text? label, Selectable? control)> _rows = new();

        public void Add(TMP_Text? label, Selectable? control)
        {
            if (label == null || control == null) return;
            _rows.Add((label, control));
        }

        public IEnumerable<(TMP_Text? label, Selectable? control)> Rows => _rows;

        public void Sync()
        {
            foreach (var (label, control) in _rows)
            {
                if (label == null || control == null) continue;
                UIRowEnabled.SyncRow(label, control);
            }
        }
    }
}
