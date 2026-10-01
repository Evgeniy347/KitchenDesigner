using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuLevelSection
    {
        private readonly IContextMenuHost _host;
        private TMP_Dropdown? _dropdown;
        private readonly List<string> _ids = new();

        private KitchenElement? Target => _host.Target;

        public ContextMenuLevelSection(IContextMenuHost host) => _host = host;

        public void Build() =>
            _dropdown = _host.Rows.Dropdown(Loc.T("element.common.level"), new List<string>(), OnSelected,
                RowVisibility.Always, "CtxLevel");

        public void ShowFor(KitchenElement element)
        {
            if (_dropdown == null) return;

            var levels = new List<Level>(LevelRegistry.Snapshot());
            levels.Sort((a, b) => a.floorElevationMm.CompareTo(b.floorElevationMm));

            _ids.Clear();
            var options = new List<string>();
            foreach (var level in levels)
            {
                _ids.Add(level.id);
                options.Add(level.name);
            }

            _dropdown.ClearOptions();
            _dropdown.AddOptions(options);
            UIFactory.FitDropdownItems(_dropdown);
            int index = _ids.IndexOf(LevelRegistry.LevelOf(element).id);
            _dropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
            _dropdown.RefreshShownValue();
        }

        private void OnSelected(int index)
        {
            var target = Target;
            if (target == null || index < 0 || index >= _ids.Count) return;
            var id = _ids[index];
            if (id == LevelRegistry.LevelOf(target).id) return;

            CommandStack.Execute(new MoveToLevelCommand(new[] { target }, id));
            SceneVisibilityManager.Invalidate();
        }
    }
}
