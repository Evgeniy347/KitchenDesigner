using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class ElementDuplicators
    {
        internal delegate GameObject Spawn(IElementFactory factory, KitchenElement source, Vector3 position);

        internal delegate void CopyExtras(KitchenElement source, GameObject copy);

        private static readonly (Func<KitchenElement, bool> handles, Spawn spawn, CopyExtras? extras)[] ByType =
        {
            (el => el is DrawerElement,
             (factory, source, pos) =>
             {
                 var src = (DrawerElement)source;
                 return factory.CreateDrawer(src.Type, src.NominalLength, src.Color,
                     src.InternalWidth, source.PartName, pos, src.System);
             },
             (source, copy) =>
             {
                 var src = (DrawerElement)source;
                 var made = copy.GetComponent<DrawerElement>();
                 if (made != null)
                 {
                     made.IsDouble = src.IsDouble;
                     made.IsUpperDrawer = src.IsUpperDrawer;
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is AssembledFacadeElement,
             (factory, source, pos) => factory.CreateAssembledFacade(
                 source.DimensionsMM, source.PartName, pos, ((AssembledFacadeElement)source).Fill),
             (source, copy) =>
             {
                 var src = (AssembledFacadeElement)source;
                 var made = copy.GetComponent<AssembledFacadeElement>();
                 if (made != null)
                 {
                     made.Mode = src.Mode;
                     made.GrooveCount = src.GrooveCount;
                     foreach (var side in GapSides.All) made.SetGap(side, src.GapOf(side));
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is RadialShelfElement,
             (factory, source, pos) =>
             {
                 var dims = source.DimensionsMM;
                 return factory.CreateRadialShelf(dims.x, dims.z, dims.y,
                     ((RadialShelfElement)source).CornerRadius, source.PartName, pos);
             },
             (source, copy) =>
             {
                 var src = (RadialShelfElement)source;
                 var made = copy.GetComponent<RadialShelfElement>();
                 if (made != null)
                     foreach (var side in GapSides.All) made.SetGap(side, src.GapOf(side));
                 CopyMaterial(source, copy);
             }),

            (el => el is FloorElement,
             (factory, source, pos) => factory.CreateFloor(source.DimensionsMM, source.PartName, pos),
             CopyMaterial),

            (el => el is LightSourceElement,
             (factory, source, pos) => factory.CreateLightSource(source.PartName, pos),
             null),

            (el => el is SinkElement,
             (factory, source, pos) => factory.CreateSink(source.PartName, pos),
             CopyMaterial),

            (el => el is CooktopElement,
             (factory, source, pos) =>
                 factory.CreateCooktop(source.PartName, pos, ((CooktopElement)source).Model),
             (source, copy) =>
             {
                 var src = (CooktopElement)source;
                 var made = copy.GetComponent<CooktopElement>();
                 made.DimensionsMM = src.DimensionsMM;
                 made.CutoutWidthMM = src.CutoutWidthMM;
                 made.CutoutDepthMM = src.CutoutDepthMM;
                 CopyMaterial(source, copy);
             }),

            (el => el is OvenElement,
             (factory, source, pos) => factory.CreateOven(source.PartName, pos),
             CopyMaterial),

            (el => el is DishwasherElement,
             (factory, source, pos) => factory.CreateDishwasher(source.PartName, pos),
             CopyMaterial),

            (el => el is PillarElement,
             (factory, source, pos) =>
                 factory.CreatePillar(((PillarElement)source).MidHeightMM, source.PartName, pos,
                     ((PillarElement)source).DiameterMM),
             CopyMaterial),

            (el => el is PipeElement,
             (factory, source, pos) =>
                 factory.CreatePipe(((PipeElement)source).SizeId, ((PipeElement)source).LengthMM,
                     source.PartName, pos),
             CopyMaterial),

            (el => el is TableElement,
             (factory, source, pos) => factory.CreateTable(source.DimensionsMM, source.PartName, pos),
             (source, copy) =>
             {
                 var made = copy.GetComponent<TableElement>();
                 if (made != null) made.LegInsetMM = ((TableElement)source).LegInsetMM;
                 CopyDecorSlots(source, copy);
             }),

            (el => el is RadiusTableElement,
             (factory, source, pos) => factory.CreateRadiusTable(source.DimensionsMM, source.PartName, pos),
             (source, copy) =>
             {
                 var made = copy.GetComponent<RadiusTableElement>();
                 if (made != null) made.LegInsetMM = ((RadiusTableElement)source).LegInsetMM;
                 CopyDecorSlots(source, copy);
             }),

            (el => el is ScrewLegElement,
             (factory, source, pos) => factory.CreateScrewLeg(source.PartName, pos),
             (source, copy) =>
             {
                 var src = (ScrewLegElement)source;
                 var made = copy.GetComponent<ScrewLegElement>();
                 if (made != null)
                 {
                     made.Thread = src.Thread;
                     made.BaseDiameterMM = src.BaseDiameterMM;
                     made.BaseHeightMM = src.BaseHeightMM;
                     made.ThreadLengthMM = src.ThreadLengthMM;
                     made.InsertionDepthMM = src.InsertionDepthMM;
                     ScrewLegHostLink.Apply(made, PartRegistry.GetAll());
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is StoolElement,
             (factory, source, pos) => factory.CreateStool(source.DimensionsMM,
                 ((StoolElement)source).CornerRadiusMM, source.PartName, pos),
             CopyDecorSlots),

            (el => el is ChairElement,
             (factory, source, pos) => factory.CreateChair(source.DimensionsMM,
                 ((ChairElement)source).CornerRadiusMM, ((ChairElement)source).SeatHeightMM,
                 source.PartName, pos),
             CopyDecorSlots),

            (el => el is SofaElement,
             (factory, source, pos) => factory.CreateSofa(source.DimensionsMM,
                 ((SofaElement)source).CornerRadiusMM, ((SofaElement)source).SeatHeightMM,
                 source.PartName, pos),
             CopyDecorSlots),

            (el => el is PouffeElement,
             (factory, source, pos) => factory.CreatePouffe(source.DimensionsMM,
                 ((PouffeElement)source).CornerRadiusMM,
                 ((PouffeElement)source).SeatThicknessMM, source.PartName, pos),
             CopyDecorSlots),

            (el => el is ToiletElement,
             (factory, source, pos) => factory.CreateToilet(
                 ((ToiletElement)source).SeatHeightMM, source.PartName, pos),
             (source, copy) =>
             {
                 var src = (ToiletElement)source;
                 var made = copy.GetComponent<ToiletElement>();
                 if (made != null)
                 {
                     made.PrimaryMaterialId = src.PrimaryMaterialId;
                     made.SecondaryMaterialId = src.SecondaryMaterialId;
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is WallHungToiletElement,
             (factory, source, pos) => factory.CreateWallHungToilet(
                 ((WallHungToiletElement)source).SeatHeightMM,
                 ((WallHungToiletElement)source).FlushPlateHeightMM, source.PartName, pos),
             (source, copy) =>
             {
                 var src = (WallHungToiletElement)source;
                 var made = copy.GetComponent<WallHungToiletElement>();
                 if (made != null)
                 {
                     made.PrimaryMaterialId = src.PrimaryMaterialId;
                     made.SecondaryMaterialId = src.SecondaryMaterialId;
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is BathtubElement,
             (factory, source, pos) => factory.CreateBathtub(source.DimensionsMM,
                 ((BathtubElement)source).RimWidthMM, ((BathtubElement)source).BowlDepthMM,
                 ((BathtubElement)source).BowlRadiusMM, ((BathtubElement)source).BowlFilletMM,
                 source.PartName, pos),
             CopyMaterial),

            (el => el is BathMixerElement,
             (factory, source, pos) => factory.CreateBathMixer(((BathMixerElement)source).Spec,
                 source.PartName, pos),
             CopyMaterial),

            (el => el is ShowerColumnElement,
             (factory, source, pos) => factory.CreateShowerColumn(
                 ((ShowerColumnElement)source).Spec, source.PartName, pos),
             CopyMaterial),

            (el => el is SocketElement,
             (factory, source, pos) => factory.CreateSocket(((SocketElement)source).Spec,
                 source.PartName, pos),
             CopyDecorSlots),

            (el => el is LightSwitchElement,
             (factory, source, pos) => factory.CreateLightSwitch(
                 ((LightSwitchElement)source).Spec, ((LightSwitchElement)source).IsOn,
                 LightNamesOf(source), source.PartName, pos),
             CopyDecorSlots),

            (el => el is BedElement,
             (factory, source, pos) => factory.CreateBed(source.DimensionsMM,
                 ((BedElement)source).IsDouble, ((BedElement)source).HasHeadboard,
                 source.PartName, pos),
             CopyDecorSlots),

            (el => el is WindowElement,
             (factory, source, pos) =>
             {
                 var src = (WindowElement)source;
                 return factory.CreateWindow(source.DimensionsMM, source.PartName, pos,
                     src.Tint, src.SillProtrusionMM);
             },
             (source, copy) =>
             {
                 var made = copy.GetComponent<WindowElement>();
                 if (made != null) made.Mode = ((WindowElement)source).Mode;
                 CopyMaterial(source, copy);
             }),

            (el => el is DoorElement,
             (factory, source, pos) => factory.CreateDoor(
                 source.DimensionsMM, source.PartName, pos, ((DoorElement)source).SashType),
             (source, copy) =>
             {
                 var made = copy.GetComponent<DoorElement>();
                 if (made != null) made.Mode = ((DoorElement)source).Mode;
                 CopyMaterial(source, copy);
             }),

            (el => el is FacadeElement,
             (factory, source, pos) =>
             {
                 var src = (FacadeElement)source;
                 return factory.CreateFacade(source.DimensionsMM, source.PartName, pos,
                     src.GapLeft, src.GapRight, src.GapTop, src.GapBottom, src.GapFront, src.GapBack);
             },
             (source, copy) =>
             {
                 var made = copy.GetComponent<FacadeElement>();
                 if (made != null) made.Mode = ((FacadeElement)source).Mode;
                 CopyMaterial(source, copy);
             }),

            (el => el is PanelElement,
             (factory, source, pos) => factory.CreatePanel(source.DimensionsMM, source.PartName, pos,
                 source.GapLeft, source.GapRight, source.GapTop, source.GapBottom,
                 source.GapFront, source.GapBack),
             CopyMaterial),

            (_ => true,
             (factory, source, pos) => factory.CreatePart(source.DimensionsMM, source.PartName, pos),
             CopyPlainPart),
        };

        public static GameObject Copy(IElementFactory factory, KitchenElement source, Vector3 position)
        {
            foreach (var (handles, spawn, extras) in ByType)
            {
                if (!handles(source)) continue;
                var copy = spawn(factory, source, position);
                copy.transform.rotation = source.transform.rotation;
                extras?.Invoke(source, copy);
                return copy;
            }
            throw new InvalidOperationException(
                "ElementDuplicators без замыкающей записи: " + source.PartName);
        }

        private static string[] LightNamesOf(KitchenElement source)
        {
            if (!(source is ILightSwitch sw)) return Array.Empty<string>();
            var names = sw.LightNames;
            var copy = new string[names.Count];
            for (int i = 0; i < names.Count; i++) copy[i] = names[i];
            return copy;
        }

        private static void CopyMaterial(KitchenElement source, GameObject copy)
        {
            var el = copy.GetComponent<KitchenElement>();
            if (el != null) MaterialManager.ApplyById(el, source.MaterialId);
        }

        private static void CopyDecorSlots(KitchenElement source, GameObject copy)
        {
            if (source is IHasTwoDecorSlots src
                && copy.GetComponent<KitchenElement>() is IHasTwoDecorSlots made)
            {
                made.PrimaryMaterialId = src.PrimaryMaterialId;
                made.SecondaryMaterialId = src.SecondaryMaterialId;
            }
            CopyMaterial(source, copy);
        }

        private static void CopyPlainPart(KitchenElement source, GameObject copy)
        {
            if (source.GetComponent<Wall>() != null) copy.AddComponent<Wall>();

            var made = copy.GetComponent<KitchenElement>();
            if (made == null) return;

            MaterialManager.ApplyById(made, source.MaterialId);

            if (made.SupportsGaps && source.SupportsGaps)
                foreach (var side in GapSides.All) made.SetGap(side, source.GapOf(side));

            if (made.SupportsGrooves)
            {
                made.SetGrooves(source.Grooves);
                var edges = EdgeBandingState.Of(source);
                made.EdgeBandingEnabled = edges.enabled;
                made.EdgeThicknessMM = edges.thicknessMM;
                made.EdgeForcedMask = edges.forcedMask;
                made.EdgeSuppressedMask = edges.suppressedMask;
            }

            if (made.SupportsTextureOverlays)
                made.SetTextureOverlays(source.TextureOverlays);
        }
    }
}
