using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ElementRestorers
    {
        internal delegate GameObject Spawn(IElementFactory factory, ElementData data);

        internal delegate void RestoreExtras(ElementData data, KitchenElement element);

        private static readonly (Func<ElementData, bool> handles, Spawn spawn, RestoreExtras? extras)[] ByFlag =
        {
            (d => d.isDrawer,
             (factory, d) => factory.CreateDrawer((DrawerType)d.drawerType, d.drawerNominalLength,
                 (DrawerColor)d.drawerColor, d.drawerInternalWidth, d.name, d.Position,
                 (DrawerSystem)d.drawerSystem),
             (d, el) =>
             {
                 if (el is not DrawerElement drawer) return;
                 drawer.IsDouble = d.drawerIsDouble;
                 drawer.IsUpperDrawer = d.drawerIsUpper;
                 drawer.PairedDrawerName = d.drawerPairedName;
                 var previous = drawer.FindAttachedFacade();
                 drawer.AttachedFacadeName = d.drawerAttachedFacadeName;
                 drawer.OnAttachedFacadeChanged(previous, drawer.FindAttachedFacade());
                 drawer.DoubleState = (DoubleDrawerState)d.doubleDrawerState;
                 if (d.doorOpen && !drawer.IsOpen)
                     drawer.SetOpen(true);
             }),

            (d => d.isWindow,
             (factory, d) => factory.CreateWindow(d.Dimensions, d.name, d.Position,
                 (GlassTint)d.windowTint, d.windowSillProtrusionMM),
             (d, el) =>
             {
                 if (el is not WindowElement window) return;
                 window.Mode = (DoorMode)d.windowDoorMode;
                 if (d.windowIsOpen) window.SetOpen(true);
                 window.AttachedWallName = d.windowAttachedWallName;
             }),

            (d => d.isDoor,
             (factory, d) => factory.CreateDoor(d.Dimensions, d.name, d.Position,
                 (DoorSashType)d.doorSashType),
             (d, el) =>
             {
                 if (el is not DoorElement door) return;
                 door.Mode = (DoorMode)d.doorDoorMode;
                 if (d.doorIsOpen) door.SetOpen(true);
                 door.AttachedWallName = d.doorAttachedWallName;
             }),

            (d => d.isPanel,
             (factory, d) => factory.CreatePanel(d.Dimensions, d.name, d.Position,
                 d.gapLeft, d.gapRight, d.gapTop, d.gapBottom),
             null),

            (d => d.isWall,
             (factory, d) => factory.CreateWall(d.Dimensions, d.name, d.Position),
             (d, el) =>
             {
                 var wall = el.GetComponent<Wall>();
                 if (wall == null) return;
                 wall.Kind = d.wallKind ?? "";
                 if (d.wallEndShape != null && d.wallEndShape.Length >= 4)
                     wall.SetEndShape(new WallMeshBuilder.EndShape
                     {
                         startFront = d.wallEndShape[0], startBack = d.wallEndShape[1],
                         endFront = d.wallEndShape[2], endBack = d.wallEndShape[3]
                     });
             }),

            (d => d.isFloor,
             (factory, d) => factory.CreateFloor(d.Dimensions, d.name, d.Position),
             (d, el) =>
             {
                 if (el is FloorElement floor && d.floorPolygonXZ != null && d.floorPolygonXZ.Length >= 6)
                     floor.SetPolygonLocalMm(d.FloorPolygon());
             }),

            (d => d.isLightSource,
             (factory, d) => factory.CreateLightSource(d.name, d.Position),
             RestoreLamp),

            (d => d.isSink,
             (factory, d) => factory.CreateSink(d.name, d.Position),
             (d, el) =>
             {
                 if (el is not SinkElement sink) return;
                 sink.AttachedPartName = d.sinkAttachedPartName;
                 sink.OffsetXMM = d.sinkOffsetXMM;
                 sink.OffsetYMM = d.sinkOffsetYMM;
             }),

            (d => d.isCooktop,
             (factory, d) => factory.CreateCooktop(d.name, d.Position, d.cooktopModel),
             RestoreCooktop),

            (d => d.isOven,
             (factory, d) => factory.CreateOven(d.name, d.Position),
             (d, el) =>
             {
                 if (el is OvenElement oven && d.doorOpen) oven.SetOpen(true);
             }),

            (d => d.isDishwasher,
             (factory, d) => factory.CreateDishwasher(d.name, d.Position),
             (d, el) =>
             {
                 if (el is not DishwasherElement dishwasher) return;
                 var previous = dishwasher.FindAttachedFacade();
                 dishwasher.AttachedFacadeName = d.dishwasherAttachedFacadeName;
                 dishwasher.OnAttachedFacadeChanged(previous, dishwasher.FindAttachedFacade());
                 if (d.doorOpen) dishwasher.SetOpen(true);
             }),

            (d => d.isPillar,
             (factory, d) => factory.CreatePillar(d.midHeightMM, d.name, d.Position, d.Dimensions.x),
             null),

            (d => d.isPipe,
             (factory, d) => factory.CreatePipe(d.pipeSizeId, d.pipeLengthMM, d.name, d.Position),
             null),

            (d => d.isScrewLeg,
             (factory, d) => factory.CreateScrewLeg(d.name, d.Position),
             (d, el) =>
             {
                 if (el is not ScrewLegElement leg) return;
                 leg.Thread = d.screwLegThread;
                 leg.BaseDiameterMM = d.screwLegBaseDiameterMM;
                 leg.BaseHeightMM = d.screwLegBaseHeightMM;
                 leg.ThreadLengthMM = d.screwLegThreadLengthMM;
                 leg.InsertionDepthMM = d.screwLegInsertionMM;
             }),

            (d => d.isTable,
             (factory, d) => factory.CreateTable(d.Dimensions, d.name, d.Position),
             (d, el) =>
             {
                 if (el is not TableElement table) return;
                 RestoreDecorSlots(d, table);
                 table.LegInsetMM = d.legInsetMM;
             }),

            (d => d.isRadiusTable,
             (factory, d) => factory.CreateRadiusTable(d.Dimensions, d.name, d.Position),
             (d, el) =>
             {
                 if (el is not RadiusTableElement table) return;
                 RestoreDecorSlots(d, table);
                 table.LegInsetMM = d.legInsetMM;
             }),

            (d => d.isStool,
             (factory, d) => factory.CreateStool(d.Dimensions, d.cornerRadius, d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isChair,
             (factory, d) => factory.CreateChair(d.Dimensions, d.cornerRadius, d.seatHeightMM,
                 d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isSofa,
             (factory, d) => factory.CreateSofa(d.Dimensions, d.cornerRadius, d.seatHeightMM,
                 d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isPouffe,
             (factory, d) => factory.CreatePouffe(d.Dimensions, d.cornerRadius,
                 d.pouffeSeatThicknessMM, d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isWallHungToilet,
             (factory, d) => factory.CreateWallHungToilet(d.toiletSeatHeightMM,
                 d.toiletFlushPlateHeightMM, d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isToilet,
             (factory, d) => factory.CreateToilet(d.toiletSeatHeightMM, d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isBathtub,
             (factory, d) => factory.CreateBathtub(d.Dimensions, d.bathtubRimWidthMM,
                 d.bathtubBowlDepthMM, d.bathtubBowlRadiusMM, d.bathtubBowlFilletMM,
                 d.name, d.Position),
             null),

            (d => d.isBathMixer,
             (factory, d) => factory.CreateBathMixer(BathMixerSpec.Clamped(d.bathMixerCentresMM,
                     d.bathMixerBodyLengthMM, d.bathMixerBodyDiameterMM,
                     d.bathMixerEscutcheonReachMM, d.bathMixerSpoutLengthMM,
                     d.bathMixerOutletDiameterMM),
                 d.name, d.Position),
             null),

            (d => d.isShowerColumn,
             (factory, d) => factory.CreateShowerColumn(ShowerColumnSpec.Clamped(
                     d.showerColumnHeightMM, d.showerRiserDiameterMM, d.showerHeadDiameterMM,
                     d.showerHeadThicknessMM, d.showerArmReachMM, d.showerWallOffsetMM,
                     d.showerHandDiameterMM, d.showerHoseLengthMM),
                 d.name, d.Position),
             null),

            (d => d.isSocket,
             (factory, d) => factory.CreateSocket(WallDeviceSpec.Clamped(d.wallDevicePlateWidthMM,
                     d.wallDevicePlateHeightMM, d.wallDeviceProtrusionMM, d.wallDevicePostCount),
                 d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isLightSwitch,
             (factory, d) => factory.CreateLightSwitch(
                 WallDeviceSpec.Clamped(d.wallDevicePlateWidthMM, d.wallDevicePlateHeightMM,
                     d.wallDeviceProtrusionMM, d.wallDevicePostCount),
                 d.lightSwitchOn, d.switchLightNames, d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isBed,
             (factory, d) => factory.CreateBed(d.Dimensions, d.bedDouble, d.bedHeadboard,
                 d.name, d.Position),
             RestoreDecorSlots),

            (d => d.isRadialShelf,
             (factory, d) => factory.CreateRadialShelf(d.Dimensions.x, d.Dimensions.z,
                 d.Dimensions.y, d.cornerRadius, d.name, d.Position),
             null),

            (d => d.assembled,
             (factory, d) => factory.CreateAssembledFacade(d.Dimensions, d.name, d.Position,
                 (AssembledFill)d.assembledFill),
             (d, el) =>
             {
                 if (el is AssembledFacadeElement assembled)
                     assembled.GrooveCount = d.grooveCount;
                 RestoreFacadeState(d, el);
             }),

            (d => d.isFacade,
             (factory, d) => factory.CreateFacade(d.Dimensions, d.name, d.Position,
                 d.gapLeft, d.gapRight, d.gapTop, d.gapBottom),
             RestoreFacadeState),

            (_ => true,
             (factory, d) => factory.CreatePart(d.Dimensions, d.name, d.Position),
             null),
        };

        public static GameObject Restore(IElementFactory factory, ElementData data)
        {
            foreach (var (handles, spawn, extras) in ByFlag)
            {
                if (!handles(data)) continue;
                var go = spawn(factory, data);
                go.transform.rotation = data.Rotation;
                var el = go.GetComponent<KitchenElement>();
                if (el != null)
                {
                    ApplyShared(data, el);
                    extras?.Invoke(data, el);
                }
                return go;
            }
            throw new InvalidOperationException(
                "ElementRestorers без замыкающей записи: " + data.name);
        }

        private static void ApplyShared(ElementData data, KitchenElement el)
        {
            el.Movable = data.movable;
            el.AttachedToName = data.attachedToName ?? "";
            el.GroupId = data.groupId;
            el.Transparent = data.transparent;
            MaterialManager.ApplyById(el, data.materialId);

            if (el.SupportsGrooves)
                el.SetGrooves(data.GrooveSpecs());

            if (el.SupportsTextureOverlays)
                el.SetTextureOverlays(data.TextureOverlaySpecs());

            if (el.SupportsGrooves)
            {
                el.EdgeBandingEnabled = data.edgeBanding;
                el.EdgeThicknessMM = data.edgeThicknessMM;
                el.Data.EdgeForcedMask = data.edgeManualMask != 0
                    ? data.edgeManualMask
                    : (data.edgeSkipValidation ? EdgeManual.AllMask : 0);
                el.Data.EdgeSuppressedMask =
                    data.edgeSuppressedMask > 0 ? data.edgeSuppressedMask : 0;
            }

            if (el.SupportsGaps)
            {
                el.GapLeft = data.gapLeft;
                el.GapRight = data.gapRight;
                el.GapTop = data.gapTop;
                el.GapBottom = data.gapBottom;
                el.GapFront = data.gapFront;
                el.GapBack = data.gapBack;
            }
        }

        private static void RestoreDecorSlots(ElementData data, KitchenElement el)
        {
            if (el is not IHasTwoDecorSlots slots) return;
            if (!string.IsNullOrEmpty(data.legsMaterialId))
                MaterialManager.ApplySecondarySlot(slots, MaterialCatalog.Get(data.legsMaterialId));
            if (!string.IsNullOrEmpty(data.tabletopMaterialId))
                MaterialManager.ApplyPrimarySlot(slots, MaterialCatalog.Get(data.tabletopMaterialId));
        }

        private static void RestoreFacadeState(ElementData data, KitchenElement el)
        {
            if (el is not FacadeElement facade) return;
            facade.Mode = (DoorMode)data.doorMode;
            if (data.doorOpen)
                facade.SetOpen(true);
        }

        private static void RestoreCooktop(ElementData data, KitchenElement el)
        {
            if (el is not CooktopElement cooktop) return;
            cooktop.Model = data.cooktopModel;
            cooktop.AttachedPartName = data.cooktopAttachedPartName;
            cooktop.OffsetXMM = data.cooktopOffsetXMM;
            cooktop.OffsetYMM = data.cooktopOffsetYMM;
            cooktop.YawDeg = data.cooktopYawDeg;
            if (data.Dimensions.x > 0 && data.Dimensions.z > 0)
                cooktop.DimensionsMM = data.Dimensions;
            if (data.cooktopCutoutWidthMM > 0)
                cooktop.CutoutWidthMM = data.cooktopCutoutWidthMM;
            if (data.cooktopCutoutDepthMM > 0)
                cooktop.CutoutDepthMM = data.cooktopCutoutDepthMM;
        }

        private static void RestoreLamp(ElementData data, KitchenElement el)
        {
            if (el is not LightSourceElement lamp) return;
            if (data.Dimensions.x > 0 && data.Dimensions.y > 0 && data.Dimensions.z > 0)
                lamp.DimensionsMM = data.Dimensions;
            lamp.TemperatureK = data.lightTemperatureK;
            lamp.PowerW = data.lightPowerW;
            lamp.DiffusionPct = data.lightDiffusionPct;
            lamp.UpLightPct = data.lightUpPct;
            lamp.BeamAngleDeg = data.lightBeamDeg;
            lamp.SoftnessPct = data.lightSoftnessPct;
            lamp.RangeMinMM = data.lightRangeMinMM;
            lamp.RangeMaxMM = data.lightRangeMaxMM;
            lamp.DropMM = data.lightDropMM;
            lamp.UpConePct = data.lightUpConePct;
            lamp.UpRangePct = data.lightUpRangePct;
            lamp.EfficacyLmPerW = data.lightEfficacyLmPerW;
            lamp.LumensPerUnit = data.lightLumensPerUnit;
            lamp.GlowPct = data.lightGlowPct;
            lamp.ShadowStrengthPct = data.lightShadowStrengthPct;
            lamp.Shape = (LampShape)Mathf.Clamp(data.lightShape, 0, 1);
            lamp.Shadow = (LampShadow)Mathf.Clamp(data.lightShadow, 0, 2);
        }
    }
}
