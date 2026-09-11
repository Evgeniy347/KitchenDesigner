using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    internal static class ChoiceRowUndo
    {
        public static void Commit(KitchenElement? element, Action change,
            params KitchenElement?[] alsoTouched)
        {
            if (element == null)
            {
                change();
                return;
            }

            var targets = new List<KitchenElement> { element };
            foreach (var other in alsoTouched)
            {
                if (other is null) continue;
                if (ReferenceEquals(other, element)) continue;
                if (targets.Contains(other)) continue;
                targets.Add(other);
            }

            var before = new ElementPropertyBag[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                before[i] = UndoableProperties.Capture(targets[i]);

            change();

            var commands = new List<IUndoCommand>();
            for (int i = 0; i < targets.Count; i++)
            {
                var after = UndoableProperties.Capture(targets[i]);
                var command = SetPropertiesCommand.TryCreate(targets[i], before[i], after);
                if (command != null) commands.Add(command);
            }

            if (commands.Count == 0) return;
            CommandStack.Execute(commands.Count == 1
                ? commands[0]
                : new CompositeCommand("Свойства " + element.PartName, commands));
        }
    }
}
