using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal static class LevelEdits
    {
        public static string Mm(int value) => NumberFormat.Input(value, 0);

        public static void Rename(Level level, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || newName == level.name) return;
            CommandStack.Execute(new RenameLevelCommand(level, newName));
        }

        public static void SetElevation(Level level, string text, TMP_InputField field)
        {
            bool parsed = NumberFormat.TryParseInt(text, out int mm);
            if (parsed && mm != level.floorElevationMm)
            {
                CommandStack.Execute(new SetLevelElevationCommand(level, mm));
                UIFactory.SetHighlight(field, false);
            }
            else if (!parsed)
            {
                UIFactory.SetErrorHighlight(field);
            }
            field.SetTextWithoutNotify(Mm(level.floorElevationMm));
        }

        public static void SetHeight(Level level, string text, TMP_InputField field)
        {
            bool parsed = NumberFormat.TryParseInt(text, out int mm);
            if (parsed && mm > 0 && mm != level.heightMm)
            {
                CommandStack.Execute(new SetLevelHeightCommand(level, mm));
                UIFactory.SetHighlight(field, false);
            }
            else if (!parsed || mm <= 0)
            {
                UIFactory.SetErrorHighlight(field);
            }
            field.SetTextWithoutNotify(Mm(level.heightMm));
        }

        public static void AddAboveTheTop()
        {
            var current = LevelRegistry.Snapshot();
            var next = LevelPlacement.NextAbove(current, KitchenSettings.Instance.ConstructionFloorHeightMm);

            if (LevelRegistry.Items.Count == 0)
            {
                var seedAndNext = new List<IUndoCommand>
                {
                    new CreateLevelCommand(current[0]),
                    new CreateLevelCommand(next),
                };
                CommandStack.Execute(new CompositeCommand($"Добавить уровень «{next.name}»", seedAndNext));
            }
            else
            {
                CommandStack.Execute(new CreateLevelCommand(next));
            }
        }

        public static void Delete(Level level)
        {
            if (LevelRegistry.IndexOf(level.id) < 0) return;
            CommandStack.Execute(new DeleteLevelCommand(level.id));
        }
    }
}
