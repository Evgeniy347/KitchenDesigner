namespace KitchenDesigner.Core
{
    public class SetWallLoadBearingCommand : IUndoCommand
    {
        private readonly Wall _wall;
        private readonly bool _before, _after;
        public string Description => $"Set wall load-bearing {_wall.name}";

        public SetWallLoadBearingCommand(Wall wall, bool before, bool after)
        {
            _wall = wall;
            _before = before;
            _after = after;
        }

        public void Execute() => _wall.LoadBearing = _after;
        public void Undo() => _wall.LoadBearing = _before;
    }
}
