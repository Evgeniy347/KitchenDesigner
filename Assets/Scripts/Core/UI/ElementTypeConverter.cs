using System;
using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ElementTypeConverter
    {
        internal enum Group { None, Structural, Drawer }

        internal enum Choice { Part, Facade, AssembledFacade, RadialShelf, DrawerGtv, DrawerMovento }

        private static readonly (Choice choice, string label)[] StructuralChoices =
        {
            (Choice.Part, "Деталь"),
            (Choice.Facade, "Фасад"),
            (Choice.AssembledFacade, "Сборный фасад"),
            (Choice.RadialShelf, "Радиусная полка"),
        };

        private static readonly (Choice choice, string label)[] DrawerChoices =
        {
            (Choice.DrawerGtv, "Ящик GTV"),
            (Choice.DrawerMovento, "Ящик Movento"),
        };

        private readonly IContextMenuHost _host;
        private readonly Action<KitchenElement> _reopen;
        private readonly List<Choice> _offered = new();

        private TMP_Dropdown? _dropdown;

        public ElementTypeConverter(IContextMenuHost host, Action<KitchenElement> reopen)
        {
            _host = host;
            _reopen = reopen;
        }

        public void Build()
        {
            var labels = new List<string>();
            foreach (var (_, label) in StructuralChoices) labels.Add(label);
            _dropdown = _host.Rows.Dropdown("Тип", labels, Select,
                RowVisibility.When(() => Convertible(_host.Target)), "CtxType");
        }

        public static bool Convertible(KitchenElement? element) => GroupOf(element) != Group.None;

        public static Group GroupOf(KitchenElement? element)
        {
            if (element == null) return Group.None;
            if (element is DrawerElement) return Group.Drawer;
            return ElementConverter.CanConvert(element) ? Group.Structural : Group.None;
        }

        public static Choice CurrentChoiceOf(KitchenElement element)
        {
            if (element is DrawerElement drawer)
                return drawer.System == DrawerSystem.Movento ? Choice.DrawerMovento : Choice.DrawerGtv;
            if (element is AssembledFacadeElement) return Choice.AssembledFacade;
            if (element is RadialShelfElement) return Choice.RadialShelf;
            if (element is FacadeElement) return Choice.Facade;
            return Choice.Part;
        }

        public void ShowFor(KitchenElement element)
        {
            if (_dropdown == null) return;
            _offered.Clear();
            _dropdown.ClearOptions();

            var group = GroupOf(element);
            if (group == Group.None) return;

            var set = group == Group.Drawer ? DrawerChoices : StructuralChoices;
            var labels = new List<string>(set.Length);
            foreach (var (choice, label) in set)
            {
                _offered.Add(choice);
                labels.Add(label);
            }
            _dropdown.AddOptions(labels);

            int index = _offered.IndexOf(CurrentChoiceOf(element));
            _dropdown.SetValueWithoutNotify(index < 0 ? 0 : index);
            _dropdown.RefreshShownValue();
        }

        internal void Select(int index)
        {
            var target = _host.Target;
            if (target == null || index < 0 || index >= _offered.Count) return;
            var choice = _offered[index];

            if (choice == Choice.DrawerGtv || choice == Choice.DrawerMovento)
                SwitchDrawerSystem(target, choice);
            else
                ConvertStructural(target, choice);
        }

        private void SwitchDrawerSystem(KitchenElement target, Choice choice)
        {
            if (!(target is DrawerElement drawer)) return;
            var system = choice == Choice.DrawerMovento ? DrawerSystem.Movento : DrawerSystem.Gtv;
            if (drawer.System == system) return;

            ChoiceRowUndo.Commit(drawer, () => drawer.System = system, drawer.FindPaired());
            _reopen(drawer);
            RefreshHighlights();
        }

        private void ConvertStructural(KitchenElement target, Choice choice)
        {
            var targetType = choice switch
            {
                Choice.Facade => ElementConverter.TargetType.Facade,
                Choice.AssembledFacade => ElementConverter.TargetType.AssembledFacade,
                Choice.RadialShelf => ElementConverter.TargetType.RadialShelf,
                _ => ElementConverter.TargetType.Part,
            };
            if (ElementConverter.GetElementType(target) == targetType) return;

            var converted = ElementConverter.Convert(target, targetType);
            if (converted != null) _reopen(converted);
            RefreshHighlights();
        }

        private static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }
    }
}
