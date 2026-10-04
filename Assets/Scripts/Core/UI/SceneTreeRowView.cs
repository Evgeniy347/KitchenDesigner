using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SceneTreeRowView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public const string BarNode = "SelectionBar";

        private Image? _background;
        private GameObject? _bar;
        private GameObject? _menu;
        private bool _hovered;
        private bool _selected;

        internal SceneTree.Node? Node { get; private set; }

        public bool Selected => _selected;

        public bool Hovered => _hovered;

        internal void Init(SceneTree.Node node, Image background, GameObject bar, GameObject? menu)
        {
            Node = node;
            _background = background;
            _bar = bar;
            _menu = menu;
            background.raycastTarget = false;
        }

        public void Paint(bool selected)
        {
            _selected = selected;
            if (_background != null)
                _background.color = selected ? UIStyle.RowSelected : _hovered ? UIStyle.RowHover : UIStyle.Transparent;
            if (_bar != null) _bar.SetActive(selected);
            if (_menu != null) _menu.SetActive(_hovered);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Paint(_selected);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Paint(_selected);
        }
    }
}
