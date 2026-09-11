using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementConverter
    {
        public enum TargetType { Part, Facade, AssembledFacade, RadialShelf, Drawer, Window, Door }

        public static KitchenElement Convert(KitchenElement source, TargetType targetType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!CanConvert(source)) return source;
            if (targetType == TargetType.Drawer) return source;
            if (targetType == TargetType.Window) return source;
            if (targetType == TargetType.Door) return source;
            if (GetElementType(source) == targetType) return source;

            var go = source.gameObject;

            var pos = go.transform.position;
            var rot = go.transform.rotation;
            var scale = go.transform.localScale;
            var goName = go.name;

            var partName = source.PartName;
            var dims = source.DimensionsMM;
            var movable = source.Movable;
            var groupId = source.GroupId;
            var materialId = source.MaterialId;
            var transparent = source.Transparent;
            var attachedToName = source.AttachedToName;

            var srcGaps = source.SupportsGaps ? source.Gaps : BoxGaps.None;
            bool srcIsFacade = source is FacadeElement;
            var mode = DoorMode.HingeFrontLeft;
            bool doorOpen = false;
            if (source is FacadeElement facade)
            {
                mode = facade.Mode;
                doorOpen = facade.IsOpen;
            }

            var fill = AssembledFill.Blind;
            int grooveCount = AppConstants.ASSEMBLED_DEFAULT_GROOVES;
            if (source is AssembledFacadeElement assembled)
            {
                fill = assembled.Fill;
                grooveCount = assembled.GrooveCount;
            }

            int cornerRadius = AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
            if (source is RadialShelfElement radial)
                cornerRadius = radial.CornerRadius;

            var grooves = new System.Collections.Generic.List<GrooveSpec>(source.Grooves);
            var edges = EdgeBandingState.Of(source);
            source.ClearGrooves();

            PartRegistry.Unregister(source);
            UnityEngine.Object.DestroyImmediate(source);

            KitchenElement result;
            switch (targetType)
            {
                case TargetType.AssembledFacade:
                    result = go.AddComponent<AssembledFacadeElement>();
                    break;
                case TargetType.Facade:
                    result = go.AddComponent<FacadeElement>();
                    ResetToCubeMesh(go);
                    break;
                case TargetType.RadialShelf:
                    result = go.AddComponent<RadialShelfElement>();
                    break;
                default:
                    result = go.AddComponent<KitchenElement>();
                    ResetToCubeMesh(go);
                    break;
            }
            PartRegistry.Register(result);

            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            go.name = goName;

            result.PartName = ElementNaming.Normalize(partName, result);
            result.Movable = movable;
            result.GroupId = groupId;
            result.MaterialId = materialId;
            result.Transparent = transparent;
            result.AttachedToName = AttachLinks.CanBeChild(result) ? attachedToName : "";

            result.DimensionsMM = dims;
            if (result is RadialShelfElement newRadial)
                newRadial.CornerRadius = cornerRadius;

            if (result.SupportsGrooves && grooves.Count > 0)
                result.SetGrooves(grooves);

            result.EdgeBandingEnabled = edges.enabled;
            result.EdgeThicknessMM = edges.thicknessMM;
            result.EdgeForcedMask = edges.forcedMask;
            result.EdgeSuppressedMask = edges.suppressedMask;

            if (result.SupportsGaps)
            {
                bool resultIsFacade = result is FacadeElement;
                var gaps = srcIsFacade == resultIsFacade
                    ? srcGaps
                    : resultIsFacade
                        ? new BoxGaps(FacadeElement.DEFAULT_GAP_MM, FacadeElement.DEFAULT_GAP_MM,
                            FacadeElement.DEFAULT_GAP_MM, FacadeElement.DEFAULT_GAP_MM)
                        : BoxGaps.None;
                foreach (var side in GapSides.All)
                    result.SetGap(side, gaps.Of(side));
            }

            if (result is FacadeElement newFacade)
            {
                newFacade.Mode = mode;
                if (doorOpen) newFacade.SetOpen(true);
            }

            if (result is AssembledFacadeElement newAssembled)
            {
                newAssembled.Fill = fill;
                newAssembled.GrooveCount = grooveCount;
            }

            return result;
        }

        public static bool CanConvert(KitchenElement? element)
        {
            if (element == null) return false;
            if (element.GetComponent<Wall>() != null) return false;
            if (element.GetComponent<BasePlate>() != null) return false;
            if (element is AssembledFacadeElement) return true;
            if (element is RadialShelfElement) return true;
            if (element is FacadeElement) return true;
            return element.GetType() == typeof(KitchenElement);
        }

        public static TargetType TargetTypeOf(ElementData data)
        {
            if (data == null) return TargetType.Part;
            if (data.isDrawer) return TargetType.Drawer;
            if (data.isWindow) return TargetType.Window;
            if (data.isDoor) return TargetType.Door;
            if (data.assembled) return TargetType.AssembledFacade;
            if (data.isRadialShelf) return TargetType.RadialShelf;
            if (data.isFacade) return TargetType.Facade;
            return TargetType.Part;
        }

        public static TargetType GetElementType(KitchenElement element)
        {
            if (element is AssembledFacadeElement) return TargetType.AssembledFacade;
            if (element is RadialShelfElement) return TargetType.RadialShelf;
            if (element is DrawerElement) return TargetType.Drawer;
            if (element is WindowElement) return TargetType.Window;
            if (element is DoorElement) return TargetType.Door;
            if (element is FacadeElement) return TargetType.Facade;
            return TargetType.Part;
        }

        private static Mesh? _cubeMesh;

        private static void ResetToCubeMesh(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && (mf.sharedMesh == null || mf.sharedMesh.name != "Cube"))
            {
                if (_cubeMesh == null)
                {
                    _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    if (_cubeMesh == null)
                    {
                        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        _cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                        UnityEngine.Object.DestroyImmediate(tmp);
                    }
                }
                mf.sharedMesh = _cubeMesh;
            }

            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) UnityEngine.Object.DestroyImmediate(mc);

            if (go.GetComponent<BoxCollider>() == null)
                go.AddComponent<BoxCollider>();
        }
    }
}
