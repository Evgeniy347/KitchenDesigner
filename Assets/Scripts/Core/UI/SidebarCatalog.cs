using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    public static class SidebarCatalog
    {
        public const string DefaultDrawerType = "A";
        public const int DefaultDrawerLengthMM = 350;
        public const string DefaultDrawerColor = "Anthracite";
        public const int DefaultDrawerWidthMM = 400;
        public const string DefaultDrawerSystem = SidebarPresetResolution.DefaultDrawerSystem;
        public const string MoventoDrawerSystem = SidebarPresetResolution.MoventoDrawerSystem;

        public struct Preset
        {
            public string applianceModel;
            public int pillarMidHeightMM;
            public string drawerType;
            public int drawerLength;
            public string drawerColor;
            public int drawerWidth;
            public string drawerSystem;
            public string laundryKind;
            public PipeNodeKind fittingKind;
            public bool facadeAssembled;

            public static Preset Default() => new Preset
            {
                applianceModel = "",
                pillarMidHeightMM = PillarElement.MidHeightMM_Default,
                drawerType = DefaultDrawerType,
                drawerLength = DefaultDrawerLengthMM,
                drawerColor = DefaultDrawerColor,
                drawerWidth = DefaultDrawerWidthMM,
                drawerSystem = DefaultDrawerSystem,
                laundryKind = LaundryMachineBody.WASHER_TYPE_ID,
                fittingKind = PipeNodeKind.Coupling,
                facadeAssembled = false,
            };
        }

        public struct Item
        {
            public string name;
            public Vector3Int dims;
            public SidebarItemKind kind;
            public Preset preset;
            internal string tileTitle;

            public Item(string name, Vector3Int dims,
                SidebarItemKind kind = SidebarItemKind.Board)
            {
                this.name = name; this.dims = dims; this.kind = kind;
                preset = Preset.Default();
                tileTitle = name;
            }

            public string DisplayName =>
                !string.IsNullOrEmpty(preset.applianceModel) && name.EndsWith(preset.applianceModel)
                ? name.Substring(0, name.Length - preset.applianceModel.Length).TrimEnd()
                : name;

            public EditModeManager.Category Category => kind switch
            {
                SidebarItemKind.Window => EditModeManager.Category.Always,
                SidebarItemKind.Door => EditModeManager.Category.Always,
                SidebarItemKind.Wall => EditModeManager.Category.Room,
                SidebarItemKind.Floor => EditModeManager.Category.Room,
                SidebarItemKind.Foundation => EditModeManager.Category.Room,
                SidebarItemKind.FloorSlab => EditModeManager.Category.Room,
                SidebarItemKind.Fence => EditModeManager.Category.Room,
                SidebarItemKind.Duct => EditModeManager.Category.Room,
                SidebarItemKind.Grille => EditModeManager.Category.Room,
                SidebarItemKind.Roof => EditModeManager.Category.Room,
                SidebarItemKind.Insulation => EditModeManager.Category.Room,
                SidebarItemKind.VentGap => EditModeManager.Category.Room,
                SidebarItemKind.Cladding => EditModeManager.Category.Room,
                _ => EditModeManager.Category.Regular,
            };
        }

        public struct Group
        {
            public string title;
            public Sprite icon;
            public List<Item> items;
        }

        private readonly struct GroupMeta
        {
            public readonly SidebarGroupKey key;
            public readonly string title;
            public readonly Sprite icon;

            public GroupMeta(SidebarGroupKey key, string title, Sprite icon)
            {
                this.key = key; this.title = title; this.icon = icon;
            }
        }

        private static IEnumerable<GroupMeta> GroupTable() => new[]
        {
            new GroupMeta(SidebarGroupKey.Board, Loc.T("catalog.group.board"), IconFactory.Shelf),
            new GroupMeta(SidebarGroupKey.Facade, Loc.T("catalog.group.facade"), IconFactory.Facade),
            new GroupMeta(SidebarGroupKey.Drawer, Loc.T("catalog.group.drawer"), IconFactory.Drawer),
            new GroupMeta(SidebarGroupKey.Furniture, Loc.T("catalog.group.furniture"), IconFactory.Furniture),
            new GroupMeta(SidebarGroupKey.Appliance, Loc.T("catalog.group.appliance"), IconFactory.Appliance),
            new GroupMeta(SidebarGroupKey.Sanitary, Loc.T("catalog.group.sanitary"), IconFactory.Faucet),
            new GroupMeta(SidebarGroupKey.Room, Loc.T("catalog.group.room"), IconFactory.Room),
            new GroupMeta(SidebarGroupKey.Construction, Loc.T("catalog.group.construction"), IconFactory.Brickwork),
        };

        private static readonly LocalizedCache<List<Group>> Catalog = new LocalizedCache<List<Group>>(BuildGroups);

        public static List<Group> Build() => Catalog.Value;

        private static List<Group> BuildGroups()
        {
            var rows = RowsWithTileTitleInherited().ToList();
            return GroupTable().Select(meta => new Group
            {
                title = meta.title,
                icon = meta.icon,
                items = rows.Where(r => r.Group == meta.key).Select(r => r.Item).ToList(),
            }).ToList();
        }

        private static IEnumerable<SidebarCatalogRow> TypesAndPresets()
        {
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Board, Loc.T("catalog.board.shelf"),
                new Item(Loc.T("catalog.board.shelf"), new Vector3Int(600, 400, 16)));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Board, Loc.T("elementType.radialShelf"),
                new Item(Loc.T("elementType.radialShelf"), new Vector3Int(600, 400, 16), SidebarItemKind.RadialShelf));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Board, Loc.T("catalog.board.hdf"),
                new Item(Loc.T("catalog.board.hdf"), new Vector3Int(600, 400, 3), SidebarItemKind.Panel));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Facade, Loc.T("catalog.facade.tile"),
                new Item(Loc.T("catalog.facade.slab"), new Vector3Int(600, 716, 18), SidebarItemKind.Facade));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Facade,
                AssembledFacadeItem(Loc.T("catalog.facade.assembled")));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Drawer, Loc.T("catalog.drawer.tile"),
                DrawerItem(Loc.T("catalog.drawer.gtv"), DefaultDrawerType, DefaultDrawerLengthMM, DefaultDrawerSystem));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Drawer,
                MoventoDrawerItem(Loc.T("catalog.drawer.movento")));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("catalog.furniture.rectTable"),
                new Item(Loc.T("catalog.furniture.rectTable"), new Vector3Int(2000, 750, 1000), SidebarItemKind.Table));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.radiusTable"),
                new Item(Loc.T("elementType.radiusTable"), new Vector3Int(2000, 750, 1000), SidebarItemKind.RadiusTable));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.stool"),
                new Item(Loc.T("elementType.stool"), new Vector3Int(StoolElement.DefaultWidthMM,
                    StoolElement.DefaultHeightMM, StoolElement.DefaultDepthMM), SidebarItemKind.Stool));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.chair"),
                new Item(Loc.T("elementType.chair"), new Vector3Int(ChairElement.DefaultWidthMM,
                    ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM), SidebarItemKind.Chair));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.sofa"),
                new Item(Loc.T("elementType.sofa"), new Vector3Int(SofaElement.DefaultWidthMM,
                    SofaElement.DefaultHeightMM, SofaElement.DefaultDepthMM), SidebarItemKind.Sofa));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.pouffe"),
                new Item(Loc.T("elementType.pouffe"), new Vector3Int(PouffeElement.DefaultWidthMM,
                    PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM), SidebarItemKind.Pouffe));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.bed"),
                new Item(Loc.T("elementType.bed"), new Vector3Int(BedElement.DefaultWidthMM,
                    BedElement.DefaultHeightMM, BedElement.DefaultDepthMM), SidebarItemKind.Bed));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("catalog.furniture.leg"),
                PillarItem(Loc.T("catalog.furniture.leg"), PillarElement.MidHeightMM_Default));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.screwLeg"),
                ScrewLegItem(Loc.T("elementType.screwLeg")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, Loc.T("elementType.sink"), SinkItem(Loc.T("elementType.sink")));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, Loc.T("elementType.cooktop"),
                CooktopItem(Loc.T("catalog.appliance.cooktop")));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Appliance,
                CooktopModelItem(Loc.T("catalog.appliance.cooktopModelPrefix") + CooktopElement.MODEL_BOSCH_PUE611BB5E,
                    CooktopElement.MODEL_BOSCH_PUE611BB5E));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, Loc.T("catalog.appliance.oven"),
                OvenItem(Loc.T("catalog.appliance.ovenPrefix") + OvenElement.MODEL));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, Loc.T("catalog.appliance.dishwasher"),
                DishwasherItem(Loc.T("catalog.appliance.dishwasherPrefix") + DishwasherElement.MODEL));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, Loc.T("catalog.appliance.washer"),
                LaundryMachineItem(LaundryMachineKind.Washer));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Appliance, Loc.T("catalog.appliance.dryer"),
                LaundryMachineItem(LaundryMachineKind.Dryer));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("catalog.sanitary.toilet"),
                ToiletItem(Loc.T("catalog.sanitary.toilet")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("catalog.sanitary.installation"),
                WallHungToiletItem(Loc.T("catalog.sanitary.installation")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("elementType.bathtub"),
                BathtubItem(Loc.T("elementType.bathtub")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("catalog.sanitary.mixer"),
                BathMixerItem(Loc.T("catalog.sanitary.mixer")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("elementType.showerColumn"),
                ShowerColumnItem(Loc.T("elementType.showerColumn")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("elementType.pipe"),
                PipeItem(Loc.T("elementType.pipe")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Sanitary, Loc.T("catalog.sanitary.fitting"),
                FittingItem(PipeNodeKind.Elbow));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Coupling));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Tee));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Cap));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Supply));
            yield return SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary,
                FittingItem(PipeNodeKind.Return));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("catalog.room.box"),
                new Item("Короб", new Vector3Int(600, 600, 600)));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("catalog.room.wall"),
                new Item(Loc.T("catalog.room.wall"), new Vector3Int(2000, 2500, 100), SidebarItemKind.Wall));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("elementType.window"),
                new Item(Loc.T("elementType.window"), new Vector3Int(900, 1200, 100), SidebarItemKind.Window));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("elementType.door"),
                new Item(Loc.T("elementType.door"), new Vector3Int(900, 2000, 100), SidebarItemKind.Door));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("catalog.room.floor"),
                new Item(Loc.T("catalog.room.floor"), new Vector3Int(
                    FloorElement.DEFAULT_SIZE_MM,
                    FloorElement.DEFAULT_THICKNESS_MM,
                    FloorElement.DEFAULT_SIZE_MM), SidebarItemKind.Floor));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("elementType.lightSource"),
                LightSourceItem(Loc.T("elementType.lightSource")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("elementType.socket"),
                SocketItem(Loc.T("elementType.socket")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Room, Loc.T("elementType.lightSwitch"),
                LightSwitchItem(Loc.T("elementType.lightSwitch")));

            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.foundation"),
                FoundationItem(Loc.T("elementType.foundation")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.floorSlab"),
                FloorSlabItem(Loc.T("elementType.floorSlab")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.fence"),
                FenceItem(Loc.T("elementType.fence")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.duct"),
                DuctItem(Loc.T("elementType.duct")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("catalog.construction.grille"),
                GrilleItem(Loc.T("catalog.construction.grille")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.roof"),
                RoofItem(Loc.T("elementType.roof")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.insulation"),
                InsulationItem(Loc.T("elementType.insulation")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.ventGap"),
                VentGapItem(Loc.T("elementType.ventGap")));
            yield return SidebarCatalogRow.TypeRow(SidebarGroupKey.Construction, Loc.T("elementType.cladding"),
                CladdingItem(Loc.T("elementType.cladding")));
        }

        private static IEnumerable<(SidebarGroupKey Group, Item Item)> RowsWithTileTitleInherited()
        {
            string currentTileTitle = "";
            foreach (var row in TypesAndPresets())
            {
                if (row.IsTypeRow) currentTileTitle = row.TileTitle!;
                var item = row.Item;
                item.tileTitle = currentTileTitle;
                yield return (row.Group, item);
            }
        }

        private static Item DrawerItem(string name, string drawerType, int length,
            string system = DefaultDrawerSystem)
        {
            int height = DrawerConstants.GetMinOpeningHeight(SidebarPresetResolution.DrawerTypeOf(drawerType));
            var item = new Item(name, new Vector3Int(DefaultDrawerWidthMM, height, length),
                SidebarItemKind.Drawer);
            item.preset.drawerType = drawerType;
            item.preset.drawerLength = length;
            item.preset.drawerColor = DefaultDrawerColor;
            item.preset.drawerWidth = DefaultDrawerWidthMM;
            item.preset.drawerSystem = system;
            return item;
        }

        private static Item MoventoDrawerItem(string name)
        {
            var item = DrawerItem(name, "B", 500, MoventoDrawerSystem);
            item.preset.drawerColor = "White";
            return item;
        }

        private static Item AssembledFacadeItem(string name)
        {
            var item = new Item(name, new Vector3Int(600, 716, 18), SidebarItemKind.Facade);
            item.preset.facadeAssembled = true;
            return item;
        }

        private static Item FittingItem(PipeNodeKind kind)
        {
            string sizeId = PipeSpec.DEFAULT_SIZE;
            var item = new Item(PipeFittingNames.Title(kind), new Vector3Int(
                PipeFittingSpec.RoundedMm(PipeFittingSpec.WidthMm(kind, sizeId)),
                PipeFittingSpec.RoundedMm(PipeFittingSpec.HeightMm(kind, sizeId)),
                PipeFittingSpec.RoundedMm(PipeFittingSpec.DepthMm(kind, sizeId))),
                SidebarItemKind.PipeFitting);
            item.preset.fittingKind = kind;
            return item;
        }

        private static Item PipeItem(string name)
        {
            int section = PipeElementSpec.SectionMM(PipeSpec.DEFAULT_SIZE);
            return new Item(name,
                new Vector3Int(section, PipeElementSpec.DEFAULT_LENGTH_MM, section),
                SidebarItemKind.Pipe);
        }

        private static Item ToiletItem(string name)
            => new Item(name, ToiletElement.ModelDimensionsMM, SidebarItemKind.Toilet);

        private static Item WallHungToiletItem(string name)
            => new Item(name, WallHungToiletElement.ModelDimensionsMM,
                SidebarItemKind.WallHungToilet);

        private static Item BathtubItem(string name)
            => new Item(name, new Vector3Int(BathtubElement.DefaultWidthMM,
                BathtubElement.DefaultHeightMM, BathtubElement.DefaultDepthMM),
                SidebarItemKind.Bathtub);

        private static Item BathMixerItem(string name)
            => new Item(name, BathMixerLayout.DimensionsMM(BathMixerSpec.Default),
                SidebarItemKind.BathMixer);

        private static Item ShowerColumnItem(string name)
            => new Item(name, ShowerColumnLayout.DimensionsMM(ShowerColumnSpec.Default),
                SidebarItemKind.ShowerColumn);

        private static Item DishwasherItem(string name)
        {
            var item = new Item(name, DishwasherElement.ModelDimensionsMM,
                SidebarItemKind.Dishwasher);
            item.preset.applianceModel = DishwasherElement.MODEL;
            return item;
        }

        private static Item LaundryMachineItem(LaundryMachineKind kind)
        {
            var item = new Item(LaundryMachineBody.NameOf(kind),
                LaundryMachineBody.DefaultDimensionsMM,
                kind == LaundryMachineKind.Dryer
                    ? SidebarItemKind.Dryer
                    : SidebarItemKind.WashingMachine);
            item.preset.laundryKind = LaundryMachineBody.TypeId(kind);
            return item;
        }

        private static Item OvenItem(string name)
        {
            var item = new Item(name, OvenElement.ModelDimensionsMM, SidebarItemKind.Oven);
            item.preset.applianceModel = OvenElement.MODEL;
            return item;
        }

        private static Item CooktopModelItem(string name, string model)
        {
            var item = new Item(name, CooktopElement.ModelDimensionsMM(model),
                SidebarItemKind.Cooktop);
            item.preset.applianceModel = model;
            return item;
        }

        private static Item CooktopItem(string name)
        {
            return new Item(name, new Vector3Int(
                CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM,
                CooktopElement.DEFAULT_DEPTH_MM), SidebarItemKind.Cooktop);
        }

        private static Item SinkItem(string name)
        {
            return new Item(name, new Vector3Int(
                SinkElement.OUTER_WIDTH_MM, SinkElement.TotalHeightMM,
                SinkElement.OUTER_DEPTH_MM), SidebarItemKind.Sink);
        }

        private static Item FoundationItem(string name) =>
            new Item(name, new Vector3Int(FoundationElement.DEFAULT_WIDTH_MM,
                FoundationElement.DEFAULT_DEPTH_MM, FoundationElement.DEFAULT_WIDTH_MM),
                SidebarItemKind.Foundation);

        private static Item FloorSlabItem(string name) =>
            new Item(name, new Vector3Int(FloorSlabElement.DEFAULT_LENGTH_MM,
                KitchenSettings.Instance.ConstructionSlabThicknessMm, FloorSlabElement.DEFAULT_WIDTH_MM),
                SidebarItemKind.FloorSlab);

        private static Item FenceItem(string name) =>
            new Item(name, new Vector3Int(FenceElement.DEFAULT_LENGTH_MM,
                FenceElement.DEFAULT_HEIGHT_MM,
                KitchenDesigner.Core.Construction.FenceDefaults.SheetThicknessMm),
                SidebarItemKind.Fence);

        private static Item DuctItem(string name) =>
            new Item(name, new Vector3Int(
                KitchenDesigner.Core.Ventilation.DuctDefaults.DefaultRoundDiameterMm,
                KitchenDesigner.Core.Ventilation.DuctDefaults.DefaultLengthMm,
                KitchenDesigner.Core.Ventilation.DuctDefaults.DefaultRoundDiameterMm),
                SidebarItemKind.Duct);

        private static Item GrilleItem(string name) =>
            new Item(name, new Vector3Int(
                KitchenDesigner.Core.Ventilation.GrilleDefaults.DefaultWidthMm,
                KitchenDesigner.Core.Ventilation.GrilleDefaults.DefaultHeightMm,
                KitchenDesigner.Core.Ventilation.GrilleDefaults.DepthMm),
                SidebarItemKind.Grille);

        private static Item RoofItem(string name)
        {
            int overhang = KitchenDesigner.Core.Construction.RoofDefaults.OverhangMm;
            int span = 2 * overhang;
            int slope = 2 * overhang;
            float runMm = slope * 0.5f;
            int rise = Mathf.Max(1, (int)(runMm
                * Mathf.Tan(KitchenDesigner.Core.Construction.RoofDefaults.PitchDeg * Mathf.Deg2Rad)));
            return new Item(name, new Vector3Int(Mathf.Max(1, span), rise, Mathf.Max(1, slope)),
                SidebarItemKind.Roof);
        }

        private static Item InsulationItem(string name) =>
            new Item(name, new Vector3Int(1000, 1000,
                KitchenDesigner.Core.Construction.WallLayerDefaults.InsulationThicknessMm),
                SidebarItemKind.Insulation);

        private static Item VentGapItem(string name) =>
            new Item(name, new Vector3Int(1000, 1000,
                KitchenDesigner.Core.Construction.WallLayerDefaults.VentGapThicknessMm),
                SidebarItemKind.VentGap);

        private static Item CladdingItem(string name) =>
            new Item(name, new Vector3Int(1000, 1000,
                KitchenDesigner.Core.Construction.WallLayerDefaults.CladdingThicknessMm),
                SidebarItemKind.Cladding);

        private static Item PillarItem(string name, int midHeightMM)
        {
            int totalH = PillarElement.TopHeightMM + midHeightMM + PillarElement.BottomHeightMM;
            var item = new Item(name, new Vector3Int(PillarElement.DiameterMM_Default, totalH,
                PillarElement.DiameterMM_Default), SidebarItemKind.Pillar);
            item.preset.pillarMidHeightMM = midHeightMM;
            return item;
        }

        private static Item ScrewLegItem(string name)
        {
            int totalH = ScrewLegSpec.BodyHeightMM(ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM,
                ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM);
            return new Item(name, new Vector3Int(
                ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM, totalH,
                ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM), SidebarItemKind.ScrewLeg);
        }

        private static Item SocketItem(string name)
            => new Item(name, WallDeviceSpec.Default.DimensionsMM, SidebarItemKind.Socket);

        private static Item LightSwitchItem(string name)
            => new Item(name, WallDeviceSpec.Default.DimensionsMM, SidebarItemKind.LightSwitch);

        private static Item LightSourceItem(string name)
        {
            return new Item(name, new Vector3Int(
                LampSpec.DEFAULT_SIZE_MM,
                LampSpec.DEFAULT_SIZE_MM,
                LampSpec.DEFAULT_SIZE_MM), SidebarItemKind.LightSource);
        }
    }
}
