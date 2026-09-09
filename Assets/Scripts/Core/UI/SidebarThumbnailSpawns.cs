using System;
using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    internal static class SidebarThumbnailSpawns
    {
        private static readonly Dictionary<SidebarItemKind, Func<GameObject>> ByKind =
            new Dictionary<SidebarItemKind, Func<GameObject>>
            {
                { SidebarItemKind.Board, () => ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Board", Vector3.zero) },
                { SidebarItemKind.Wall, () => ElementFactory.CreateWall(new Vector3Int(2000, 2500, 100), "Wall", Vector3.zero) },
                { SidebarItemKind.Facade, () => ElementFactory.CreateFacade(new Vector3Int(600, 720, 18), "Facade", Vector3.zero) },
                { SidebarItemKind.Panel, () => ElementFactory.CreatePanel(new Vector3Int(600, 720, 18), "Panel", Vector3.zero) },
                { SidebarItemKind.RadialShelf, () => ElementFactory.CreateRadialShelf(600, 350, 18, 50, "RadialShelf", Vector3.zero) },
                { SidebarItemKind.Drawer, () => ElementFactory.CreateDrawer(DrawerType.B, 400, DrawerColor.White, 500,
                    "Drawer", Vector3.zero) },
                { SidebarItemKind.Table, () => ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Table", Vector3.zero) },
                { SidebarItemKind.RadiusTable, () => ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700),
                    "RadiusTable", Vector3.zero) },
                { SidebarItemKind.Stool, () => ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 20, "Stool", Vector3.zero) },
                { SidebarItemKind.Chair, () => ElementFactory.CreateChair(new Vector3Int(450, 900, 450), 20, 450,
                    "Chair", Vector3.zero) },
                { SidebarItemKind.Sofa, () => ElementFactory.CreateSofa(new Vector3Int(1800, 850, 900), 30, 420,
                    "Sofa", Vector3.zero) },
                { SidebarItemKind.Pouffe, () => ElementFactory.CreatePouffe(new Vector3Int(400, 400, 400), 20, 100,
                    "Pouffe", Vector3.zero) },
                { SidebarItemKind.Bed, () => ElementFactory.CreateBed(new Vector3Int(1600, 500, 2000), true, true,
                    "Bed", Vector3.zero) },
                { SidebarItemKind.Pillar, () => ElementFactory.CreatePillar(1200, "Pillar", Vector3.zero) },
                { SidebarItemKind.ScrewLeg, () => ElementFactory.CreateScrewLeg("ScrewLeg", Vector3.zero) },
                { SidebarItemKind.Pipe, () => ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 500, "Pipe", Vector3.zero) },
                { SidebarItemKind.Sink, () => ElementFactory.CreateSink("Sink", Vector3.zero) },
                { SidebarItemKind.Cooktop, () => ElementFactory.CreateCooktop("Cooktop", Vector3.zero) },
                { SidebarItemKind.Oven, () => ElementFactory.CreateOven("Oven", Vector3.zero) },
                { SidebarItemKind.Dishwasher, () => ElementFactory.CreateDishwasher("Dishwasher", Vector3.zero) },
                { SidebarItemKind.Toilet, () => ElementFactory.CreateToilet(400, "Toilet", Vector3.zero) },
                { SidebarItemKind.WallHungToilet, () => ElementFactory.CreateWallHungToilet(400, 1100,
                    "WallHungToilet", Vector3.zero) },
                { SidebarItemKind.Bathtub, () => ElementFactory.CreateBathtub(
                    new Vector3Int(BathtubElement.DefaultWidthMM, BathtubElement.DefaultHeightMM,
                        BathtubElement.DefaultDepthMM),
                    BathtubElement.DefaultRimWidthMM, BathtubElement.DefaultBowlDepthMM,
                    BathtubElement.DefaultBowlRadiusMM, BathtubElement.DefaultBowlFilletMM,
                    "Bathtub", Vector3.zero) },
                { SidebarItemKind.BathMixer, () => ElementFactory.CreateBathMixer(BathMixerSpec.Default, "BathMixer", Vector3.zero) },
                { SidebarItemKind.ShowerColumn, () => ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default,
                    "ShowerColumn", Vector3.zero) },
                { SidebarItemKind.Socket, () => ElementFactory.CreateSocket(WallDeviceSpec.Default, "Socket", Vector3.zero) },
                { SidebarItemKind.LightSwitch, () => ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
                    "LightSwitch", Vector3.zero) },
                { SidebarItemKind.Floor, () => ElementFactory.CreateFloor(new Vector3Int(3000, 20, 3000), "Floor", Vector3.zero) },
                { SidebarItemKind.LightSource, () => ElementFactory.CreateLightSource("LightSource", Vector3.zero) },
                { SidebarItemKind.Window, () => ElementFactory.CreateWindow(new Vector3Int(1200, 1400, 150),
                    "Window", Vector3.zero) },
                { SidebarItemKind.Door, () => ElementFactory.CreateDoor(new Vector3Int(900, 2000, 150), "Door", Vector3.zero) },
            };

        private static readonly Dictionary<PipeNodeKind, Func<GameObject>> ByFitting =
            new Dictionary<PipeNodeKind, Func<GameObject>>
            {
                { PipeNodeKind.Elbow, () => ElementFactory.CreatePipeElbow("PipeElbow", Vector3.zero) },
                { PipeNodeKind.Coupling, () => ElementFactory.CreatePipeCoupling("PipeCoupling", Vector3.zero) },
                { PipeNodeKind.Tee, () => ElementFactory.CreatePipeTee("PipeTee", Vector3.zero) },
                { PipeNodeKind.Cap, () => ElementFactory.CreatePipeCap("PipeCap", Vector3.zero) },
                { PipeNodeKind.Supply, () => ElementFactory.CreatePipeSupply("PipeSupply", Vector3.zero) },
                { PipeNodeKind.Return, () => ElementFactory.CreatePipeReturn("PipeReturn", Vector3.zero) },
            };

        public static Func<GameObject>? For(SidebarCatalog.Item item)
        {
            if (item.kind == SidebarItemKind.PipeFitting)
                return ByFitting.TryGetValue(item.fittingKind, out var byFitting) ? byFitting : null;
            if (item.kind == SidebarItemKind.Facade && item.facadeAssembled)
                return () => ElementFactory.CreateAssembledFacade(new Vector3Int(600, 720, 18),
                    "AssembledFacade", Vector3.zero, AssembledFill.Blind);
            return ByKind.TryGetValue(item.kind, out var byKind) ? byKind : null;
        }
    }
}
