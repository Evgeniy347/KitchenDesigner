using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using KitchenDesigner.Core.Construction;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class WallFieldsEditor : ElementFieldsEditor
    {
        public const string LoadBearingNode = "CtxWallLoadBearing";
        public const string LoadBearingLabel = "Несущая";
        public const string MasonryNode = "CtxWallMasonry";
        public const string MasonryLabel = "Технология";
        public const string JointNode = "КладкаШов";
        public const string JointLabel = "Шов";
        public const string WasteNode = "КладкаЗапас";
        public const string WasteLabel = "Запас";

        private Toggle? _loadBearing;
        private TMP_Dropdown? _masonry;
        private TMP_InputField? _joint;
        private TMP_InputField? _waste;

        public WallFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element.GetComponent<Wall>() != null;

        public static List<string> MasonryOptions()
        {
            var options = new List<string>(MasonryUnit.Table.Count);
            foreach (var unit in MasonryUnit.Table) options.Add(unit.Title);
            return options;
        }

        public override void Build()
        {
            var isWall = RowVisibility.When(
                () => Host.Target != null && Host.Target.GetComponent<Wall>() != null);
            _loadBearing = Rows.Toggle(LoadBearingNode, LoadBearingLabel, true, OnLoadBearingToggled,
                isWall, RowGap, hint: "element.wall.loadBearing");

            var masonryRow = Rows.NamedDropdown(MasonryNode, MasonryLabel, MasonryOptions(),
                OnMasonryPicked, isWall);
            _masonry = masonryRow.dropdown;
            HintBadge.AttachAfterLabel(masonryRow.label as TextMeshProUGUI,
                hint: "element.wall.masonry");

            var jointRow = Rows.LabelledNumberField(JointLabel, isWall, "мм", JointNode);
            _joint = jointRow.field;
            HintBadge.AttachAfterLabel(jointRow.label, hint: "element.wall.joint");

            var wasteRow = Rows.LabelledNumberField(WasteLabel, isWall, "%", WasteNode);
            _waste = wasteRow.field;
            HintBadge.AttachAfterLabel(wasteRow.label, hint: "element.wall.waste");
        }

        public override IEnumerable<TMP_InputField?> ArithmeticFields()
        {
            yield return _joint;
            yield return _waste;
        }

        public override void Show(KitchenElement element) => WriteRows(element);

        public override void Refresh(KitchenElement element)
        {
            var wall = element.GetComponent<Wall>();
            if (wall == null) return;
            _loadBearing?.SetIsOnWithoutNotify(wall.LoadBearing);
            _masonry?.SetValueWithoutNotify((int)wall.Masonry);
            _masonry?.RefreshShownValue();
            Fields.RefreshUnfocused(_joint, wall.JointMm.ToString());
            Fields.RefreshUnfocused(_waste, wall.WastePct.ToString());
        }

        public override void Track(KitchenElement element)
        {
            var wall = element.GetComponent<Wall>();
            var state = SetWallMasonryCommand.Snapshot(wall);
            Fields.Track(_joint, state.JointMm.ToString());
            Fields.Track(_waste, state.WastePct.ToString());
        }

        public override void Apply(KitchenElement element)
        {
            var wall = element.GetComponent<Wall>();
            if (wall == null || _joint == null || _waste == null) return;

            var before = SetWallMasonryCommand.Snapshot(wall);
            var after = new WallMasonry(before.Technology,
                Fields.ParseInt(_joint, before.JointMm),
                Fields.ParseInt(_waste, before.WastePct));
            if (!after.Equals(before))
                CommandStack.Execute(new SetWallMasonryCommand(wall, before, after));
        }

        public override void AfterApply(KitchenElement element) => WriteRows(element);

        private void WriteRows(KitchenElement element)
        {
            var wall = element.GetComponent<Wall>();
            if (wall == null) return;
            _loadBearing?.SetIsOnWithoutNotify(wall.LoadBearing);
            _masonry?.SetValueWithoutNotify((int)wall.Masonry);
            _masonry?.RefreshShownValue();
            if (_joint != null) _joint.text = wall.JointMm.ToString();
            if (_waste != null) _waste.text = wall.WastePct.ToString();
        }

        private void OnLoadBearingToggled(bool on)
        {
            var wall = Host.Target != null ? Host.Target.GetComponent<Wall>() : null;
            if (wall == null) return;
            bool before = wall.LoadBearing;
            if (before == on) return;
            CommandStack.Execute(new SetWallLoadBearingCommand(wall, before, on));
        }

        private void OnMasonryPicked(int index)
        {
            var wall = Host.Target != null ? Host.Target.GetComponent<Wall>() : null;
            if (wall == null) return;
            var before = SetWallMasonryCommand.Snapshot(wall);
            var after = new WallMasonry((MasonryTechnology)index, before.JointMm, before.WastePct);
            if (after.Equals(before)) return;
            CommandStack.Execute(new SetWallMasonryCommand(wall, before, after));
        }
    }
}
