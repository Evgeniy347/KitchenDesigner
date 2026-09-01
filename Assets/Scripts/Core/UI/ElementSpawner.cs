using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class ElementSpawner
    {
        private readonly Func<Vector3> _groundPointInFrontOfCamera;
        private readonly Func<PlacementController?> _placement;

        public ElementSpawner(Func<Vector3> groundPointInFrontOfCamera,
            Func<PlacementController?> placement)
        {
            _groundPointInFrontOfCamera = groundPointInFrontOfCamera;
            _placement = placement;
        }

        public void SpawnPreset(int index)
        {
            if (index < 0 || index >= AppConstants.PRESET_DIMENSIONS_MM.Length) return;
            var dims = AppConstants.PRESET_DIMENSIONS_MM[index];
            CommitImmediate(CreateBoardGo(dims, $"Board {dims.x}x{dims.y}x{dims.z}"));
        }

        public void SpawnBoard(Vector3Int dims, string name) =>
            BeginPlacement(CreateBoardGo(dims, name));

        public void SpawnFacade(Vector3Int dims, string name,
            int gapLeft, int gapRight, int gapTop, int gapBottom) =>
            PlaceCenteredOnGround(dims.y, pos =>
                ElementFactory.CreateFacade(dims, name, pos, gapLeft, gapRight, gapTop, gapBottom));

        public void SpawnAssembledFacade(Vector3Int dims, string name, AssembledFill fill) =>
            PlaceCenteredOnGround(dims.y, pos =>
                ElementFactory.CreateAssembledFacade(dims, name, pos, fill));

        public void SpawnWall(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateWall(dims, name, pos));

        public void SpawnDrawer(string drawerType, int length, string colorName, int width,
            string name, DrawerSystem system)
        {
            var type = drawerType switch
            {
                "B" => DrawerType.B,
                "C" => DrawerType.C,
                "D" => DrawerType.D,
                _ => DrawerType.A
            };
            var color = colorName.ToLowerInvariant() switch
            {
                "white" => DrawerColor.White,
                "black" => DrawerColor.Black,
                _ => DrawerColor.Anthracite
            };
            PlaceCenteredOnGround(DrawerConstants.GetMinOpeningHeight(type), pos =>
                ElementFactory.CreateDrawer(type, length, color, width,
                    DrawerLinks.UniqueName(name), pos, system));
        }

        public void SpawnTable(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateTable(dims, name, pos));

        public void SpawnRadiusTable(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateRadiusTable(dims, name, pos));

        public void SpawnStool(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateStool(dims, 0, name, pos));

        public void SpawnChair(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateChair(dims, 0,
                AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT, name, pos));

        public void SpawnSofa(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateSofa(dims,
                SofaElement.DefaultCornerRadiusMM, SofaElement.DefaultSeatHeightMM,
                name, pos));

        public void SpawnPanel(Vector3Int dims, string name,
            int gapLeft, int gapRight, int gapTop, int gapBottom) =>
            PlaceCenteredOnGround(dims.y, pos =>
                ElementFactory.Instance.CreatePanel(dims, name, pos, gapLeft, gapRight, gapTop, gapBottom));

        public void SpawnRadialShelf(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos =>
                ElementFactory.CreateRadialShelf(dims.x, dims.z, dims.y,
                    AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, name, pos));

        public void SpawnWindow(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateWindow(dims, name, pos));

        public void SpawnDoor(Vector3Int dims, string name) =>
            PlaceCenteredOnGround(dims.y, pos => ElementFactory.CreateDoor(dims, name, pos));

        public void SpawnPillar(int midHeightMM, string name)
        {
            int totalH = PillarElement.TopHeightMM + midHeightMM + PillarElement.BottomHeightMM;
            PlaceCenteredOnGround(totalH, pos => ElementFactory.CreatePillar(midHeightMM, name, pos));
        }

        public void SpawnFloor(Vector3Int dims, string name) =>
            PlaceAtHeightUnaffectedByGrid(-dims.y * 0.5f * AppConstants.MM_TO_UNITS,
                pos => ElementFactory.CreateFloor(dims, name, pos));

        public void SpawnSink(string name) =>
            PlaceAtHeightUnaffectedByGrid(WorktopHeightMeters,
                pos => ElementFactory.CreateSink(name, pos));

        public void SpawnCooktop(string name, string model) =>
            PlaceAtHeightUnaffectedByGrid(WorktopHeightMeters,
                pos => ElementFactory.CreateCooktop(name, pos, model));

        public void SpawnOven(string name) =>
            PlaceAtHeightUnaffectedByGrid(
                OvenElement.ModelDimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS,
                pos => ElementFactory.CreateOven(name, pos));

        public void SpawnDishwasher(string name) =>
            PlaceAtHeightUnaffectedByGrid(
                DishwasherElement.ModelDimensionsMM.y * 0.5f * AppConstants.MM_TO_UNITS,
                pos => ElementFactory.CreateDishwasher(name, pos));

        public void SpawnLightSource(string name) =>
            PlaceAtHeightUnaffectedByGrid(PendantHeightMeters,
                pos => ElementFactory.CreateLightSource(name, pos));

        public const float WorktopHeightMeters = 0.9f;
        public const float PendantHeightMeters = 2.2f;

        internal Vector3 CenteredOnGroundPoint(int heightMM)
        {
            Vector3 pos = _groundPointInFrontOfCamera();
            pos.y = heightMM * 0.5f * AppConstants.MM_TO_UNITS;
            return GridManager.SnapToGrid(pos);
        }

        internal Vector3 GroundPointAtHeightUnaffectedByGrid(float centerYMeters)
        {
            Vector3 pos = GridManager.SnapToGrid(_groundPointInFrontOfCamera());
            pos.y = centerYMeters;
            return pos;
        }

        private void PlaceCenteredOnGround(int heightMM, Func<Vector3, GameObject> create) =>
            BeginPlacement(create(CenteredOnGroundPoint(heightMM)));

        private void PlaceAtHeightUnaffectedByGrid(float centerYMeters, Func<Vector3, GameObject> create) =>
            BeginPlacement(create(GroundPointAtHeightUnaffectedByGrid(centerYMeters)));

        private GameObject CreateBoardGo(Vector3Int dims, string name) =>
            ElementFactory.CreatePart(dims, name, CenteredOnGroundPoint(dims.y));

        private void BeginPlacement(GameObject go)
        {
            var element = go.GetComponent<KitchenElement>();
            if (element == null) return;
            var placement = _placement();
            if (placement != null)
                placement.Begin(element);
            else
                CommitImmediate(go);
        }

        private static void CommitImmediate(GameObject go)
        {
            var element = go.GetComponent<KitchenElement>();
            if (element == null) return;
            CommandStack.Execute(new CreateCommand(go));
            SelectionManager.Instance?.Select(element);
        }
    }
}
