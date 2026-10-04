using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    [ExecuteAlways]
    public sealed class DataTableShownHook : MonoBehaviour
    {
        private Action? _onShown;

        public void Init(Action onShown) => _onShown = onShown;

        private void OnEnable() => _onShown?.Invoke();
    }
}
