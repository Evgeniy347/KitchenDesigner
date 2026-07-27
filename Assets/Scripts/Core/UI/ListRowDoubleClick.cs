using UnityEngine;
using UnityEngine.EventSystems;

namespace KitchenDesigner.Core.UI
{
    public class ListRowDoubleClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action? OnDoubleClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount == 2)
                OnDoubleClick?.Invoke();
        }
    }
}
