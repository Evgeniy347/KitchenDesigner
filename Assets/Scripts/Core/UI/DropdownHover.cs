using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Наведение на ПУНКТ выпадающего списка.
    ///
    /// У <see cref="TMP_Dropdown"/> нет события «курсор над пунктом»: сами пункты
    /// создаются только в момент раскрытия и умирают при закрытии, поэтому повесить
    /// на них триггеры заранее нельзя. Этот компонент ждёт появления объекта
    /// «Dropdown List» и навешивает PointerEnter/PointerExit на каждый созданный
    /// пункт.
    ///
    /// Нужно это выбору стороны для накладки текстуры: пока курсор стоит на «C»,
    /// соответствующая грань подсвечивается прямо в сцене (SideHighlighter.ShowFace) —
    /// иначе буква ничего не говорит о том, какая это грань.</summary>
    public class DropdownHover : MonoBehaviour
    {
        private TMP_Dropdown _dropdown = null!;
        private System.Action<int>? _onEnter;
        private System.Action? _onExit;
        private Transform? _list;

        /// <summary>Повесить обработку наведения. onEnter получает индекс пункта,
        /// onExit зовётся при уходе с пункта и при закрытии списка — подсветка не
        /// должна пережить закрытие.</summary>
        public static DropdownHover Attach(TMP_Dropdown dropdown,
            System.Action<int> onEnter, System.Action onExit)
        {
            var self = dropdown.gameObject.AddComponent<DropdownHover>();
            self._dropdown = dropdown;
            self._onEnter = onEnter;
            self._onExit = onExit;
            return self;
        }

        private void Update()
        {
            // Список Unity создаёт и уничтожает сам; на практике может и
            // просто деактивировать — ловим оба варианта.
            var list = _dropdown != null
                ? _dropdown.transform.Find("Dropdown List")
                : null;

            if (list == _list && (list == null || list.gameObject.activeInHierarchy))
                return;

            if (list == null || !list.gameObject.activeInHierarchy)
            {
                _list = null;
                _onExit?.Invoke();
                return;
            }

            _list = list;
            HookItems(list);
        }

        private void OnDisable()
        {
            if (_list == null) return;
            _list = null;
            _onExit?.Invoke();
        }

        /// <summary>Пункты — это активные Toggle внутри списка, в порядке опций.
        /// Шаблонный пункт к этому моменту уже выключен, поэтому в выборку
        /// не попадает.</summary>
        private void HookItems(Transform list)
        {
            var toggles = list.GetComponentsInChildren<Toggle>(includeInactive: false);
            var items = new List<Toggle>(toggles);
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                PointerHover.Attach(items[i].gameObject,
                    () => _onEnter?.Invoke(index),
                    () => _onExit?.Invoke());
            }
        }
    }
}
