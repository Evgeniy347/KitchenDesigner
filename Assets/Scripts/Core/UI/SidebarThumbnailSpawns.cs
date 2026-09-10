using System;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    internal sealed class SidebarThumbnailSpawns : IElementSpawns
    {
        private GameObject? _spawned;

        public static Func<GameObject> For(SidebarCatalog.Item item) => () =>
        {
            var spawns = new SidebarThumbnailSpawns();
            spawns.Spawn(item);
            return spawns._spawned!;
        };

        public void Spawn(SidebarCatalog.Item item)
        {
            switch (item.kind)
            {
                case SidebarItemKind.Floor:
                    _spawned = ElementFactory.CreateFloor(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.LightSource:
                    _spawned = ElementFactory.CreateLightSource(item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Sink:
                    _spawned = ElementFactory.CreateSink(item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Cooktop:
                    _spawned = ElementFactory.CreateCooktop(item.name, Vector3.zero,
                        item.preset.applianceModel);
                    break;
                case SidebarItemKind.Oven:
                    _spawned = ElementFactory.CreateOven(item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Dishwasher:
                    _spawned = ElementFactory.CreateDishwasher(item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Drawer:
                {
                    var type = SidebarPresetResolution.DrawerTypeOf(item.preset.drawerType);
                    var color = SidebarPresetResolution.DrawerColorOf(item.preset.drawerColor);
                    var system = SidebarPresetResolution.DrawerSystemOf(item.preset.drawerSystem);
                    _spawned = ElementFactory.CreateDrawer(type, item.preset.drawerLength, color,
                        item.preset.drawerWidth, item.name, Vector3.zero, system);
                    break;
                }
                case SidebarItemKind.Window:
                    _spawned = ElementFactory.CreateWindow(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Door:
                    _spawned = ElementFactory.CreateDoor(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.RadiusTable:
                    _spawned = ElementFactory.CreateRadiusTable(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Stool:
                    _spawned = ElementFactory.CreateStool(item.dims, 0, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Chair:
                    _spawned = ElementFactory.CreateChair(item.dims, 0,
                        AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Sofa:
                    _spawned = ElementFactory.CreateSofa(item.dims, SofaElement.DefaultCornerRadiusMM,
                        SofaElement.DefaultSeatHeightMM, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Bed:
                    _spawned = ElementFactory.CreateBed(item.dims, true, true, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Pouffe:
                    _spawned = ElementFactory.CreatePouffe(item.dims, PouffeElement.DefaultCornerRadiusMM,
                        PouffeElement.DefaultSeatThicknessMM, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Toilet:
                    _spawned = ElementFactory.CreateToilet(ToiletElement.DefaultSeatHeightMM,
                        item.name, Vector3.zero);
                    break;
                case SidebarItemKind.WallHungToilet:
                    _spawned = ElementFactory.CreateWallHungToilet(
                        WallHungToiletElement.DefaultSeatHeightMM,
                        WallHungToiletElement.DefaultFlushPlateHeightMM, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Bathtub:
                    _spawned = ElementFactory.CreateBathtub(item.dims, BathtubElement.DefaultRimWidthMM,
                        BathtubElement.DefaultBowlDepthMM, BathtubElement.DefaultBowlRadiusMM,
                        BathtubElement.DefaultBowlFilletMM, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.BathMixer:
                    _spawned = ElementFactory.CreateBathMixer(BathMixerSpec.Default, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.ShowerColumn:
                    _spawned = ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default, item.name,
                        Vector3.zero);
                    break;
                case SidebarItemKind.Socket:
                    _spawned = ElementFactory.CreateSocket(WallDeviceSpec.Default, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.LightSwitch:
                    _spawned = ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                        item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Table:
                    _spawned = ElementFactory.CreateTable(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Pillar:
                    _spawned = ElementFactory.CreatePillar(item.preset.pillarMidHeightMM, item.name,
                        Vector3.zero);
                    break;
                case SidebarItemKind.ScrewLeg:
                    _spawned = ElementFactory.CreateScrewLeg(item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Pipe:
                    _spawned = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE,
                        PipeElementSpec.DEFAULT_LENGTH_MM, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.PipeFitting:
                    _spawned = item.preset.fittingKind switch
                    {
                        PipeNodeKind.Elbow => ElementFactory.CreatePipeElbow(item.name, Vector3.zero),
                        PipeNodeKind.Coupling => ElementFactory.CreatePipeCoupling(item.name, Vector3.zero),
                        PipeNodeKind.Tee => ElementFactory.CreatePipeTee(item.name, Vector3.zero),
                        PipeNodeKind.Cap => ElementFactory.CreatePipeCap(item.name, Vector3.zero),
                        PipeNodeKind.Supply => ElementFactory.CreatePipeSupply(item.name, Vector3.zero),
                        PipeNodeKind.Return => ElementFactory.CreatePipeReturn(item.name, Vector3.zero),
                        _ => ElementFactory.CreatePipeCoupling(item.name, Vector3.zero),
                    };
                    break;
                case SidebarItemKind.Panel:
                    _spawned = ElementFactory.Instance.CreatePanel(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.RadialShelf:
                    _spawned = ElementFactory.CreateRadialShelf(item.dims.x, item.dims.z, item.dims.y,
                        AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Facade:
                    _spawned = item.preset.facadeAssembled
                        ? ElementFactory.CreateAssembledFacade(item.dims, item.name, Vector3.zero,
                            AssembledFill.Blind)
                        : ElementFactory.CreateFacade(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Wall:
                    _spawned = ElementFactory.CreateWall(item.dims, item.name, Vector3.zero);
                    break;
                case SidebarItemKind.Board:
                default:
                    _spawned = ElementFactory.CreatePart(item.dims, item.name, Vector3.zero);
                    break;
            }
        }
    }
}
