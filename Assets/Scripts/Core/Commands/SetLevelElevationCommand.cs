using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class SetLevelElevationCommand : IUndoCommand
    {
        private readonly Level _level;
        private readonly int _before;
        private readonly int _after;
        private readonly List<KitchenElement> _elements;
        private readonly float _deltaUnits;

        public string Description => $"Отметка уровня «{_level.name}»";

        public SetLevelElevationCommand(Level level, int newElevationMm)
        {
            _level = level;
            _before = level.floorElevationMm;
            _after = newElevationMm;
            _deltaUnits = (_after - _before) * AppConstants.MM_TO_UNITS;

            _elements = new List<KitchenElement>();
            var levels = LevelRegistry.Snapshot();
            foreach (var el in PartRegistry.All)
            {
                if (el != null && LevelResolution.ResolveElementLevel(el.LevelId, levels).id == level.id)
                    _elements.Add(el);
            }
        }

        public void Execute()
        {
            _level.floorElevationMm = _after;
            Shift(_deltaUnits);
            SceneVisibilityManager.Invalidate();
            LevelRegistry.Touch();
        }

        public void Undo()
        {
            _level.floorElevationMm = _before;
            Shift(-_deltaUnits);
            SceneVisibilityManager.Invalidate();
            LevelRegistry.Touch();
        }

        private void Shift(float deltaUnits)
        {
            if (deltaUnits == 0f) return;
            foreach (var el in _elements)
                if (el != null) el.transform.position += new Vector3(0f, deltaUnits, 0f);
        }
    }
}
