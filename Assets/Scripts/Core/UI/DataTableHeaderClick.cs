using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KitchenDesigner.Core.UI
{
    public sealed class DataTableHeaderClick : MonoBehaviour, IPointerClickHandler
    {
        private Action? _onClick;

        public void Init(Action onClick) => _onClick = onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _onClick?.Invoke();
        }
    }
}
