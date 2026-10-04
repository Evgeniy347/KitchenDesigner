using TMPro;
using UnityEngine.EventSystems;

namespace KitchenDesigner.Core.UI
{
    public sealed class RestingInputField : TMP_InputField
    {
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            base.OnUpdateSelected(eventData);
            if (!isFocused) InputFieldScroll.Rest(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            InputFieldScroll.Rest(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            InputFieldScroll.Rest(this);
        }
    }
}
