using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class DropdownHover : MonoBehaviour
    {
        private const string ListObjectName = "Dropdown List";

        private TMP_Dropdown _dropdown = null!;
        private System.Action<int>? _onEnter;
        private System.Action? _onExit;
        private Transform? _openList;

        public static DropdownHover Attach(TMP_Dropdown dropdown,
            System.Action<int> onEnter, System.Action onExit)
        {
            var self = dropdown.gameObject.AddComponent<DropdownHover>();
            self._dropdown = dropdown;
            self._onEnter = onEnter;
            self._onExit = onExit;
            return self;
        }

        private Transform? FindOpenList()
        {
            var list = _dropdown != null ? _dropdown.transform.Find(ListObjectName) : null;
            return list != null && list.gameObject.activeInHierarchy ? list : null;
        }

        private void Update()
        {
            var list = FindOpenList();
            if (list == _openList) return;

            _openList = list;
            if (list is null) _onExit?.Invoke();
            else HookItems(list);
        }

        private void OnDisable()
        {
            if (_openList == null) return;
            _openList = null;
            _onExit?.Invoke();
        }

        private void HookItems(Transform list)
        {
            var itemsInOptionOrder = list.GetComponentsInChildren<Toggle>(includeInactive: false);
            for (int i = 0; i < itemsInOptionOrder.Length; i++)
            {
                int index = i;
                PointerHover.Attach(itemsInOptionOrder[i].gameObject,
                    () => _onEnter?.Invoke(index),
                    () => _onExit?.Invoke());
            }
        }
    }
}
