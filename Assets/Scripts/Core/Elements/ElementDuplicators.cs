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
             null),

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
             null),

            (el => el is DishwasherElement,
             (factory, source, pos) => factory.CreateDishwasher(source.PartName, pos),
             null),

            (el => el is PillarElement,
             (factory, source, pos) =>
                 factory.CreatePillar(((PillarElement)source).MidHeightMM, source.PartName, pos,
                     ((PillarElement)source).DiameterMM),
             CopyMaterial),

            (el => el is TableElement,
             (factory, source, pos) => factory.CreateTable(source.DimensionsMM, source.PartName, pos),
             CopyMaterial),

            (el => el is RadiusTableElement,
             (factory, source, pos) => factory.CreateRadiusTable(source.DimensionsMM, source.PartName, pos),
             (source, copy) =>
             {
                 var src = (RadiusTableElement)source;
                 var made = copy.GetComponent<RadiusTableElement>();
                 if (made != null)
                 {
                     made.LegInsetMM = src.LegInsetMM;
                     made.TabletopMaterialId = src.TabletopMaterialId;
                     made.LegsMaterialId = src.LegsMaterialId;
                 }
                 CopyMaterial(source, copy);
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
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is StoolElement,
             (factory, source, pos) => factory.CreateStool(source.DimensionsMM,
                 ((StoolElement)source).CornerRadiusMM, source.PartName, pos),
             (source, copy) =>
             {
                 var src = (StoolElement)source;
                 var made = copy.GetComponent<StoolElement>();
                 if (made != null)
                 {
                     made.TabletopMaterialId = src.TabletopMaterialId;
                     made.LegsMaterialId = src.LegsMaterialId;
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is ChairElement,
             (factory, source, pos) => factory.CreateChair(source.DimensionsMM,
                 ((ChairElement)source).CornerRadiusMM, ((ChairElement)source).SeatHeightMM,
                 source.PartName, pos),
             (source, copy) =>
             {
                 var src = (ChairElement)source;
                 var made = copy.GetComponent<ChairElement>();
                 if (made != null)
                 {
                     made.TabletopMaterialId = src.TabletopMaterialId;
                     made.LegsMaterialId = src.LegsMaterialId;
                 }
                 CopyMaterial(source, copy);
             }),

            (el => el is SofaElement,
             (factory, source, pos) => factory.CreateSofa(source.DimensionsMM,
                 ((SofaElement)source).CornerRadiusMM, ((SofaElement)source).SeatHeightMM,
                 source.PartName, pos),
             (source, copy) =>
             {
                 var src = (SofaElement)source;
                 var made = copy.GetComponent<SofaElement>();
                 if (made != null)
                 {
                     made.TabletopMaterialId = src.TabletopMaterialId;
                     made.LegsMaterialId = src.LegsMaterialId;
                 }
                 CopyMaterial(source, copy);
             }),

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

        private static void CopyMaterial(KitchenElement source, GameObject copy)
        {
            var el = copy.GetComponent<KitchenElement>();
            if (el != null) MaterialManager.ApplyById(el, source.MaterialId);
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
                made.EdgeManualMask = edges.manualMask;
            }

            if (made.SupportsTextureOverlays)
                made.SetTextureOverlays(source.TextureOverlays);
        }
    }
}
