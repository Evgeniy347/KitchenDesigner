using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    internal interface IElementSpawns
    {
        void SpawnBoard(Vector3Int dims, string name);

        void SpawnFacade(Vector3Int dims, string name,
            int gapLeft, int gapRight, int gapTop, int gapBottom);

        void SpawnAssembledFacade(Vector3Int dims, string name, AssembledFill fill);

        void SpawnWall(Vector3Int dims, string name);

        void SpawnDrawer(string drawerType, int length, string colorName, int width,
            string name, DrawerSystem system);

        void SpawnTable(Vector3Int dims, string name);

        void SpawnRadiusTable(Vector3Int dims, string name);

        void SpawnStool(Vector3Int dims, string name);

        void SpawnChair(Vector3Int dims, string name);

        void SpawnSofa(Vector3Int dims, string name);

        void SpawnBed(Vector3Int dims, string name);

        void SpawnPouffe(Vector3Int dims, string name);

        void SpawnToilet(string name);

        void SpawnWallHungToilet(string name);

        void SpawnBathtub(Vector3Int dims, string name);

        void SpawnBathMixer(string name);

        void SpawnShowerColumn(string name);

        void SpawnSocket(string name);

        void SpawnLightSwitch(string name);

        void SpawnPanel(Vector3Int dims, string name,
            int gapLeft, int gapRight, int gapTop, int gapBottom);

        void SpawnRadialShelf(Vector3Int dims, string name);

        void SpawnWindow(Vector3Int dims, string name);

        void SpawnDoor(Vector3Int dims, string name);

        void SpawnScrewLeg(string name);

        void SpawnPillar(int midHeightMM, string name);
        void SpawnPipe(string name);

        void SpawnFloor(Vector3Int dims, string name);

        void SpawnSink(string name);

        void SpawnCooktop(string name, string model);

        void SpawnOven(string name);

        void SpawnDishwasher(string name);

        void SpawnLightSource(string name);
    }
}
