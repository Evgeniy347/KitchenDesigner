using System;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    public sealed class ElementSpawner : IElementSpawns
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

        public void Spawn(SidebarCatalog.Item item)
        {
            switch (item.kind)
            {
                case SidebarItemKind.Floor:
                    PlaceAtHeightUnaffectedByGrid(-AppConstants.HalfHeightUnits(item.dims.y),
                        pos => ElementFactory.CreateFloor(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.LightSource:
                    PlaceAtHeightUnaffectedByGrid(PendantHeightMeters,
                        pos => ElementFactory.CreateLightSource(item.name, pos));
                    break;
                case SidebarItemKind.Sink:
                    PlaceAtHeightUnaffectedByGrid(WorktopHeightMeters,
                        pos => ElementFactory.CreateSink(item.name, pos));
                    break;
                case SidebarItemKind.Cooktop:
                    PlaceAtHeightUnaffectedByGrid(WorktopHeightMeters,
                        pos => ElementFactory.CreateCooktop(item.name, pos, item.preset.applianceModel));
                    break;
                case SidebarItemKind.Oven:
                    PlaceAtHeightUnaffectedByGrid(
                        AppConstants.HalfHeightUnits(OvenElement.ModelDimensionsMM.y),
                        pos => ElementFactory.CreateOven(item.name, pos));
                    break;
                case SidebarItemKind.Dishwasher:
                    PlaceAtHeightUnaffectedByGrid(
                        AppConstants.HalfHeightUnits(DishwasherElement.ModelDimensionsMM.y),
                        pos => ElementFactory.CreateDishwasher(item.name, pos));
                    break;
                case SidebarItemKind.WashingMachine:
                case SidebarItemKind.Dryer:
                {
                    var kind = SidebarPresetResolution.LaundryKindOf(item.preset.laundryKind);
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateLaundryMachine(kind, item.dims, item.name, pos));
                    break;
                }
                case SidebarItemKind.Drawer:
                {
                    var type = SidebarPresetResolution.DrawerTypeOf(item.preset.drawerType);
                    var color = SidebarPresetResolution.DrawerColorOf(item.preset.drawerColor);
                    var system = SidebarPresetResolution.DrawerSystemOf(item.preset.drawerSystem);
                    PlaceCenteredOnGround(DrawerConstants.GetMinOpeningHeight(type), pos =>
                        ElementFactory.CreateDrawer(type, item.preset.drawerLength, color,
                            item.preset.drawerWidth, DrawerLinks.UniqueName(item.name), pos, system));
                    break;
                }
                case SidebarItemKind.Window:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateWindow(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.Door:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateDoor(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.RadiusTable:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateRadiusTable(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.Stool:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateStool(item.dims, 0, item.name, pos));
                    break;
                case SidebarItemKind.Chair:
                    PlaceCenteredOnGround(item.dims.y, pos => ElementFactory.CreateChair(item.dims, 0,
                        AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT, item.name, pos));
                    break;
                case SidebarItemKind.Sofa:
                    PlaceCenteredOnGround(item.dims.y, pos => ElementFactory.CreateSofa(item.dims,
                        SofaElement.DefaultCornerRadiusMM, SofaElement.DefaultSeatHeightMM,
                        item.name, pos));
                    break;
                case SidebarItemKind.Bed:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateBed(item.dims, true, true, item.name, pos));
                    break;
                case SidebarItemKind.Pouffe:
                    PlaceCenteredOnGround(item.dims.y, pos => ElementFactory.CreatePouffe(item.dims,
                        PouffeElement.DefaultCornerRadiusMM, PouffeElement.DefaultSeatThicknessMM,
                        item.name, pos));
                    break;
                case SidebarItemKind.Toilet:
                    PlaceCenteredOnGround(ToiletElement.ModelDimensionsMM.y,
                        pos => ElementFactory.CreateToilet(ToiletElement.DefaultSeatHeightMM,
                            item.name, pos));
                    break;
                case SidebarItemKind.WallHungToilet:
                    PlaceCenteredOnGround(WallHungToiletElement.ModelDimensionsMM.y,
                        pos => ElementFactory.CreateWallHungToilet(
                            WallHungToiletElement.DefaultSeatHeightMM,
                            WallHungToiletElement.DefaultFlushPlateHeightMM, item.name, pos));
                    break;
                case SidebarItemKind.Bathtub:
                    PlaceCenteredOnGround(item.dims.y, pos => ElementFactory.CreateBathtub(item.dims,
                        BathtubElement.DefaultRimWidthMM, BathtubElement.DefaultBowlDepthMM,
                        BathtubElement.DefaultBowlRadiusMM, BathtubElement.DefaultBowlFilletMM,
                        item.name, pos));
                    break;
                case SidebarItemKind.BathMixer:
                    PlaceAtHeightUnaffectedByGrid(
                        BathMixerLayout.CentreAboveFloorMM(BathMixerSpec.Default)
                            * AppConstants.MM_TO_UNITS,
                        pos => ElementFactory.CreateBathMixer(BathMixerSpec.Default, item.name, pos));
                    break;
                case SidebarItemKind.ShowerColumn:
                    PlaceAtHeightUnaffectedByGrid(
                        ShowerColumnLayout.CentreAboveFloorMM(ShowerColumnSpec.Default)
                            * AppConstants.MM_TO_UNITS,
                        pos => ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default,
                            item.name, pos));
                    break;
                case SidebarItemKind.Socket:
                    PlaceAtHeightUnaffectedByGrid(
                        WallDeviceLayout.SocketCentreAboveFloorMM * AppConstants.MM_TO_UNITS,
                        pos => ElementFactory.CreateSocket(WallDeviceSpec.Default, item.name, pos));
                    break;
                case SidebarItemKind.LightSwitch:
                    PlaceAtHeightUnaffectedByGrid(
                        WallDeviceLayout.SwitchCentreAboveFloorMM * AppConstants.MM_TO_UNITS,
                        pos => ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                            item.name, pos));
                    break;
                case SidebarItemKind.Table:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateTable(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.Pillar:
                {
                    int midHeightMM = item.preset.pillarMidHeightMM;
                    int totalH = PillarElement.TopHeightMM + midHeightMM + PillarElement.BottomHeightMM;
                    PlaceCenteredOnGround(totalH, pos =>
                        ElementFactory.CreatePillar(midHeightMM, item.name, pos));
                    break;
                }
                case SidebarItemKind.ScrewLeg:
                {
                    int totalH = ScrewLegSpec.BodyHeightMM(ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM,
                        ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM);
                    PlaceCenteredOnGround(totalH, pos => ElementFactory.CreateScrewLeg(item.name, pos));
                    break;
                }
                case SidebarItemKind.Pipe:
                    PlaceCenteredOnGround(PipeElementSpec.DEFAULT_LENGTH_MM,
                        pos => ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE,
                            PipeElementSpec.DEFAULT_LENGTH_MM, item.name, pos));
                    break;
                case SidebarItemKind.PipeFitting:
                    PlaceCenteredOnGround(
                        PipeFittingSpec.RoundedMm(
                            PipeFittingSpec.HeightMm(item.preset.fittingKind, PipeSpec.DEFAULT_SIZE)),
                        pos => CreateFitting(item.preset.fittingKind, item.name, pos));
                    break;
                case SidebarItemKind.Panel:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.Instance.CreatePanel(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.RadialShelf:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateRadialShelf(item.dims.x, item.dims.z, item.dims.y,
                            AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, item.name, pos));
                    break;
                case SidebarItemKind.Facade:
                    if (item.preset.facadeAssembled)
                        PlaceCenteredOnGround(item.dims.y, pos =>
                            ElementFactory.CreateAssembledFacade(item.dims, item.name, pos,
                                AssembledFill.Blind));
                    else
                        PlaceCenteredOnGround(item.dims.y, pos =>
                            ElementFactory.CreateFacade(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.Wall:
                    PlaceCenteredOnGround(item.dims.y, pos =>
                        ElementFactory.CreateWall(item.dims, item.name, pos));
                    break;
                case SidebarItemKind.Board:
                default:
                    BeginPlacement(CreateBoardGo(item.dims, item.name));
                    break;
            }
        }

        private static GameObject CreateFitting(PipeNodeKind kind, string name, Vector3 pos) =>
            kind switch
            {
                PipeNodeKind.Elbow => ElementFactory.CreatePipeElbow(name, pos),
                PipeNodeKind.Coupling => ElementFactory.CreatePipeCoupling(name, pos),
                PipeNodeKind.Tee => ElementFactory.CreatePipeTee(name, pos),
                PipeNodeKind.Cap => ElementFactory.CreatePipeCap(name, pos),
                PipeNodeKind.Supply => ElementFactory.CreatePipeSupply(name, pos),
                PipeNodeKind.Return => ElementFactory.CreatePipeReturn(name, pos),
                _ => ElementFactory.CreatePipeCoupling(name, pos),
            };

        public const float WorktopHeightMeters = 0.9f;
        public const float PendantHeightMeters = 2.2f;

        internal Vector3 CenteredOnGroundPoint(int heightMM)
        {
            Vector3 pos = _groundPointInFrontOfCamera();
            pos.y = AppConstants.HalfHeightUnits(heightMM);
            return GridManager.SnapToGrid(pos);
        }

        internal Vector3 GroundPointAtHeightUnaffectedByGrid(float centerYMeters)
        {
            Vector3 pos = GridManager.SnapToGrid(_groundPointInFrontOfCamera());
            pos.y = centerYMeters;
            return pos;
        }

        private void PlaceCenteredOnGround(int heightMM, Func<Vector3, GameObject> create) =>
            BeginPlacement(LiftByBottomSkirt(create(CenteredOnGroundPoint(heightMM))));

        private static GameObject LiftByBottomSkirt(GameObject go)
        {
            var element = go.GetComponent<KitchenElement>();
            if (element != null)
                element.transform.position += new Vector3(0f,
                    GappedBox.BottomSkirtUnits(element.Gaps), 0f);
            return go;
        }

        private void PlaceAtHeightUnaffectedByGrid(float centerYMeters, Func<Vector3, GameObject> create) =>
            BeginPlacement(create(GroundPointAtHeightUnaffectedByGrid(centerYMeters)));

        private GameObject CreateBoardGo(Vector3Int dims, string name) =>
            LiftByBottomSkirt(ElementFactory.CreatePart(dims, name,
                CenteredOnGroundPoint(dims.y)));

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
