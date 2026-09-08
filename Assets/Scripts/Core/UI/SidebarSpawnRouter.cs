using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarSpawnRouter
    {
        public static void Route(SidebarCatalog.Item item, IElementSpawns spawner)
        {
            switch (item.kind)
            {
                case SidebarItemKind.Floor:
                    spawner.SpawnFloor(item.dims, item.name);
                    break;
                case SidebarItemKind.LightSource:
                    spawner.SpawnLightSource(item.name);
                    break;
                case SidebarItemKind.Sink:
                    spawner.SpawnSink(item.name);
                    break;
                case SidebarItemKind.Cooktop:
                    spawner.SpawnCooktop(item.name, item.applianceModel);
                    break;
                case SidebarItemKind.Oven:
                    spawner.SpawnOven(item.name);
                    break;
                case SidebarItemKind.Dishwasher:
                    spawner.SpawnDishwasher(item.name);
                    break;
                case SidebarItemKind.Drawer:
                    spawner.SpawnDrawer(item.drawerType, item.drawerLength, item.drawerColor,
                        item.drawerWidth, item.name,
                        SidebarPresetResolution.DrawerSystemOf(item.drawerSystem));
                    break;
                case SidebarItemKind.Window:
                    spawner.SpawnWindow(item.dims, item.name);
                    break;
                case SidebarItemKind.Door:
                    spawner.SpawnDoor(item.dims, item.name);
                    break;
                case SidebarItemKind.RadiusTable:
                    spawner.SpawnRadiusTable(item.dims, item.name);
                    break;
                case SidebarItemKind.Stool:
                    spawner.SpawnStool(item.dims, item.name);
                    break;
                case SidebarItemKind.Chair:
                    spawner.SpawnChair(item.dims, item.name);
                    break;
                case SidebarItemKind.Sofa:
                    spawner.SpawnSofa(item.dims, item.name);
                    break;
                case SidebarItemKind.Bed:
                    spawner.SpawnBed(item.dims, item.name);
                    break;
                case SidebarItemKind.Pouffe:
                    spawner.SpawnPouffe(item.dims, item.name);
                    break;
                case SidebarItemKind.Toilet:
                    spawner.SpawnToilet(item.name);
                    break;
                case SidebarItemKind.WallHungToilet:
                    spawner.SpawnWallHungToilet(item.name);
                    break;
                case SidebarItemKind.Bathtub:
                    spawner.SpawnBathtub(item.dims, item.name);
                    break;
                case SidebarItemKind.BathMixer:
                    spawner.SpawnBathMixer(item.name);
                    break;
                case SidebarItemKind.ShowerColumn:
                    spawner.SpawnShowerColumn(item.name);
                    break;
                case SidebarItemKind.Socket:
                    spawner.SpawnSocket(item.name);
                    break;
                case SidebarItemKind.LightSwitch:
                    spawner.SpawnLightSwitch(item.name);
                    break;
                case SidebarItemKind.Table:
                    spawner.SpawnTable(item.dims, item.name);
                    break;
                case SidebarItemKind.Pillar:
                    spawner.SpawnPillar(item.pillarMidHeightMM, item.name);
                    break;
                case SidebarItemKind.ScrewLeg:
                    spawner.SpawnScrewLeg(item.name);
                    break;
                case SidebarItemKind.Pipe:
                    spawner.SpawnPipe(item.name);
                    break;
                case SidebarItemKind.PipeFitting:
                    spawner.SpawnPipeFitting(item.fittingKind, item.name);
                    break;
                case SidebarItemKind.Panel:
                    spawner.SpawnPanel(item.dims, item.name);
                    break;
                case SidebarItemKind.RadialShelf:
                    spawner.SpawnRadialShelf(item.dims, item.name);
                    break;
                case SidebarItemKind.AssembledFacade:
                    spawner.SpawnAssembledFacade(item.dims, item.name, AssembledFill.Blind);
                    break;
                case SidebarItemKind.Facade:
                    spawner.SpawnFacade(item.dims, item.name);
                    break;
                case SidebarItemKind.Wall:
                    spawner.SpawnWall(item.dims, item.name);
                    break;
                case SidebarItemKind.Board:
                    spawner.SpawnBoard(item.dims, item.name);
                    break;
                default:
                    spawner.SpawnBoard(item.dims, item.name);
                    break;
            }
        }
    }
}
