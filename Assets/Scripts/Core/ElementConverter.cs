using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ElementConverter
    {
        public enum TargetType { Part, Facade, AssembledFacade, RadialShelf }

        public static KitchenElement Convert(KitchenElement source, TargetType targetType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var currentType = GetElementType(source);
            if (currentType == targetType) return source;

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

            int gapL = 2, gapR = 2, gapT = 2, gapB = 2;
            var mode = DoorMode.HingeFrontLeft;
            bool doorOpen = false;
            if (source is FacadeElement facade)
            {
                gapL = facade.GapLeft;
                gapR = facade.GapRight;
                gapT = facade.GapTop;
                gapB = facade.GapBottom;
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

            int radius = Mathf.Max(dims.x, dims.z);
            if (source is RadialShelfElement radial)
                radius = radial.Radius;

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
                    break;
                case TargetType.RadialShelf:
                    result = go.AddComponent<RadialShelfElement>();
                    break;
                default:
                    result = go.AddComponent<KitchenElement>();
                    break;
            }
            PartRegistry.Register(result);

            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            go.name = goName;

            result.PartName = partName;
            result.Movable = movable;
            result.GroupId = groupId;
            result.MaterialId = materialId;
            result.Transparent = transparent;

            // Размеры: для радиусной полки формируем из radius×thickness×radius.
            if (result is RadialShelfElement newRadial)
            {
                int thickness = Mathf.Max(1, dims.y);
                newRadial.Radius = Mathf.Max(1, radius);
                newRadial.DimensionsMM = new Vector3Int(newRadial.Radius, thickness, newRadial.Radius);
            }
            else
            {
                result.DimensionsMM = dims;
            }

            if (result is FacadeElement newFacade)
            {
                newFacade.GapLeft = gapL;
                newFacade.GapRight = gapR;
                newFacade.GapTop = gapT;
                newFacade.GapBottom = gapB;
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

        public static TargetType GetElementType(KitchenElement element)
        {
            if (element is AssembledFacadeElement) return TargetType.AssembledFacade;
            if (element is RadialShelfElement) return TargetType.RadialShelf;
            if (element is FacadeElement) return TargetType.Facade;
            return TargetType.Part;
        }
    }
}
