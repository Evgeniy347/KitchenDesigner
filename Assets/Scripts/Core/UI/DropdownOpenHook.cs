using System;
using System.Collections;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal sealed class DropdownOpenHook : MonoBehaviour
    {
        public Action? OnOpen;
        public Action? OnAfterShow;

        private void OnEnable()
        {
            OnOpen?.Invoke();
            StartCoroutine(DelayedAfterShow());
        }

        private IEnumerator DelayedAfterShow()
        {
            yield return null;
            OnAfterShow?.Invoke();
        }
    }
}
