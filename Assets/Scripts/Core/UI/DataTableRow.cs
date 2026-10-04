using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class DataTableRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private DataTable? _table;
        private Image? _background;
        private GameObject? _bar;
        private bool _interactive;
        private bool _hovered;
        private bool _selected;

        public DataRow? Row { get; private set; }

        public void Init(DataTable table, DataRow row, Image background, GameObject bar, bool interactive)
        {
            _table = table;
            Row = row;
            _background = background;
            _bar = bar;
            _interactive = interactive;
            background.raycastTarget = interactive;
        }

        public void Paint(bool selected)
        {
            _selected = selected;
            if (_background != null)
                _background.color = selected ? UIStyle.RowSelected : _hovered ? UIStyle.RowHover : UIStyle.Transparent;
            if (_bar != null) _bar.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_interactive) return;
            _hovered = true;
            Paint(_selected);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Paint(_selected);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_interactive || _table == null || Row == null) return;
            if (eventData.clickCount >= 2) _table.Activate(Row);
            else _table.Select(Row);
        }
    }
}
