using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public class SetFloorplanMetadataCommand : IUndoCommand
    {
        private readonly List<RoomData> _beforeRooms, _afterRooms;
        private readonly List<FloorplanScopeData> _beforeScopes, _afterScopes;
        public string Description { get; }

        public SetFloorplanMetadataCommand(string id, List<RoomData> rooms, string[] elements)
        {
            Description = $"Set floorplan metadata {id}";
            _beforeRooms = new List<RoomData>(ProjectRooms.Items);
            _beforeScopes = new List<FloorplanScopeData>(ProjectFloorplans.Items);
            _afterRooms = new List<RoomData>();
            foreach (var room in _beforeRooms)
                if (!string.Equals(room.floorplanId, id, System.StringComparison.OrdinalIgnoreCase)) _afterRooms.Add(room);
            _afterRooms.AddRange(rooms);
            _afterScopes = new List<FloorplanScopeData>();
            foreach (var scope in _beforeScopes)
                if (!string.Equals(scope.id, id, System.StringComparison.OrdinalIgnoreCase)) _afterScopes.Add(scope);
            _afterScopes.Add(new FloorplanScopeData { id = id, elements = elements });
        }

        public void Execute() { ProjectRooms.Set(_afterRooms); ProjectFloorplans.Set(_afterScopes); }
        public void Undo() { ProjectRooms.Set(_beforeRooms); ProjectFloorplans.Set(_beforeScopes); }
    }
}
