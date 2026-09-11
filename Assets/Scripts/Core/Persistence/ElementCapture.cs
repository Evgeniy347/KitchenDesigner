namespace KitchenDesigner.Core
{
    public static class ElementCapture
    {
        public static ElementData FromElement(KitchenElement element)
        {
            var d = new ElementData { name = element.PartName };
            var dims = element.DimensionsMM;
            d.dimensionsMM = new[] { dims.x, dims.y, dims.z };

			var drawer = element as DrawerElement;
			var wall = element.GetComponent<Wall>();
			var facade = element as FacadeElement;
			var radialShelf = element as RadialShelfElement;
			var radiusTable = element as RadiusTableElement;
			var stool = element as StoolElement;
			var chair = element as ChairElement;
			var sofa = element as SofaElement;
			var pouffe = element as PouffeElement;
			var toilet = element as ToiletElement;
			var wallHungToilet = element as WallHungToiletElement;
			var bathtub = element as BathtubElement;
			var bathMixer = element as BathMixerElement;
			var showerColumn = element as ShowerColumnElement;
			var socket = element as SocketElement;
			var lightSwitch = element as LightSwitchElement;
			var bed = element as BedElement;
			var tableEl2 = element as TableElement;
			var windowEl = element as WindowElement;
			var doorEl = element as DoorElement;
			var pillar = element as PillarElement;
			var panel = element as PanelElement;

            var p = wall != null ? wall.FullPosition
                  : windowEl != null ? windowEl.ClosedPosition
                  : doorEl != null ? doorEl.ClosedPosition
                  : facade != null ? facade.ClosedPosition
                  : drawer != null ? drawer.ClosedPosition
                  : element.AttachRestPosition;
            d.position = new[] { p.x, p.y, p.z };

            var r = windowEl != null ? windowEl.ClosedRotation
                  : doorEl != null ? doorEl.ClosedRotation
                  : facade != null ? facade.ClosedRotation
                  : drawer != null ? drawer.ClosedRotation
                  : element.AttachRestRotation;
            d.rotation = new[] { r.x, r.y, r.z, r.w };

            d.movable = element.Movable;
            d.attachedToName = element.AttachedToName ?? "";
            d.isWall = wall != null;
            d.wallKind = wall != null ? wall.Kind : "";
            d.wallLoadBearing = wall != null && wall.LoadBearing;
            if (wall != null)
            {
                d.wallMasonry = (int)wall.Masonry;
                d.wallJointMm = wall.JointMm;
                d.wallWastePct = wall.WastePct;
                var ws = wall.EndShape;
                d.wallEndShape = new[] { ws.startFront, ws.startBack, ws.endFront, ws.endBack };
            }
            d.isFacade = facade != null;
            d.isRadialShelf = radialShelf != null;
            d.isRadiusTable = radiusTable != null;
            d.isStool = stool != null;
            d.isChair = chair != null;
            d.isSofa = sofa != null;
            d.isPouffe = pouffe != null;
            d.pouffeSeatThicknessMM = pouffe != null ? pouffe.SeatThicknessMM
                : PouffeLayout.DefaultSeatThicknessMM;
            d.isToilet = toilet != null;
            d.isWallHungToilet = wallHungToilet != null;
            d.toiletSeatHeightMM = toilet != null ? toilet.SeatHeightMM
                : wallHungToilet != null ? wallHungToilet.SeatHeightMM
                : ToiletLayout.DefaultSeatHeightMM;
            d.toiletFlushPlateHeightMM = wallHungToilet != null
                ? wallHungToilet.FlushPlateHeightMM
                : WallHungToiletLayout.DefaultPlateBottomMM;
            d.isBathtub = bathtub != null;
            d.bathtubRimWidthMM = bathtub != null ? bathtub.RimWidthMM
                : BathtubLayout.DefaultRimWidthMM;
            d.bathtubBowlDepthMM = bathtub != null ? bathtub.BowlDepthMM
                : BathtubLayout.DefaultBowlDepthMM;
            d.bathtubBowlRadiusMM = bathtub != null ? bathtub.BowlRadiusMM
                : BathtubLayout.DefaultBowlRadiusMM;
            d.bathtubBowlFilletMM = bathtub != null ? bathtub.BowlFilletMM
                : BathtubLayout.DefaultBowlFilletMM;
            CaptureBathMixer(d, bathMixer);
            CaptureShowerColumn(d, showerColumn);
            CaptureWallDevice(d, socket, lightSwitch);
            d.isBed = bed != null;
            d.bedDouble = bed != null ? bed.IsDouble : true;
            d.bedHeadboard = bed != null ? bed.HasHeadboard : true;
            d.seatHeightMM = chair != null ? chair.SeatHeightMM
                : sofa != null ? sofa.SeatHeightMM
                : AppConstants.CHAIR_SEAT_HEIGHT_DEFAULT;
            d.isTable = tableEl2 != null;
            if (tableEl2 != null)
                d.legInsetMM = tableEl2.LegInsetMM;
            else if (radiusTable != null)
                d.legInsetMM = radiusTable.LegInsetMM;
            var slots = element as IHasTwoDecorSlots;
            d.legsMaterialId = slots != null ? slots.SecondaryMaterialId : MaterialCatalog.DefaultId;
            d.tabletopMaterialId = slots != null ? slots.PrimaryMaterialId : MaterialCatalog.DefaultId;
            d.cornerRadius = radialShelf != null ? radialShelf.CornerRadius
                : stool != null ? stool.CornerRadiusMM
                : chair != null ? chair.CornerRadiusMM
                : sofa != null ? sofa.CornerRadiusMM
                : pouffe != null ? pouffe.CornerRadiusMM : 0;

            d.gapLeft = element.SupportsGaps ? element.GapLeft : 0;
            d.gapRight = element.SupportsGaps ? element.GapRight : 0;
            d.gapTop = element.SupportsGaps ? element.GapTop : 0;
            d.gapBottom = element.SupportsGaps ? element.GapBottom : 0;
            d.gapFront = element.SupportsGaps ? element.GapFront : 0;
            d.gapBack = element.SupportsGaps ? element.GapBack : 0;

            if (facade != null)
            {
                d.doorMode = (int)facade.Mode;
                d.doorOpen = facade.IsOpen;
                if (facade is AssembledFacadeElement assembled)
                {
                    d.assembled = true;
                    d.assembledFill = (int)assembled.Fill;
                    d.grooveCount = assembled.GrooveCount;
                }
                else
                {
                    d.assembled = false;
                    d.assembledFill = 0;
                    d.grooveCount = 0;
                }
            }
            else
            {
                d.doorMode = 0;
                d.doorOpen = false;
                d.assembled = false;
                d.assembledFill = 0;
                d.grooveCount = 0;
            }

            if (drawer != null)
            {
                d.isDrawer = true;
                d.drawerSystem = (int)drawer.System;
                d.drawerType = (int)drawer.Type;
                d.drawerNominalLength = drawer.NominalLength;
                d.drawerColor = (int)drawer.Color;
                d.drawerInternalWidth = drawer.InternalWidth;
                d.drawerIsDouble = drawer.IsDouble;
                d.drawerIsUpper = drawer.IsUpperDrawer;
                d.drawerPairedName = drawer.PairedDrawerName ?? "";
                d.drawerAttachedFacadeName = drawer.AttachedFacadeName ?? "";
                d.doubleDrawerState = (int)drawer.DoubleState;
                d.doorOpen = drawer.IsOpen;
            }

			if (windowEl != null)
			{
				d.isWindow = true;
				d.windowTint = (int)windowEl.Tint;
				d.windowSillProtrusionMM = windowEl.SillProtrusionMM;
				d.windowDoorMode = (int)windowEl.Mode;
				d.windowIsOpen = windowEl.IsOpen;
				d.windowAttachedWallName = windowEl.AttachedWallName ?? "";
			}

			if (doorEl != null)
			{
				d.isDoor = true;
				d.doorSashType = (int)doorEl.SashType;
				d.doorDoorMode = (int)doorEl.Mode;
				d.doorIsOpen = doorEl.IsOpen;
				d.doorAttachedWallName = doorEl.AttachedWallName ?? "";
			}

			d.isPillar = pillar != null;
			d.isPanel = panel != null;
			if (element is SinkElement sinkEl)
			{
				d.isSink = true;
				d.sinkAttachedPartName = sinkEl.AttachedPartName ?? "";
				d.sinkOffsetXMM = sinkEl.OffsetXMM;
				d.sinkOffsetYMM = sinkEl.OffsetYMM;
			}
			if (element is CooktopElement cooktopEl)
			{
				d.isCooktop = true;
				d.cooktopModel = cooktopEl.Model ?? "";
				d.cooktopAttachedPartName = cooktopEl.AttachedPartName ?? "";
				d.cooktopOffsetXMM = cooktopEl.OffsetXMM;
				d.cooktopOffsetYMM = cooktopEl.OffsetYMM;
				d.cooktopCutoutWidthMM = cooktopEl.CutoutWidthMM;
				d.cooktopCutoutDepthMM = cooktopEl.CutoutDepthMM;
				d.cooktopYawDeg = cooktopEl.YawDeg;
			}
			d.isOven = element is OvenElement;
			if (element is OvenElement ovenEl)
				d.doorOpen = ovenEl.IsOpen;
			d.isDishwasher = element is DishwasherElement;
			if (element is DishwasherElement dishwasherEl)
			{
				d.dishwasherAttachedFacadeName = dishwasherEl.AttachedFacadeName ?? "";
				d.doorOpen = dishwasherEl.IsOpen;
			}
			d.isLaundryMachine = element is LaundryMachineElement;
			if (element is LaundryMachineElement laundryEl)
			{
				d.laundryMachineKind = (int)laundryEl.Kind;
				d.doorOpen = laundryEl.IsOpen;
			}
			d.isFloor = element is FloorElement;
			if (element is FloorElement floor && floor.PolygonLocalMm.Count >= 3)
			{
				d.floorPolygonXZ = new int[floor.PolygonLocalMm.Count * 2];
				for (int i = 0; i < floor.PolygonLocalMm.Count; i++)
				{
					d.floorPolygonXZ[i * 2] = floor.PolygonLocalMm[i].x;
					d.floorPolygonXZ[i * 2 + 1] = floor.PolygonLocalMm[i].y;
				}
			}
			d.isLightSource = element is LightSourceElement;
			if (element is LightSourceElement lightEl)
			{
				d.lightTemperatureK = lightEl.TemperatureK;
				d.lightPowerW = lightEl.PowerW;
				d.lightDiffusionPct = lightEl.DiffusionPct;
				d.lightUpPct = lightEl.UpLightPct;
				d.lightBeamDeg = lightEl.BeamAngleDeg;
				d.lightSoftnessPct = lightEl.SoftnessPct;
				d.lightRangeMinMM = lightEl.RangeMinMM;
				d.lightRangeMaxMM = lightEl.RangeMaxMM;
				d.lightDropMM = lightEl.DropMM;
				d.lightUpConePct = lightEl.UpConePct;
				d.lightUpRangePct = lightEl.UpRangePct;
				d.lightEfficacyLmPerW = lightEl.EfficacyLmPerW;
				d.lightLumensPerUnit = lightEl.LumensPerUnit;
				d.lightGlowPct = lightEl.GlowPct;
				d.lightShadowStrengthPct = lightEl.ShadowStrengthPct;
				d.lightShape = (int)lightEl.Shape;
				d.lightShadow = (int)lightEl.Shadow;
			}
			d.midHeightMM = pillar != null ? pillar.MidHeightMM : PillarElement.MidHeightMM_Default;
			if (element is ScrewLegElement screwLeg)
			{
				d.isScrewLeg = true;
				d.screwLegThread = screwLeg.Thread;
				d.screwLegThreadLengthMM = screwLeg.ThreadLengthMM;
				d.screwLegInsertionMM = screwLeg.InsertionDepthMM;
				d.screwLegBaseDiameterMM = screwLeg.BaseDiameterMM;
				d.screwLegBaseHeightMM = screwLeg.BaseHeightMM;
			}

			if (element is PipeElement pipe)
			{
				d.isPipe = true;
				d.pipeSizeId = pipe.SizeId;
				d.pipeLengthMM = pipe.LengthMM;
			}

			if (element is PipeFittingElement fitting)
			{
				d.isPipeFitting = true;
				d.pipeFittingType = Plumbing.PipeFittingNames.TypeId(fitting.NodeKind);
			}

			var grooveSpecs = element.Grooves;
			d.grooves = new GrooveEntry[grooveSpecs.Count];
			for (int i = 0; i < grooveSpecs.Count; i++)
				d.grooves[i] = new GrooveEntry(grooveSpecs[i]);

			var overlaySpecs = element.TextureOverlays;
			d.textureOverlays = new TextureOverlayEntry[overlaySpecs.Count];
			for (int i = 0; i < overlaySpecs.Count; i++)
				d.textureOverlays[i] = new TextureOverlayEntry(overlaySpecs[i]);

			d.edgeBanding = element.Data.EdgeBanding;
			d.edgeThicknessMM = element.Data.EdgeThicknessMM;
			d.edgeManualMask = element.Data.EdgeForcedMask;
			d.edgeSuppressedMask = element.Data.EdgeSuppressedMask;
			d.edgeSkipValidation =
				(d.edgeManualMask | d.edgeSuppressedMask) == EdgeManual.AllMask;

			d.groupId = element.GroupId;
            d.materialId = element.MaterialId;
            d.transparent = element.Transparent;
            return d;
        }

        private static void CaptureBathMixer(ElementData d, BathMixerElement? mixer)
        {
            d.isBathMixer = mixer != null;
            var spec = mixer != null ? mixer.Spec : BathMixerSpec.Default;
            d.bathMixerCentresMM = spec.CentresMM;
            d.bathMixerBodyLengthMM = spec.BodyLengthMM;
            d.bathMixerBodyDiameterMM = spec.BodyDiameterMM;
            d.bathMixerEscutcheonReachMM = spec.EscutcheonReachMM;
            d.bathMixerSpoutLengthMM = spec.SpoutLengthMM;
            d.bathMixerOutletDiameterMM = spec.OutletDiameterMM;
        }

        private static void CaptureWallDevice(ElementData d, SocketElement? socket,
            LightSwitchElement? lightSwitch)
        {
            d.isSocket = socket != null;
            d.isLightSwitch = lightSwitch != null;

            var device = socket != null ? (IWallDevice?)socket : lightSwitch;
            var spec = WallDeviceSpec.Of(device);
            d.wallDevicePlateWidthMM = spec.PlateWidthMM;
            d.wallDevicePlateHeightMM = spec.PlateHeightMM;
            d.wallDeviceProtrusionMM = spec.ProtrusionMM;
            d.wallDevicePostCount = spec.PostCount;

            d.lightSwitchOn = lightSwitch == null || lightSwitch.IsOn;
            d.switchLightNames = LinkedLightNames(lightSwitch);
        }

        private static string[] LinkedLightNames(LightSwitchElement? lightSwitch)
        {
            if (lightSwitch == null) return System.Array.Empty<string>();
            var names = lightSwitch.LightNames;
            var copy = new string[names.Count];
            for (int i = 0; i < names.Count; i++) copy[i] = names[i];
            return copy;
        }

        private static void CaptureShowerColumn(ElementData d, ShowerColumnElement? column)
        {
            d.isShowerColumn = column != null;
            var spec = column != null ? column.Spec : ShowerColumnSpec.Default;
            d.showerColumnHeightMM = spec.ColumnHeightMM;
            d.showerRiserDiameterMM = spec.RiserDiameterMM;
            d.showerHeadDiameterMM = spec.HeadDiameterMM;
            d.showerHeadThicknessMM = spec.HeadThicknessMM;
            d.showerArmReachMM = spec.ArmReachMM;
            d.showerWallOffsetMM = spec.WallOffsetMM;
            d.showerHandDiameterMM = spec.HandShowerDiameterMM;
            d.showerHoseLengthMM = spec.HoseLengthMM;
        }
    }
}
