using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal sealed class WallFieldsEditor : ElementFieldsEditor
    {
        public const string LoadBearingNode = "CtxWallLoadBearing";
        public const string LoadBearingLabel = "Несущая";

        private Toggle? _loadBearing;

        public WallFieldsEditor(IContextMenuHost host) : base(host) { }

        public override bool Handles(KitchenElement element) => element.GetComponent<Wall>() != null;

        public override void Build()
        {
            var isWall = RowVisibility.When(
                () => Host.Target != null && Host.Target.GetComponent<Wall>() != null);
            _loadBearing = Rows.Toggle(LoadBearingNode, LoadBearingLabel, true, OnLoadBearingToggled,
                isWall, RowGap);
        }

        public override void Show(KitchenElement element) => WriteToggle(element);

        public override void Refresh(KitchenElement element) => WriteToggle(element);

        public override void AfterApply(KitchenElement element) => WriteToggle(element);

        private void WriteToggle(KitchenElement element)
        {
            var wall = element.GetComponent<Wall>();
            if (wall != null) _loadBearing?.SetIsOnWithoutNotify(wall.LoadBearing);
        }

        private void OnLoadBearingToggled(bool on)
        {
            var wall = Host.Target != null ? Host.Target.GetComponent<Wall>() : null;
            if (wall == null) return;
            bool before = wall.LoadBearing;
            if (before == on) return;
            CommandStack.Execute(new SetWallLoadBearingCommand(wall, before, on));
        }
    }
}
