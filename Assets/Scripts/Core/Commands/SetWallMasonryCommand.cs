using KitchenDesigner.Core.Construction;

namespace KitchenDesigner.Core
{
    public readonly struct WallMasonry
    {
        public readonly MasonryTechnology Technology;
        public readonly int JointMm;
        public readonly int WastePct;

        public WallMasonry(MasonryTechnology technology, int jointMm, int wastePct)
        {
            Technology = technology;
            JointMm = jointMm;
            WastePct = wastePct;
        }

        public bool Equals(WallMasonry other) =>
            Technology == other.Technology && JointMm == other.JointMm && WastePct == other.WastePct;
    }

    public class SetWallMasonryCommand : IUndoCommand
    {
        private readonly Wall? _wall;
        private readonly WallMasonry _before;
        private readonly WallMasonry _after;

        public string Description => $"Set wall masonry {_wall?.name}";

        public SetWallMasonryCommand(Wall wall, WallMasonry before, WallMasonry after)
        {
            _wall = wall;
            _before = before;
            _after = after;
        }

        public static WallMasonry Snapshot(Wall? wall) =>
            wall == null
                ? new WallMasonry(MasonryTechnology.BrickSingle,
                    KitchenSettings.CONSTRUCTION_JOINT_DEFAULT_MM,
                    KitchenSettings.CONSTRUCTION_WASTE_DEFAULT_PCT)
                : new WallMasonry(wall.Masonry, wall.JointMm, wall.WastePct);

        public void Execute() => Apply(_after);

        public void Undo() => Apply(_before);

        private void Apply(WallMasonry state)
        {
            if (_wall == null) return;
            _wall.Masonry = state.Technology;
            _wall.JointMm = state.JointMm;
            _wall.WastePct = state.WastePct;
        }
    }
}
