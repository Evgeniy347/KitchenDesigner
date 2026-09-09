using System;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SidebarThumbnailSpawns : IElementSpawns
    {
        private GameObject? _spawned;

        public static Func<GameObject> For(SidebarCatalog.Item item) => () => Spawn(item);

        internal static GameObject Spawn(SidebarCatalog.Item item)
        {
            var spawns = new SidebarThumbnailSpawns();
            SidebarSpawnRouter.Route(item, spawns);
            return spawns._spawned!;
        }

        public void SpawnBoard(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreatePart(dims, name, Vector3.zero);

        public void SpawnFacade(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateFacade(dims, name, Vector3.zero);

        public void SpawnAssembledFacade(Vector3Int dims, string name, AssembledFill fill) =>
            _spawned = ElementFactory.CreateAssembledFacade(dims, name, Vector3.zero, fill);

        public void SpawnWall(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateWall(dims, name, Vector3.zero);

        public void SpawnDrawer(string drawerType, int length, string colorName, int width,
            string name, DrawerSystem system)
        {
            var type = SidebarPresetResolution.DrawerTypeOf(drawerType);
            var color = SidebarPresetResolution.DrawerColorOf(colorName);
            _spawned = ElementFactory.CreateDrawer(type, length, color, width, name, Vector3.zero, system);
        }

        public void SpawnTable(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateTable(dims, name, Vector3.zero);

        public void SpawnRadiusTable(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateRadiusTable(dims, name, Vector3.zero);

        public void SpawnStool(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateStool(dims, 0, name, Vector3.zero);

        public void SpawnChair(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateChair(dims, 0, AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT,
                name, Vector3.zero);

        public void SpawnSofa(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateSofa(dims, SofaElement.DefaultCornerRadiusMM,
                SofaElement.DefaultSeatHeightMM, name, Vector3.zero);

        public void SpawnBed(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateBed(dims, true, true, name, Vector3.zero);

        public void SpawnPouffe(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreatePouffe(dims, PouffeElement.DefaultCornerRadiusMM,
                PouffeElement.DefaultSeatThicknessMM, name, Vector3.zero);

        public void SpawnToilet(string name) =>
            _spawned = ElementFactory.CreateToilet(ToiletElement.DefaultSeatHeightMM, name, Vector3.zero);

        public void SpawnWallHungToilet(string name) =>
            _spawned = ElementFactory.CreateWallHungToilet(WallHungToiletElement.DefaultSeatHeightMM,
                WallHungToiletElement.DefaultFlushPlateHeightMM, name, Vector3.zero);

        public void SpawnBathtub(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateBathtub(dims, BathtubElement.DefaultRimWidthMM,
                BathtubElement.DefaultBowlDepthMM, BathtubElement.DefaultBowlRadiusMM,
                BathtubElement.DefaultBowlFilletMM, name, Vector3.zero);

        public void SpawnBathMixer(string name) =>
            _spawned = ElementFactory.CreateBathMixer(BathMixerSpec.Default, name, Vector3.zero);

        public void SpawnShowerColumn(string name) =>
            _spawned = ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default, name, Vector3.zero);

        public void SpawnSocket(string name) =>
            _spawned = ElementFactory.CreateSocket(WallDeviceSpec.Default, name, Vector3.zero);

        public void SpawnLightSwitch(string name) =>
            _spawned = ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                name, Vector3.zero);

        public void SpawnPanel(Vector3Int dims, string name) =>
            _spawned = ElementFactory.Instance.CreatePanel(dims, name, Vector3.zero);

        public void SpawnRadialShelf(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateRadialShelf(dims.x, dims.z, dims.y,
                AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, name, Vector3.zero);

        public void SpawnWindow(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateWindow(dims, name, Vector3.zero);

        public void SpawnDoor(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateDoor(dims, name, Vector3.zero);

        public void SpawnScrewLeg(string name) =>
            _spawned = ElementFactory.CreateScrewLeg(name, Vector3.zero);

        public void SpawnPillar(int midHeightMM, string name) =>
            _spawned = ElementFactory.CreatePillar(midHeightMM, name, Vector3.zero);

        public void SpawnPipe(string name) =>
            _spawned = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE,
                PipeElementSpec.DEFAULT_LENGTH_MM, name, Vector3.zero);

        public void SpawnPipeFitting(PipeNodeKind kind, string name) =>
            _spawned = kind switch
            {
                PipeNodeKind.Elbow => ElementFactory.CreatePipeElbow(name, Vector3.zero),
                PipeNodeKind.Coupling => ElementFactory.CreatePipeCoupling(name, Vector3.zero),
                PipeNodeKind.Tee => ElementFactory.CreatePipeTee(name, Vector3.zero),
                PipeNodeKind.Cap => ElementFactory.CreatePipeCap(name, Vector3.zero),
                PipeNodeKind.Supply => ElementFactory.CreatePipeSupply(name, Vector3.zero),
                PipeNodeKind.Return => ElementFactory.CreatePipeReturn(name, Vector3.zero),
                _ => ElementFactory.CreatePipeCoupling(name, Vector3.zero),
            };

        public void SpawnFloor(Vector3Int dims, string name) =>
            _spawned = ElementFactory.CreateFloor(dims, name, Vector3.zero);

        public void SpawnSink(string name) =>
            _spawned = ElementFactory.CreateSink(name, Vector3.zero);

        public void SpawnCooktop(string name, string model) =>
            _spawned = ElementFactory.CreateCooktop(name, Vector3.zero, model);

        public void SpawnOven(string name) =>
            _spawned = ElementFactory.CreateOven(name, Vector3.zero);

        public void SpawnDishwasher(string name) =>
            _spawned = ElementFactory.CreateDishwasher(name, Vector3.zero);

        public void SpawnLightSource(string name) =>
            _spawned = ElementFactory.CreateLightSource(name, Vector3.zero);
    }
}
