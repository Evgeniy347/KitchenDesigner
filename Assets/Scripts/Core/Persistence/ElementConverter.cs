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
            var currentType = GetElementType(source);
            if (currentType == targetType) return source;

            // Ящик GTV — самостоятельный тип со собственной геометрией/состоянием: он не
            // участвует в конвертации ни в одну сторону (план §10.1). Выпадающий список типа
            // в контекстном меню отражает «Ящик GTV» как индикатор, но конвертация — no-op.
            if (currentType == TargetType.Drawer || targetType == TargetType.Drawer) return source;
            if (currentType == TargetType.Window || targetType == TargetType.Window) return source;
            if (currentType == TargetType.Door || targetType == TargetType.Door) return source;

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

            int cornerRadius = AppConstants.RADIAL_CORNER_RADIUS_DEFAULT;
            if (source is RadialShelfElement radial)
                cornerRadius = radial.CornerRadius;

            // Пазы снимаем ДО удаления компонента: ClearGrooves возвращает
            // встроенный куб и один материал. Иначе новый тип унаследовал бы от
            // детали меш с пазами — причём уже уничтоженный в OnDestroy.
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

            // Старый компонент уже снят с учёта в PartRegistry, поэтому его имя
            // свободно и элемент сохраняет его при конвертации типа.
            result.PartName = ElementNaming.Normalize(partName, result);
            result.Movable = movable;
            result.GroupId = groupId;
            result.MaterialId = materialId;
            result.Transparent = transparent;

            // Размеры сохраняются как есть; для радиусной полки дополнительно
            // задаётся радиус угла (клампится к min(ширина, глубина)).
            result.DimensionsMM = dims;
            if (result is RadialShelfElement newRadial)
                newRadial.CornerRadius = cornerRadius;

            // Пазы переносятся только между типами, которые их поддерживают
            // (сейчас — базовая «деталь»); в фасад/полку они не уезжают.
            if (result.SupportsGrooves && grooves.Count > 0)
                result.SetGrooves(grooves);

            // Параметры кромкования переносятся ВСЕГДА, даже в фасад, который
            // их не показывает: иначе конвертация «деталь → фасад → деталь»
            // молча возвращала бы настройки к умолчанию.
            result.EdgeBandingEnabled = edges.enabled;
            result.EdgeThicknessMM = edges.thicknessMM;
            result.EdgeManualMask = edges.manualMask;

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
