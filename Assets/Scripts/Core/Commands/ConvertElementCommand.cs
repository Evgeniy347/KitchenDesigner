using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class ConvertElementCommand : IUndoCommand, ISerializableCommand
    {
        private readonly GameObject _go;
        private readonly string _partName;
        private readonly ElementData _before;
        private readonly ElementConverter.TargetType _target;
        private readonly Action<KitchenElement>? _retarget;

        public string Description => $"Convert {_partName}";

        public KitchenElement? CurrentElement => Resolve();

        private ConvertElementCommand(GameObject go, string partName, ElementData before,
            ElementConverter.TargetType target, Action<KitchenElement>? retarget)
        {
            _go = go;
            _partName = partName;
            _before = before;
            _target = target;
            _retarget = retarget;
        }

        public static bool IsStructural(ElementConverter.TargetType target) =>
            target == ElementConverter.TargetType.Part
            || target == ElementConverter.TargetType.Facade
            || target == ElementConverter.TargetType.AssembledFacade
            || target == ElementConverter.TargetType.RadialShelf;

        public static ConvertElementCommand? TryCreate(KitchenElement? source,
            ElementConverter.TargetType target, Action<KitchenElement>? retarget)
        {
            if (source == null) return null;
            if (!IsStructural(target)) return null;
            if (!ElementConverter.CanConvert(source)) return null;
            if (ElementConverter.GetElementType(source) == target) return null;

            return new ConvertElementCommand(source.gameObject, source.PartName,
                ElementCapture.FromElement(source), target, retarget);
        }

        public static KitchenElement? Run(KitchenElement? source,
            ElementConverter.TargetType target, Action<KitchenElement>? retarget)
        {
            var command = TryCreate(source, target, retarget);
            if (command == null) return null;
            CommandStack.Execute(command);
            return command.CurrentElement;
        }

        internal static ConvertElementCommand? FromRecord(KitchenElement element, CommandRecord record)
        {
            if (element == null || record == null) return null;
            if (record.convertState == null || record.convertState.Length == 0) return null;
            var before = record.convertState[0];
            if (before == null) return null;
            if (!Enum.IsDefined(typeof(ElementConverter.TargetType), record.convertTo)) return null;
            var target = (ElementConverter.TargetType)record.convertTo;
            if (!IsStructural(target)) return null;
            return new ConvertElementCommand(element.gameObject, before.name ?? "", before, target, null);
        }

        public CommandRecord? ToRecord(Func<KitchenElement, int> indexOf)
        {
            var element = Resolve();
            int index = element != null ? indexOf(element) : -1;
            if (index < 0) return null;
            return new CommandRecord
            {
                type = "convert",
                description = Description,
                elementIndex = index,
                convertTo = (int)_target,
                convertState = new[] { _before },
            };
        }

        public void Execute() => SwapTo(_target, null);

        public void Undo() => SwapTo(ElementConverter.TargetTypeOf(_before), _before);

        private void SwapTo(ElementConverter.TargetType target, ElementData? restore)
        {
            var element = Resolve();
            if (element == null) return;
            if (restore == null && ElementConverter.GetElementType(element) == target) return;

            var selection = SelectionManager.Instance;
            bool selected = selection != null && selection.IsSelected(element);
            if (selected && selection != null) selection.DeselectAll();

            if (element is FacadeElement openFacade && openFacade.IsOpen)
                openFacade.SetOpen(false);

            var converted = ElementConverter.Convert(element, target);
            if (converted == null) return;

            if (restore != null)
                ConvertedElementRestore.Apply(restore, converted);
            else if (_before.doorOpen && converted is FacadeElement newFacade && !newFacade.IsOpen)
                newFacade.SetOpen(true);

            if (selected && selection != null) selection.Select(converted);
            _retarget?.Invoke(converted);
        }

        private KitchenElement? Resolve()
        {
            if (_go != null)
            {
                var onObject = _go.GetComponent<KitchenElement>();
                if (onObject != null) return onObject;
            }

            var all = PartRegistry.All;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && string.Equals(all[i].PartName, _partName, StringComparison.Ordinal))
                    return all[i];
            return null;
        }
    }
}
