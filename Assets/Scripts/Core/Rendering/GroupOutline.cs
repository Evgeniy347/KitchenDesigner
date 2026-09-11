using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class GroupOutline
    {
        internal const string HolderName = "__GroupOutline";

        private GameObject? _holder;
        private ElementOutline? _outline;

        internal GameObject? Holder => _holder;

        public void Show(Vector3 center, Vector3 size)
        {
            if (_holder == null)
            {
                _holder = new GameObject(HolderName);
                _outline = _holder.AddComponent<ElementOutline>();
            }

            _holder.transform.SetPositionAndRotation(center, Quaternion.identity);
            _holder.transform.localScale = size;
            if (_outline != null) _outline.Show(selected: false);
        }

        public void Hide()
        {
            if (_outline != null) _outline.Hide();
        }

        public void Dispose()
        {
            if (_holder != null) DestroyNow.The(_holder);
            _holder = null;
            _outline = null;
        }
    }
}
