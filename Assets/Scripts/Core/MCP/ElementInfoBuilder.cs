using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    internal static class ElementInfoBuilder
    {
        public static ElementInfo Build(KitchenElement el, List<KitchenElement>? allElements,
            bool includeFacadeValidation = false, ValidationResult? validation = null)
        {
            var info = BuildSharedFields(el, allElements, validation);
            FillFacadeValidation(info, el, allElements, includeFacadeValidation);
            foreach (var detail in Details) detail(info, el);
            return info;
        }

        private static ElementInfo BuildSharedFields(KitchenElement el,
            List<KitchenElement>? allElements, ValidationResult? validation)
        {
            var t = el.transform;
            var group = GroupManager.GroupOf(el);
            var wall = el.GetComponent<Wall>();
            Vector3 pos = wall != null ? wall.FullPosition : t.position;

            bool hasViolations = false;
            if (allElements != null && allElements.Count > 0)
            {
                var vr = validation ?? ConstraintValidator.Validate(allElements);
                hasViolations = vr.violations.Contains(el);
            }

            var aabb = McpAabb.Of(el.GetVertices());
            var effDim = McpAabb.EffectiveDimMM(el);

            return new ElementInfo
            {
                name = el.PartName, type = el.GetType().Name,
                dimXMm = el.DimensionsMM.x, dimYMm = el.DimensionsMM.y, dimZMm = el.DimensionsMM.z,
                posXMm = McpAnchor.ToMm(pos.x), posYMm = McpAnchor.ToMm(pos.y), posZMm = McpAnchor.ToMm(pos.z),
                rotXDeg = t.eulerAngles.x, rotYDeg = t.eulerAngles.y, rotZDeg = t.eulerAngles.z,
                active = el.gameObject.activeInHierarchy,
                locked = !el.Movable,
                transparent = el.Transparent,
                attachedToName = string.IsNullOrEmpty(el.AttachedToName) ? null : el.AttachedToName,
                attachDetached = string.IsNullOrEmpty(el.AttachedToName) ? (bool?)null : AttachLinks.IsDetached(el),
                moduleId = group != null ? group.id : 0,
                moduleName = group != null ? group.name : null,
                materialId = el.MaterialId,
                hasViolations = hasViolations,
                aabbMinXMm = McpAnchor.ToMm(aabb.minX), aabbMinYMm = McpAnchor.ToMm(aabb.minY), aabbMinZMm = McpAnchor.ToMm(aabb.minZ),
                aabbMaxXMm = McpAnchor.ToMm(aabb.maxX), aabbMaxYMm = McpAnchor.ToMm(aabb.maxY), aabbMaxZMm = McpAnchor.ToMm(aabb.maxZ),
                worldDimXMm = Mathf.RoundToInt((aabb.maxX - aabb.minX) / AppConstants.MM_TO_UNITS),
                worldDimYMm = Mathf.RoundToInt((aabb.maxY - aabb.minY) / AppConstants.MM_TO_UNITS),
                worldDimZMm = Mathf.RoundToInt((aabb.maxZ - aabb.minZ) / AppConstants.MM_TO_UNITS),
                effectiveDimXMm = effDim.x, effectiveDimYMm = effDim.y, effectiveDimZMm = effDim.z,
                faceGaps = allElements != null ? McpAabb.AxisGaps(el, allElements) : null,
                cornerRadiusMm = el is RadialShelfElement radial ? radial.CornerRadius : 0,
                grooves = el.Grooves.Count > 0 ? McpSpecCodec.FormatGrooves(el) : null,
                textureOverlays = el.TextureOverlays.Count > 0 ? McpSpecCodec.FormatTextureOverlays(el) : null,
                edgeBanding = el.SupportsEdges ? el.EdgeBandingEnabled : (bool?)null,
                edgeThicknessMM = el.SupportsEdges ? el.EdgeThicknessMM : (float?)null,
                edgeSides = el.SupportsEdges && McpSpecCodec.FormatEdgeSides(el).Length > 0
                    ? McpSpecCodec.FormatEdgeSides(el) : null,
                edges = el.EdgeBandingEnabled && allElements != null
                    ? McpSpecCodec.FormatBandedEdgesRecomputedFromScene(el, allElements) : null,
                facadeMode = el is FacadeElement facade ? FacadeDoor.WireName(facade.Mode) : null,
            };
        }

        private static void FillFacadeValidation(ElementInfo info, KitchenElement el,
            List<KitchenElement>? allElements, bool includeFacadeValidation)
        {
            if (!includeFacadeValidation || allElements == null || !(el is FacadeElement facade)) return;

            var issues = McpFacadeIssues.Of(facade, allElements);
            info.faceNormalX = issues.normal.x;
            info.faceNormalY = issues.normal.y;
            info.faceNormalZ = issues.normal.z;
            info.faceInward = issues.faceInward;
            info.faceObstructions = issues.obstructions;
            info.openingViolations = issues.openingViolations;
        }

        private delegate void Detail(ElementInfo info, KitchenElement el);

        private static readonly Detail[] Details =
        {
            For<DrawerElement>((info, drawer) => info.drawer = new DrawerInfo
            {
                system = McpWireEnums.Name(drawer.System),
                drawerType = drawer.Type.ToString(),
                drawerLengthMM = drawer.NominalLength,
                drawerColor = McpWireEnums.Name(drawer.Color),
                internalWidthMM = drawer.InternalWidth,
                isDouble = drawer.IsDouble,
                isUpper = drawer.IsUpperDrawer,
                pairedDrawerName = drawer.PairedDrawerName,
                attachedFacadeName = drawer.AttachedFacadeName,
                doubleState = McpWireEnums.Name(drawer.DoubleState),
                isOpen = drawer.IsOpen
            }),

            For<TableElement>((info, table) => info.table = new TableInfo
            {
                legInsetMM = table.LegInsetMM,
                tabletopMaterialId = table.PrimaryMaterialId,
                legsMaterialId = table.SecondaryMaterialId
            }),

            For<RadiusTableElement>((info, table) => info.radiusTable = new RadiusTableInfo
            {
                legInsetMM = table.LegInsetMM,
                shape = "capsule",
                tabletopMaterialId = table.PrimaryMaterialId,
                legsMaterialId = table.SecondaryMaterialId
            }),

            For<StoolElement>((info, stool) =>
            {
                info.cornerRadiusMm = stool.CornerRadiusMM;
                info.stool = new StoolInfo
                {
                    cornerRadiusMM = stool.CornerRadiusMM,
                    shape = stool.ShapeName,
                    tabletopMaterialId = stool.PrimaryMaterialId,
                    legsMaterialId = stool.SecondaryMaterialId
                };
            }),

            For<ChairElement>((info, chair) =>
            {
                info.cornerRadiusMm = chair.CornerRadiusMM;
                info.chair = new ChairInfo
                {
                    cornerRadiusMM = chair.CornerRadiusMM,
                    seatHeightMM = chair.SeatHeightMM,
                    backrestThicknessMM = ChairElement.BackrestThicknessMM,
                    tabletopMaterialId = chair.PrimaryMaterialId,
                    legsMaterialId = chair.SecondaryMaterialId
                };
            }),

            For<SofaElement>((info, sofa) =>
            {
                info.cornerRadiusMm = sofa.CornerRadiusMM;
                info.sofa = new SofaInfo
                {
                    cornerRadiusMM = sofa.CornerRadiusMM,
                    seatHeightMM = sofa.SeatHeightMM,
                    backDepthMM = SofaLayout.BackDepthMM,
                    cushionCount = SofaLayout.CushionCount,
                    tabletopMaterialId = sofa.PrimaryMaterialId,
                    legsMaterialId = sofa.SecondaryMaterialId
                };
            }),

            For<PouffeElement>((info, pouffe) => info.pouffe = new PouffeInfo
            {
                cornerRadiusMM = pouffe.CornerRadiusMM,
                maxCornerRadiusMM = PouffeElement.MaxCornerRadiusMM(pouffe.DimensionsMM),
                seatThicknessMM = pouffe.SeatThicknessMM,
                maxSeatThicknessMM = PouffeElement.MaxSeatThicknessMM(pouffe.DimensionsMM.y),
                bodyHeightMM = PouffeLayout.BodyHeightMM(pouffe.DimensionsMM.y,
                    pouffe.SeatThicknessMM),
                tabletopMaterialId = pouffe.PrimaryMaterialId,
                legsMaterialId = pouffe.SecondaryMaterialId
            }),

            For<ToiletElement>((info, toilet) => info.toilet = new ToiletInfo
            {
                seatHeightMM = toilet.SeatHeightMM,
                minSeatHeightMM = ToiletElement.MinSeatHeightMM,
                maxSeatHeightMM = ToiletElement.MaxSeatHeightMM,
                cisternHeightMM = toilet.CisternHeightMM,
                tabletopMaterialId = toilet.PrimaryMaterialId,
                legsMaterialId = toilet.SecondaryMaterialId
            }),

            For<WallHungToiletElement>((info, toilet) => info.wallHungToilet =
                new WallHungToiletInfo
                {
                    seatHeightMM = toilet.SeatHeightMM,
                    minSeatHeightMM = WallHungToiletElement.MinSeatHeightMM,
                    maxSeatHeightMM = WallHungToiletElement.MaxSeatHeightMM,
                    bowlBottomMM = toilet.BowlBottomMM,
                    flushPlateHeightMM = toilet.FlushPlateHeightMM,
                    minFlushPlateHeightMM =
                        WallHungToiletElement.MinFlushPlateHeightMM(toilet.SeatHeightMM),
                    maxFlushPlateHeightMM = WallHungToiletElement.MaxFlushPlateHeightMM,
                    tabletopMaterialId = toilet.PrimaryMaterialId,
                    legsMaterialId = toilet.SecondaryMaterialId
                }),

            For<BathtubElement>((info, tub) => info.bathtub = new BathtubInfo
            {
                rimWidthMM = tub.RimWidthMM,
                maxRimWidthMM = BathtubElement.MaxRimWidthMM(tub.DimensionsMM),
                bowlDepthMM = tub.BowlDepthMM,
                maxBowlDepthMM = BathtubElement.MaxBowlDepthMM(tub.DimensionsMM),
                bowlRadiusMM = tub.BowlRadiusMM,
                maxBowlRadiusMM =
                    BathtubElement.MaxBowlRadiusMM(tub.DimensionsMM, tub.RimWidthMM),
                bowlFilletMM = tub.BowlFilletMM,
                maxBowlFilletMM = BathtubElement.MaxBowlFilletMM(
                    tub.DimensionsMM, tub.RimWidthMM, tub.BowlDepthMM),
                shellCornerRadiusMM = BathtubLayout.ShellCornerRadiusMM(
                    tub.DimensionsMM, tub.RimWidthMM, tub.BowlRadiusMM),
                bowlWidthMM = BathtubLayout.InnerWidthMM(tub.DimensionsMM, tub.RimWidthMM),
                bowlDepthPlanMM = BathtubLayout.InnerDepthMM(tub.DimensionsMM, tub.RimWidthMM)
            }),

            For<BathMixerElement>((info, mixer) => info.bathMixer = new BathMixerInfo
            {
                centresMM = mixer.CentresMM,
                maxCentresMM = BathMixerSpec.MaxCentresForMM(mixer.BodyLengthMM,
                    mixer.BodyDiameterMM),
                bodyLengthMM = mixer.BodyLengthMM,
                minBodyLengthMM = BathMixerSpec.MinBodyLengthForMM(mixer.BodyDiameterMM),
                bodyDiameterMM = mixer.BodyDiameterMM,
                escutcheonReachMM = mixer.EscutcheonReachMM,
                spoutLengthMM = mixer.SpoutLengthMM,
                outletDiameterMM = mixer.OutletDiameterMM
            }),

            For<ShowerColumnElement>((info, column) => info.showerColumn = new ShowerColumnInfo
            {
                columnHeightMM = column.ColumnHeightMM,
                minColumnHeightMM =
                    ShowerColumnSpec.MinColumnHeightForMM(column.RiserDiameterMM),
                riserDiameterMM = column.RiserDiameterMM,
                headDiameterMM = column.HeadDiameterMM,
                headThicknessMM = column.HeadThicknessMM,
                armReachMM = column.ArmReachMM,
                minArmReachMM = ShowerColumnSpec.MinArmReachForMM(column.RiserDiameterMM,
                    column.WallOffsetMM),
                wallOffsetMM = column.WallOffsetMM,
                handShowerDiameterMM = column.HandShowerDiameterMM,
                hoseLengthMM = column.HoseLengthMM
            }),

            For<IWallDevice>((info, device) => info.wallDevice = new WallDeviceInfo
            {
                plateWidthMM = device.PlateWidthMM,
                plateHeightMM = device.PlateHeightMM,
                protrusionMM = device.ProtrusionMM,
                postCount = device.PostCount
            }),

            For<ILightSwitch>((info, source) => info.lightSwitch = new LightSwitchInfo
            {
                isOn = source.IsOn,
                lights = LinkedLightNames(source),
                maxLights = SwitchLightLinks.MaxLightsPerSwitch
            }),

            For<BedElement>((info, bed) => info.bed = new BedInfo
            {
                size = bed.SizeName,
                isDouble = bed.IsDouble,
                hasHeadboard = bed.HasHeadboard,
                pillowCount = bed.PillowCount,
                legCount = bed.LegCount,
                legHeightMM = BedLayout.LegHeightMM,
                mattressTopMM = BedLayout.DeckTopMM,
                tabletopMaterialId = bed.PrimaryMaterialId,
                legsMaterialId = bed.SecondaryMaterialId
            }),

            For<PillarElement>((info, pillar) => info.pillar = new PillarInfo
            {
                midHeightMM = pillar.MidHeightMM,
                diameterMM = pillar.DiameterMM
            }),

            For<PipeElement>((info, pipe) => info.pipe = new PipeInfo
            {
                sizeId = pipe.SizeId,
                designation = pipe.Designation,
                nominalBoreMM = pipe.NominalBoreMM,
                lengthMM = pipe.LengthMM,
                outerDiameterMM = pipe.OuterDiameterMm,
                innerDiameterMM = pipe.InnerDiameterMm,
                wallThicknessMM = pipe.WallThicknessMm
            }),

            For<PipeFittingElement>((info, fitting) => info.pipeFitting = new PipeFittingInfo
            {
                kind = Plumbing.PipeFittingNames.TypeId(fitting.NodeKind),
                portCount = fitting.PortCount,
                bores = BoresOf(fitting)
            }),

            For<ScrewLegElement>((info, leg) => info.screwLeg = new ScrewLegInfo
            {
                thread = leg.Thread,
                threadLengthMM = leg.ThreadLengthMM,
                insertionMM = leg.InsertionIntoHostMM ?? 0,
                baseDiameterMM = leg.BaseDiameterMM,
                baseHeightMM = leg.BaseHeightMM,
                heightAboveFloorMM = leg.HeightAboveFloorMM,
                leftInHostMM = leg.LeftInHostMM,
                rightInHostMM = leg.RightInHostMM,
                topInHostMM = leg.TopInHostMM,
                bottomInHostMM = leg.BottomInHostMM,
                hostName = leg.HostPartName ?? ""
            }),

            For<CooktopElement>((info, cooktop) => info.cooktop = new CooktopInfo
            {
                model = cooktop.Model,
                fixedSize = cooktop.HasFixedSize,
                cutoutWidthMM = cooktop.CutoutWidthMM,
                cutoutDepthMM = cooktop.CutoutDepthMM,
                plateHeightMM = CooktopElement.RIM_HEIGHT_MM,
                bodyHeightMM = cooktop.BodyHeightMM,
                attachedPartName = cooktop.AttachedPartName,
                offsetXMM = cooktop.OffsetXMM,
                offsetYMM = cooktop.OffsetYMM,
                yawDeg = cooktop.YawDeg
            }),

            For<OvenElement>((info, oven) => info.oven = new OvenInfo
            {
                model = OvenElement.MODEL,
                fixedSize = oven.HasFixedSize,
                facadeThicknessMM = OvenElement.FACADE_THICKNESS_MM,
                bodyWidthMM = OvenElement.BODY_WIDTH_MM,
                bodyDepthMM = OvenElement.BODY_DEPTH_MM,
                bodyHeightMM = OvenElement.BODY_HEIGHT_MM,
                controlPanelHeightMM = OvenElement.CONTROL_PANEL_HEIGHT_MM,
                glassHeightMM = OvenElement.GLASS_HEIGHT_MM,
                handleProtrusionMM = OvenElement.HANDLE_PROTRUSION_MM,
                isOpen = oven.IsOpen
            }),

            For<DishwasherElement>((info, dishwasher) => info.dishwasher = new DishwasherInfo
            {
                model = DishwasherElement.MODEL,
                fixedSize = dishwasher.HasFixedSize,
                attachedFacadeName = dishwasher.AttachedFacadeName ?? "",
                nicheWidthMM = DishwasherElement.NICHE_WIDTH_MM,
                nicheMinDepthMM = DishwasherElement.NICHE_MIN_DEPTH_MM,
                heightMinMM = DishwasherElement.HEIGHT_MIN_MM,
                heightMaxMM = DishwasherElement.HEIGHT_MAX_MM,
                facadeWidthMM = DishwasherElement.FACADE_WIDTH_MM,
                facadeMinHeightMM = DishwasherElement.FACADE_MIN_HEIGHT_MM,
                facadeMaxHeightMM = DishwasherElement.FACADE_MAX_HEIGHT_MM,
                facadeNominalHeightMM = DishwasherElement.FACADE_NOMINAL_HEIGHT_MM,
                plinthMM = PlinthOfAttachedFacadeOrZero(dishwasher),
                plinthMinMM = DishwasherElement.PLINTH_MIN_MM,
                plinthMaxMM = DishwasherElement.PLINTH_MAX_MM,
                plinthSetbackMM = DishwasherElement.PLINTH_SETBACK_MM,
                plinthNicheMM = DishwasherElement.PLINTH_NICHE_MM,
                baseHeightMM = DishwasherElement.BASE_HEIGHT_MM,
                baseSetbackMM = DishwasherElement.BASE_SETBACK_MM,
                facadeMountGapMM = DishwasherElement.FACADE_MOUNT_GAP_MM,
                isOpen = dishwasher.IsOpen
            }),

            For<WindowElement>((info, window) => info.window = new WindowInfo
            {
                tint = McpWireEnums.Name(window.Tint),
                sillProtrusionMM = window.SillProtrusionMM,
                mode = FacadeDoor.WireName(window.Mode),
                isOpen = window.IsOpen,
                attachedWallName = window.AttachedWallName
            }),

            For<DoorElement>((info, door) => info.door = new DoorInfo
            {
                sashType = McpWireEnums.Name(door.SashType),
                mode = FacadeDoor.WireName(door.Mode),
                isOpen = door.IsOpen,
                attachedWallName = door.AttachedWallName
            }),
        };

        private static int PlinthOfAttachedFacadeOrZero(DishwasherElement dishwasher) =>
            dishwasher.FindAttachedFacade() is FacadeElement facade
                ? DishwasherElement.PlinthForFacade(facade.DimensionsMM.y)
                : 0;

        private static string[] LinkedLightNames(ILightSwitch source)
        {
            var names = source.LightNames;
            var copy = new string[names.Count];
            for (int i = 0; i < names.Count; i++) copy[i] = names[i];
            return copy;
        }

        private static string[] BoresOf(PipeFittingElement fitting)
        {
            var sizes = Analysis.ScenePipeSurvey.SizesOf(fitting);
            var bores = new string[fitting.PortCount];
            for (int i = 0; i < bores.Length; i++)
                bores[i] = Analysis.ScenePipeSurvey.DesignationAt(sizes, i);
            return bores;
        }

        private static Detail For<T>(Action<ElementInfo, T> fill) where T : class
            => (info, el) => { if (el is T typed) fill(info, typed); };
    }
}
