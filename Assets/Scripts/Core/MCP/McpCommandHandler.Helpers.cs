using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        // ── Поиск объектов и элементов ───────────────────────────────────

        private static KitchenElement? FindElementByName(string name)
        {
            foreach (var el in PartRegistry.All)
                if (el != null && (el.PartName == name || el.name == name))
                    return el;
            return null;
        }

        private static GameObject? FindGameObject(string path)
        {
            if (path.Contains("/"))
            {
                var parts = path.Split('/');
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root.name == parts[0])
                    {
                        var found = FindDescendant(root.transform, parts, 1);
                        if (found != null) return found;
                    }
                }
            }
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene.name == null) continue;
                if (go.name == path) return go;
            }
            return null;
        }

        private static GameObject? FindDescendant(Transform parent, string[] parts, int index)
        {
            if (index >= parts.Length) return parent.gameObject;
            foreach (Transform child in parent)
                if (child.name == parts[index])
                    return FindDescendant(child, parts, index + 1);
            return null;
        }

        private static BasePlate? FindFloor()
        {
            var go = GameObject.FindWithTag("Floor");
            if (go == null) return null;
            return go.GetComponent<BasePlate>();
        }

        /// <summary>Модуль по id или имени (без учёта регистра).</summary>
        private static LinkGroup? FindModule(string module)
        {
            if (string.IsNullOrEmpty(module)) return null;
            if (int.TryParse(module, out int id))
            {
                foreach (var g in GroupManager.AllGroups())
                    if (g.id == id) return g;
            }
            foreach (var g in GroupManager.AllGroups())
                if (string.Equals(g.name, module, StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        // ── Векторы и геометрия ─────────────────────────────────────────

        /// <summary>Собрать вектор из nullable-полей, беря текущее значение для
        /// отсутствующих осей (омитить = «не менять эту ось»).</summary>
        private static Vector3 ResolveVec(float? x, float? y, float? z, Vector3 current)
            => new Vector3(x ?? current.x, y ?? current.y, z ?? current.z);

        /// <summary>Собрать размеры (мм) из nullable-полей. Приоритет: width/height/depth,
        /// затем алиасы dimX/dimY/dimZ, затем текущий размер. Отсутствующее измерение =
        /// «не менять». Каждое измерение не меньше 1 мм.</summary>
        private static Vector3Int ResolveDims(int? width, int? height, int? depth,
            int? dimX, int? dimY, int? dimZ, Vector3Int current)
        {
            int w = width ?? dimX ?? current.x;
            int h = height ?? dimY ?? current.y;
            int d = depth ?? dimZ ?? current.z;
            return new Vector3Int(Mathf.Max(1, w), Mathf.Max(1, h), Mathf.Max(1, d));
        }

        private static AabbInfo ComputeAABB(Vector3[] vertices)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in vertices)
            {
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
            }
            return new AabbInfo { minX = minX, minY = minY, minZ = minZ, maxX = maxX, maxY = maxY, maxZ = maxZ };
        }

        private static bool AABBsOverlap(AabbInfo a, AabbInfo b)
        {
            return a.minX < b.maxX && a.maxX > b.minX &&
                   a.minY < b.maxY && a.maxY > b.minY &&
                   a.minZ < b.maxZ && a.maxZ > b.minZ;
        }

        private static Vector3Int GetEffectiveDimMM(KitchenElement el)
        {
            var facade = el as FacadeElement;
            if (facade != null)
            {
                return new Vector3Int(
                    el.DimensionsMM.x + facade.GapLeft + facade.GapRight,
                    el.DimensionsMM.y + facade.GapTop + facade.GapBottom,
                    el.DimensionsMM.z);
            }
            return el.DimensionsMM;
        }

        /// <summary>Глубина пересечения (мм) → категория серьёзности для агента.</summary>
        private static string ClassifyOverlapMm(float mm) =>
            Tolerance.IsNoiseMm(mm) ? "touching"
            : mm < 2f ? "minor_overlap"
            : mm < 10f ? "overlap"
            : "deep_penetration";

        /// <summary>Проекции AABB на две оси, КРОМЕ указанной, пересекаются (с допуском).
        /// Без этого «ближайшим по Y» может оказаться деталь из другого угла сцены.</summary>
        private static bool ProjectionsOverlapExceptAxis(AabbInfo a, AabbInfo b, int axis)
        {
            if (axis != 0 && !Tolerance.IntervalsOverlap(a.minX, a.maxX, b.minX, b.maxX)) return false;
            if (axis != 1 && !Tolerance.IntervalsOverlap(a.minY, a.maxY, b.minY, b.maxY)) return false;
            if (axis != 2 && !Tolerance.IntervalsOverlap(a.minZ, a.maxZ, b.minZ, b.maxZ)) return false;
            return true;
        }

        private static List<AxisGapInfo> ComputeAxisGaps(KitchenElement element, List<KitchenElement> allElements)
        {
            var elAabb = ComputeAABB(element.GetVertices());
            var gaps = new List<AxisGapInfo>();
            string[] axisNames = { "x", "y", "z" };
            float[] aMin = { elAabb.minX, elAabb.minY, elAabb.minZ };
            float[] aMax = { elAabb.maxX, elAabb.maxY, elAabb.maxZ };

            var others = new List<(KitchenElement el, AabbInfo aabb)>(allElements.Count);
            foreach (var other in allElements)
                if (other != null && other != element)
                    others.Add((other, ComputeAABB(other.GetVertices())));

            for (int axis = 0; axis < 3; axis++)
            {
                float bestGapUnits = float.MaxValue;
                string? bestNeighbor = null;
                float am = aMin[axis], ax = aMax[axis];

                foreach (var (other, oAabb) in others)
                {
                    if (!ProjectionsOverlapExceptAxis(elAabb, oAabb, axis)) continue;

                    float bMin = 0, bMax = 0;
                    if (axis == 0) { bMin = oAabb.minX; bMax = oAabb.maxX; }
                    else if (axis == 1) { bMin = oAabb.minY; bMax = oAabb.maxY; }
                    else { bMin = oAabb.minZ; bMax = oAabb.maxZ; }

                    float gap;
                    if (ax <= bMin) gap = bMin - ax;
                    else if (bMax <= am) gap = am - bMax;
                    else gap = -(Mathf.Min(ax, bMax) - Mathf.Max(am, bMin));

                    if (Mathf.Abs(gap) < Mathf.Abs(bestGapUnits))
                    {
                        bestGapUnits = gap;
                        bestNeighbor = other.PartName;
                    }
                }

                if (bestNeighbor == null) continue;

                float gapMM = bestGapUnits / AppConstants.MM_TO_UNITS;
                bool touching = Tolerance.IsNoiseMm(gapMM);
                gaps.Add(new AxisGapInfo
                {
                    axis = axisNames[axis],
                    neighbor = bestNeighbor,
                    gapMM = touching ? 0f : gapMM,
                    touching = touching,
                    isOverlap = !touching && gapMM < 0
                });
            }
            return gaps;
        }

        // ── ElementInfo builder ──────────────────────────────────────────

        /// <summary>Инфо об элементе, включая принадлежность модулю (группе) —
        /// чтобы через MCP была видна конфигурация сцены.</summary>
        private static ElementInfo BuildElementInfo(KitchenElement el)
        {
            return BuildElementInfo(el, null, false);
        }

        private static ElementInfo BuildElementInfo(KitchenElement el, List<KitchenElement>? allElements,
            bool includeFacadeValidation = false, ValidationResult? validation = null)
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

            var aabb = ComputeAABB(el.GetVertices());
            var effDim = GetEffectiveDimMM(el);
            var gaps = allElements != null ? ComputeAxisGaps(el, allElements) : null;
            var radial = el as RadialShelfElement;
            var drawer = el as DrawerElement;
            var table = el as TableElement;
            var radiusTable = el as RadiusTableElement;
            var window = el as WindowElement;
            var door = el as DoorElement;
            var pillar = el as PillarElement;
            var cooktop = el as CooktopElement;
            var oven = el as OvenElement;
            var dishwasher = el as DishwasherElement;
            FacadeValidationData? facadeValidation = includeFacadeValidation && el is FacadeElement fe && allElements != null
                ? ComputeFacadeValidation(fe, allElements)
                : (FacadeValidationData?)null;

            return new ElementInfo
            {
                name = el.PartName, type = el.GetType().Name,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                rotX = t.eulerAngles.x, rotY = t.eulerAngles.y, rotZ = t.eulerAngles.z,
                active = el.gameObject.activeInHierarchy,
                locked = !el.Movable,
                moduleId = group != null ? group.id : 0,
                moduleName = group != null ? group.name : null,
                materialId = el.MaterialId,
                hasViolations = hasViolations,
                aabbMinX = aabb.minX, aabbMinY = aabb.minY, aabbMinZ = aabb.minZ,
                aabbMaxX = aabb.maxX, aabbMaxY = aabb.maxY, aabbMaxZ = aabb.maxZ,
                worldDimX = Mathf.RoundToInt((aabb.maxX - aabb.minX) / AppConstants.MM_TO_UNITS),
                worldDimY = Mathf.RoundToInt((aabb.maxY - aabb.minY) / AppConstants.MM_TO_UNITS),
                worldDimZ = Mathf.RoundToInt((aabb.maxZ - aabb.minZ) / AppConstants.MM_TO_UNITS),
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z,
                faceGaps = gaps,
                cornerRadius = radial != null ? radial.CornerRadius : 0,
                grooves = el.Grooves.Count > 0 ? FormatGrooves(el) : null,
                textureOverlays = el.TextureOverlays.Count > 0 ? FormatTextureOverlays(el) : null,
                edgeBanding = el.SupportsEdges ? el.EdgeBandingEnabled : (bool?)null,
                edgeThicknessMM = el.SupportsEdges ? el.EdgeThicknessMM : (float?)null,
                // Поле контракта — на всю деталь: true, когда ручными помечены
                // все четыре стороны (частичный набор в MCP не выводится).
                edgeSkipValidation = el.SupportsEdges && el.EdgeManualMask == EdgeManual.AllMask
                    ? true : (bool?)null,
                edges = el.EdgeBandingEnabled && allElements != null
                    ? FormatEdges(el, allElements) : null,
                facadeMode = el is FacadeElement feMode ? FacadeDoor.WireName(feMode.Mode) : null,
                faceNormalX = facadeValidation?.normal.x,
                faceNormalY = facadeValidation?.normal.y,
                faceNormalZ = facadeValidation?.normal.z,
                faceInward = facadeValidation != null ? facadeValidation.Value.faceInward : (bool?)null,
                faceObstructions = facadeValidation?.obstructions,
                openingViolations = facadeValidation?.openingViolations,
                drawer = drawer != null ? new DrawerInfo
                {
                    system = drawer.System == DrawerSystem.Movento ? "movento" : "gtv",
                    drawerType = drawer.Type.ToString(),
                    drawerLength = drawer.NominalLength,
                    drawerColor = WireName(drawer.Color),
                    internalWidth = drawer.InternalWidth,
                    isDouble = drawer.IsDouble,
                    isUpper = drawer.IsUpperDrawer,
                    pairedDrawerName = drawer.PairedDrawerName,
                    attachedFacadeName = drawer.AttachedFacadeName,
                    doubleState = WireName(drawer.DoubleState),
                    isOpen = drawer.IsOpen
                } : null,
                table = table != null ? new TableInfo
                {
                    legInsetMM = table.LegInsetMM,
                    tabletopMaterialId = table.TabletopMaterialId,
                    legsMaterialId = table.LegsMaterialId
                } : null,
                radiusTable = radiusTable != null ? new RadiusTableInfo
                {
                    legInsetMM = radiusTable.LegInsetMM,
                    shape = "capsule",
                    tabletopMaterialId = radiusTable.TabletopMaterialId,
                    legsMaterialId = radiusTable.LegsMaterialId
                } : null,
                pillar = pillar != null ? new PillarInfo
                {
                    midHeightMM = pillar.MidHeightMM
                } : null,
                cooktop = cooktop != null ? new CooktopInfo
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
                } : null,
                oven = oven != null ? new OvenInfo
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
                } : null,
                dishwasher = dishwasher != null ? new DishwasherInfo
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
                    // Цоколь считается по ПРИСТЁГНУТОМУ фасаду: без него высота
                    // цоколя ещё не определена, и выдумывать номинал нечестно.
                    plinthMM = dishwasher.FindAttachedFacade() is FacadeElement dwFacade
                        ? DishwasherElement.PlinthForFacade(dwFacade.DimensionsMM.y)
                        : 0,
                    plinthMinMM = DishwasherElement.PLINTH_MIN_MM,
                    plinthMaxMM = DishwasherElement.PLINTH_MAX_MM,
                    plinthSetbackMM = DishwasherElement.PLINTH_SETBACK_MM,
                    plinthNicheMM = DishwasherElement.PLINTH_NICHE_MM,
                    facadeMountGapMM = DishwasherElement.FACADE_MOUNT_GAP_MM,
                    isOpen = dishwasher.IsOpen
                } : null,
                window = window != null ? new WindowInfo
                {
                    tint = WireName(window.Tint),
                    sillProtrusionMM = window.SillProtrusionMM,
                    mode = FacadeDoor.WireName(window.Mode),
                    isOpen = window.IsOpen,
                    attachedWallName = window.AttachedWallName
                } : null,
                door = door != null ? new DoorInfo
                {
                    sashType = WireName(door.SashType),
                    mode = FacadeDoor.WireName(door.Mode),
                    isOpen = door.IsOpen,
                    attachedWallName = door.AttachedWallName
                } : null
            };
        }

        private readonly struct FacadeValidationData
        {
            public readonly Vector3 normal;
            public readonly bool faceInward;
            public readonly List<FaceObstructionInfo> obstructions;
            public readonly List<OpeningViolationInfo> openingViolations;

            public FacadeValidationData(Vector3 normal, bool faceInward,
                List<FaceObstructionInfo> obstructions,
                List<OpeningViolationInfo> openingViolations)
            {
                this.normal = normal;
                this.faceInward = faceInward;
                this.obstructions = obstructions;
                this.openingViolations = openingViolations;
            }
        }

        private static FacadeValidationData ComputeFacadeValidation(FacadeElement facade, List<KitchenElement> allElements)
        {
            var normal = FacadeValidator.GetFaceNormal(facade);
            bool faceInward = FacadeValidator.IsFacingInward(facade);

            var rawObstructions = FacadeValidator.FindFaceObstructions(facade, allElements);
            var obstructions = new List<FaceObstructionInfo>(rawObstructions.Count);
            foreach (var o in rawObstructions)
            {
                obstructions.Add(new FaceObstructionInfo
                {
                    neighbor = o.neighbor,
                    distanceFromFaceMm = o.distanceFromFaceMm,
                    overlapWidthMm = o.overlapWidthMm,
                    overlapHeightMm = o.overlapHeightMm
                });
            }

            var rawOpening = FacadeValidator.FindOpeningViolations(facade, allElements);
            var opening = new List<OpeningViolationInfo>(rawOpening.Count);
            foreach (var v in rawOpening)
            {
                opening.Add(new OpeningViolationInfo
                {
                    neighbor = v.neighbor,
                    openingMode = v.openingMode,
                    collisionAtProgress = v.collisionAtProgress,
                    collisionOverlapMm = v.collisionOverlapMm
                });
            }

            return new FacadeValidationData(normal, faceInward, obstructions, opening);
        }

        private static (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)
            BuildFacadeResponseFields(KitchenElement element)
        {
            if (!(element is FacadeElement facade))
                return (null, false, null, null);

            var data = ComputeFacadeValidation(facade, PartRegistry.GetAll());
            var normal = new { x = data.normal.x, y = data.normal.y, z = data.normal.z };
            return (normal, data.faceInward, data.obstructions, data.openingViolations);
        }

        private static Dictionary<KitchenElement, (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)>
            ComputeFacadeViolations(List<KitchenElement> all)
        {
            var result = new Dictionary<KitchenElement, (object?, bool, object?, object?)>();
            foreach (var el in all)
            {
                if (!(el is FacadeElement facade)) continue;
                var fields = BuildFacadeResponseFields(facade);
                bool hasIssue = fields.faceInward ||
                    (fields.faceObstructions != null && ((List<FaceObstructionInfo>)fields.faceObstructions).Count > 0) ||
                    (fields.openingViolations != null && ((List<OpeningViolationInfo>)fields.openingViolations).Count > 0);
                if (hasIssue)
                    result[el] = fields;
            }
            return result;
        }

        private static Dictionary<KitchenElement, List<string>>
            ComputeDrawerViolations(List<KitchenElement> all)
        {
            var result = new Dictionary<KitchenElement, List<string>>();
            foreach (var el in all)
            {
                if (!(el is DrawerElement drawer)) continue;
                var validation = DrawerValidator.ValidateAll(drawer, all);
                if (!validation.IsValid)
                    result[el] = validation.Errors;
            }
            return result;
        }

        private static (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)
            BuildFacadeViolationFields(KitchenElement el, List<KitchenElement> all)
        {
            if (!(el is FacadeElement facade)) return (null, false, null, null);
            return BuildFacadeResponseFields(facade);
        }

        private static List<object> ComputeViolationOverlaps(KitchenElement el, List<KitchenElement> all)
        {
            var elAabb = ComputeAABB(el.GetVertices());
            var results = new List<object>();
            foreach (var other in all)
            {
                if (other == el || other == null) continue;
                var otherAabb = ComputeAABB(other.GetVertices());
                if (!AABBsOverlap(elAabb, otherAabb)) continue;

                float overlapX = Mathf.Min(elAabb.maxX, otherAabb.maxX) - Mathf.Max(elAabb.minX, otherAabb.minX);
                float overlapY = Mathf.Min(elAabb.maxY, otherAabb.maxY) - Mathf.Max(elAabb.minY, otherAabb.minY);
                float overlapZ = Mathf.Min(elAabb.maxZ, otherAabb.maxZ) - Mathf.Max(elAabb.minZ, otherAabb.minZ);
                float toMm = 1f / AppConstants.MM_TO_UNITS;
                float depthMm = Mathf.Min(overlapX, Mathf.Min(overlapY, overlapZ)) * toMm;
                if (Tolerance.IsNoiseMm(depthMm)) continue;

                results.Add(new {
                    kind = "overlap",
                    neighbor = other.PartName,
                    severity = ClassifyOverlapMm(depthMm),
                    penetrationMm = Mathf.Round(depthMm * 10f) / 10f,
                    overlapXmm = Mathf.Round(overlapX * toMm * 10f) / 10f,
                    overlapYmm = Mathf.Round(overlapY * toMm * 10f) / 10f,
                    overlapZmm = Mathf.Round(overlapZ * toMm * 10f) / 10f
                });
            }
            return results;
        }

        // ── Модули ──────────────────────────────────────────────────────

        private static ModuleInfo BuildModuleInfo(LinkGroup g)
        {
            return BuildModuleInfo(g, null);
        }

        private static ModuleInfo BuildModuleInfo(LinkGroup g, List<KitchenElement>? allElements,
            ValidationResult? validation = null)
        {
            var members = GroupManager.MembersOf(g);
            var info = new ModuleInfo
            {
                id = g.id,
                name = g.name,
                movable = g.movable,
                widthAxis = g.widthAxis,
                editing = ModuleEditMode.Active == g,
                elementCount = members.Count,
                elements = new List<ElementInfo>()
            };

            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var el in members)
            {
                if (el == null) continue;
                info.elements.Add(BuildElementInfo(el, allElements, false, validation));
                foreach (var v in el.GetVertices())
                {
                    min = Vector3.Min(min, v);
                    max = Vector3.Max(max, v);
                }
            }
            if (info.elements.Count > 0)
            {
                Vector3 c = (min + max) * 0.5f;
                Vector3 s = (max - min) / AppConstants.MM_TO_UNITS;
                info.boundsCenter = new[] { c.x, c.y, c.z };
                info.boundsSizeMM = new[]
                    { Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.y), Mathf.RoundToInt(s.z) };
            }
            return info;
        }

        // ── ETag / фильтр имён ─────────────────────────────────────────

        /// <summary>SHA256 хеш от JSON-представления списка для ETag. Использует
        /// McpJson (округление до 0.1 мм) — хеш не меняется от суб-миллиметрового дрейфа.</summary>
        private static string ComputeEtag(List<ElementInfo> list)
        {
            var json = McpJson.Serialize(list);
            var bytes = Encoding.UTF8.GetBytes(json);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>Совпадение имени с фильтром: подстрока или wildcard '*', без учёта регистра.</summary>
        private static bool NameMatchesFilter(string name, string filter)
        {
            if (filter.IndexOf('*') < 0)
                return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // ── Scene helpers ──────────────────────────────────────────────

        private HierarchyNode BuildHierarchyNode(GameObject go)
        {
            var node = new HierarchyNode
            {
                name = go.name,
                path = GetGameObjectPath(go),
                active = go.activeSelf,
                children = new List<HierarchyNode>()
            };
            foreach (Transform child in go.transform)
                node.children.Add(BuildHierarchyNode(child.gameObject));
            return node;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, "/");
                sb.Insert(0, t.name);
                t = t.parent;
            }
            return sb.ToString();
        }

        private static ElementDebugInfo BuildElementDebugInfo(KitchenElement el)
        {
            var verts = el.GetVertices();
            var aabb = ComputeAABB(verts);
            var srcFaces = el.GetFaces();
            var faces = new FaceInfo[srcFaces.Length];
            for (int i = 0; i < srcFaces.Length; i++)
            {
                faces[i] = new FaceInfo
                {
                    centerX = srcFaces[i].center.x, centerY = srcFaces[i].center.y, centerZ = srcFaces[i].center.z,
                    normalX = srcFaces[i].normal.x, normalY = srcFaces[i].normal.y, normalZ = srcFaces[i].normal.z,
                    sizeX = srcFaces[i].size.x, sizeY = srcFaces[i].size.y
                };
            }
            var vertices = new VertexInfo[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                vertices[i] = new VertexInfo { x = verts[i].x, y = verts[i].y, z = verts[i].z };
            var effDim = GetEffectiveDimMM(el);
            return new ElementDebugInfo
            {
                name = el.PartName, type = el.GetType().Name,
                aabb = aabb, faces = faces, vertices = vertices,
                dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                effectiveDimX = effDim.x, effectiveDimY = effDim.y, effectiveDimZ = effDim.z
            };
        }

        // ── Face parsing ───────────────────────────────────────────────

        /// <summary>"left"/"right"/"bottom"/"top"/"back"/"front" → ось (0/1/2) и сторона.</summary>
        private static bool TryParseFace(string s, out int axis, out bool maxSide)
        {
            axis = 0; maxSide = false;
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "left":   axis = 0; maxSide = false; return true;
                case "right":  axis = 0; maxSide = true;  return true;
                case "bottom": axis = 1; maxSide = false; return true;
                case "top":    axis = 1; maxSide = true;  return true;
                case "back":   axis = 2; maxSide = false; return true;
                case "front":  axis = 2; maxSide = true;  return true;
                default: return false;
            }
        }

        private static float AabbSide(AabbInfo aabb, int axis, bool maxSide)
        {
            if (axis == 0) return maxSide ? aabb.maxX : aabb.minX;
            if (axis == 1) return maxSide ? aabb.maxY : aabb.minY;
            return maxSide ? aabb.maxZ : aabb.minZ;
        }

        // ── Мутация: блокировка, подсветка, нарушения ──────────────────

        /// <summary>Единая проверка блокировки для мутирующих команд: null — можно
        /// менять; иначе готовый Error с единым текстом (один источник сообщения для
        /// модели во всех move/resize/rotate/delete).</summary>
        private static McpResponse? RequireMovable(KitchenElement element, string name, string reqId)
        {
            if (element.Movable) return null;
            return McpResponse.Error(reqId, -1,
                $"Element '{name}' is LOCKED, so move/resize/delete are rejected. " +
                "Unlock it with set_element_lock {locked:false} — but ONLY if the user explicitly allowed editing this element.");
        }

        /// <summary>Обновить подсветку (зелёная/красная) после мутации. Все пути мутации
        /// (MCP, UI, drag, undo/redo) должны вызывать это, иначе визуал устаревает.</summary>
        private static void RefreshElementHighlights()
        {
            var hl = Object.FindAnyObjectByType<ElementHighlighter>();
            if (hl != null)
                hl.RefreshHighlights();
        }

        /// <summary>Есть ли у элемента нарушения (пересечение / нет связности) в текущей сцене.</summary>
        private static bool HasViolations(KitchenElement element)
        {
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return false;
            var vr = ConstraintValidator.Validate(all);
            return vr != null && vr.violations.Contains(element);
        }

        /// <summary>Нарушения ИМЕННО этого элемента: пересечения (kind=overlap, с severity),
        /// оторванность от структуры, фасадные проблемы, ошибки ящика.
        /// Пустой список = элемент чист.</summary>
        private static List<object> BuildElementViolations(KitchenElement el, List<KitchenElement>? all, ValidationResult? vr)
        {
            var list = new List<object>();
            if (all == null || all.Count == 0) return list;

            var overlaps = ComputeViolationOverlaps(el, all);
            foreach (var o in overlaps) list.Add(o);

            if (vr != null && vr.violations.Contains(el) && overlaps.Count == 0)
                list.Add(new
                {
                    kind = "disconnected",
                    message = "Element is not face-to-face connected to the wall/floor structure."
                });

            if (el is FacadeElement facade)
            {
                var data = ComputeFacadeValidation(facade, all);
                if (data.faceInward)
                    list.Add(new
                    {
                        kind = "facade_facing_inward",
                        message = "The facade's front face points INTO the cabinet. Rotate it 180 degrees."
                    });
                foreach (var o in data.obstructions)
                    list.Add(new
                    {
                        kind = "face_obstruction",
                        neighbor = o.neighbor,
                        distanceFromFaceMm = o.distanceFromFaceMm,
                        overlapWidthMm = o.overlapWidthMm,
                        overlapHeightMm = o.overlapHeightMm
                    });
                foreach (var v in data.openingViolations)
                    list.Add(new
                    {
                        kind = "opening_collision",
                        neighbor = v.neighbor,
                        openingMode = v.openingMode,
                        collisionAtProgress = v.collisionAtProgress,
                        collisionOverlapMm = v.collisionOverlapMm
                    });
            }

            if (el is DrawerElement drawer)
            {
                var validation = DrawerValidator.ValidateAll(drawer, all);
                if (!validation.IsValid)
                    foreach (var err in validation.Errors)
                        list.Add(new { kind = "drawer_invalid", message = err });
            }

            return list;
        }

        /// <summary>Единый конверт ответа ВСЕХ мутаций: ok + полный ElementInfo +
        /// нарушения этого элемента + счётчик структурных нарушений по сцене.
        /// Модель видит результат и проблемы сразу, без второго запроса.</summary>
        private static object BuildMutationResult(KitchenElement el)
        {
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            return new
            {
                ok = true,
                element = BuildElementInfo(el, all, includeFacadeValidation: false, validation: vr),
                violations = BuildElementViolations(el, all, vr),
                sceneViolationCount = vr != null ? vr.violations.Count : 0
            };
        }
    }
}
