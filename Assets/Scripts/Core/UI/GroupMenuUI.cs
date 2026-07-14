using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Меню группы (ПКМ-клик по объекту): для несвязанного мультивыделения —
    /// кнопка «Связать»; для связанного объекта — настройки группы (имя, запрет
    /// перемещения, разорвать связь). Замок закрыт = связано, открыт = разорвать.</summary>
    public class GroupMenuUI : MonoBehaviour
    {
        public static GroupMenuUI Instance { get; private set; } = null!;

        private GameObject _root = null!;
        private GameObject _linkRoot = null!;
        private GameObject _groupRoot = null!;
        private TMP_InputField _nameField = null!;
        private Toggle _lockMove = null!;
        private LinkGroup? _group;

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("GroupMenu", canvas, Vector2.zero, new Vector2(280, 240));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(0, 40);
            _root = panel.gameObject;

            _linkRoot = NewRoot(panel.transform);
            UIFactory.CreateLabel("GmLinkTitle", _linkRoot.transform, "Связать выделенные?", 18,
                new Vector2(0, 40), new Vector2(260, 28), TextAnchor.MiddleCenter);
            UIFactory.CreateButton("GmLink", _linkRoot.transform, "Связать (замок)",
                new Vector2(0, -6), new Vector2(180, 40), DoLink);

            _groupRoot = NewRoot(panel.transform);
            UIFactory.CreateLabel("GmTitle", _groupRoot.transform, "Группа", 20,
                new Vector2(0, 70), new Vector2(260, 28), TextAnchor.MiddleCenter);
            UIFactory.CreateLabel("GmNameLbl", _groupRoot.transform, "Имя", 15,
                new Vector2(-100, 32), new Vector2(60, 24));
            _nameField = UIFactory.CreateInputField("GmName", _groupRoot.transform, "",
                new Vector2(35, 32), new Vector2(160, 24));
            _nameField.onEndEdit.AddListener(t => { if (_group != null) _group.name = t; });
            _lockMove = UIFactory.CreateToggle("GmLockMove", _groupRoot.transform, "Запретить перемещение", false,
                new Vector2(0, -4), new Vector2(248, 26), v => { if (_group != null) GroupManager.SetMovable(_group, !v); });
            UIFactory.CreateButton("GmEdit", _groupRoot.transform, "Редактировать модуль",
                new Vector2(0, -42), new Vector2(200, 36), DoEditModule);
            UIFactory.CreateButton("GmUnlink", _groupRoot.transform, "Разорвать связь",
                new Vector2(0, -82), new Vector2(200, 36), DoUnlink);

            _root.SetActive(false);

            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;
        }

        private GameObject NewRoot(Transform parent)
        {
            var rt = UIFactory.CreateRect("Root", parent);
            UIFactory.AnchorCenter(rt);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(280, 240);
            return rt.gameObject;
        }

        public void Open(KitchenElement element)
        {
            if (element == null) return;
            _group = GroupManager.GroupOf(element);

            if (_group != null)
            {
                _linkRoot.SetActive(false);
                _groupRoot.SetActive(true);
                _nameField.SetTextWithoutNotify(_group.name);
                _lockMove.SetIsOnWithoutNotify(!_group.movable);
                _root.SetActive(true);
                return;
            }

            var sel = SelectionManager.Instance;
            if (sel == null || sel.SelectedElements.Count < 2) { Close(); return; }
            _linkRoot.SetActive(true);
            _groupRoot.SetActive(false);
            _root.SetActive(true);
        }

        public void Close()
        {
            _group = null;
            if (_root != null) _root.SetActive(false);
        }

        private void DoLink()
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return;
            var members = new List<KitchenElement>(sel.SelectedElements);
            var g = GroupManager.Link(members);
            if (g != null)
            {
                sel.SelectOnly(GroupManager.MembersOf(g));
                Open(members[0]); // переключиться в настройки группы
            }
        }

        private void DoUnlink()
        {
            if (_group == null) return;
            GroupManager.Unlink(_group);
            Close();
        }

        // Вход в режим редактирования модуля: детали редактируются поштучно,
        // остальная сцена блокируется (ModuleEditMode).
        private void DoEditModule()
        {
            if (_group == null) return;
            var g = _group;
            Close();
            ModuleEditMode.Enter(g);
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.DeselectAll();
        }

        private void Update()
        {
            if (_root != null && _root.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Close();
        }

        // Закрываем меню, если выделение ушло с нашей группы.
        private void OnSelectionChanged(KitchenElement? element)
        {
            if (_root == null || !_root.activeSelf || _group == null) return;
            if (element == null || GroupManager.GroupOf(element) != _group)
                Close();
        }

        private void OnDestroy()
        {
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }
    }
}
