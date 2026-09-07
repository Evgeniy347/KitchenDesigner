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

        private static Vector3 ResolveVec(float? x, float? y, float? z, Vector3 current)
            => new Vector3(x ?? current.x, y ?? current.y, z ?? current.z);

        private static Vector3Int ResolveDims(int? width, int? height, int? depth,
            int? dimX, int? dimY, int? dimZ, Vector3Int current)
        {
            int w = width ?? dimX ?? current.x;
            int h = height ?? dimY ?? current.y;
            int d = depth ?? dimZ ?? current.z;
            return new Vector3Int(Mathf.Max(1, w), Mathf.Max(1, h), Mathf.Max(1, d));
        }

        private static (object? faceNormal, bool faceInward, object? faceObstructions, object? openingViolations)
            BuildFacadeResponseFields(KitchenElement element)
        {
            if (!(element is FacadeElement facade))
                return (null, false, null, null);

            var data = McpFacadeIssues.Of(facade, PartRegistry.GetAll());
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
            var elAabb = McpAabb.Of(el.GetVertices());
            var results = new List<object>();
            foreach (var other in all)
            {
                if (other == el || other == null) continue;
                var otherAabb = McpAabb.Of(other.GetVertices());
                if (!McpAabb.Overlap(elAabb, otherAabb)) continue;

                float overlapX = Mathf.Min(elAabb.maxX, otherAabb.maxX) - Mathf.Max(elAabb.minX, otherAabb.minX);
                float overlapY = Mathf.Min(elAabb.maxY, otherAabb.maxY) - Mathf.Max(elAabb.minY, otherAabb.minY);
                float overlapZ = Mathf.Min(elAabb.maxZ, otherAabb.maxZ) - Mathf.Max(elAabb.minZ, otherAabb.minZ);
                float toMm = 1f / AppConstants.MM_TO_UNITS;
                float depthMm = Mathf.Min(overlapX, Mathf.Min(overlapY, overlapZ)) * toMm;
                if (Tolerance.IsNoiseMm(depthMm)) continue;

                results.Add(new {
                    kind = "overlap",
                    neighbor = other.PartName,
                    severity = McpAabb.ClassifyOverlapMm(depthMm),
                    penetrationMm = Mathf.Round(depthMm * 10f) / 10f,
                    overlapXmm = Mathf.Round(overlapX * toMm * 10f) / 10f,
                    overlapYmm = Mathf.Round(overlapY * toMm * 10f) / 10f,
                    overlapZmm = Mathf.Round(overlapZ * toMm * 10f) / 10f
                });
            }
            return results;
        }

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
                info.elements.Add(ElementInfoBuilder.Build(el, allElements, false, validation));
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
                info.boundsCenterMm = new[] { McpAnchor.ToMm(c.x), McpAnchor.ToMm(c.y), McpAnchor.ToMm(c.z) };
                info.boundsSizeMM = new[]
                    { Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.y), Mathf.RoundToInt(s.z) };
            }
            return info;
        }

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

        private static bool NameMatchesFilter(string name, string filter)
        {
            if (filter.IndexOf('*') < 0)
                return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(filter).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

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
            var aabb = McpAabb.Of(verts);
            var srcFaces = el.GetFaces();
            var faces = new FaceInfo[srcFaces.Length];
            for (int i = 0; i < srcFaces.Length; i++)
            {
                faces[i] = new FaceInfo
                {
                    centerXMm = McpAnchor.ToMm(srcFaces[i].center.x), centerYMm = McpAnchor.ToMm(srcFaces[i].center.y), centerZMm = McpAnchor.ToMm(srcFaces[i].center.z),
                    normalX = srcFaces[i].normal.x, normalY = srcFaces[i].normal.y, normalZ = srcFaces[i].normal.z,
                    sizeXMm = McpAnchor.ToMm(srcFaces[i].size.x), sizeYMm = McpAnchor.ToMm(srcFaces[i].size.y)
                };
            }
            var vertices = new VertexInfo[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                vertices[i] = new VertexInfo { xMm = McpAnchor.ToMm(verts[i].x), yMm = McpAnchor.ToMm(verts[i].y), zMm = McpAnchor.ToMm(verts[i].z) };
            var effDim = McpAabb.EffectiveDimMM(el);
            return new ElementDebugInfo
            {
                name = el.PartName, type = el.GetType().Name,
                aabb = McpAnchor.ToMmBox(aabb), faces = faces, vertices = vertices,
                dimXMm = el.DimensionsMM.x, dimYMm = el.DimensionsMM.y, dimZMm = el.DimensionsMM.z,
                effectiveDimXMm = effDim.x, effectiveDimYMm = effDim.y, effectiveDimZMm = effDim.z
            };
        }

        private static McpResponse? RequireMovable(KitchenElement element, string name, string reqId)
        {
            if (element.Movable) return null;
            return McpResponse.Error(reqId, -1,
                $"Element '{name}' is LOCKED, so move/resize/delete are rejected. " +
                "Unlock it with set_element_lock {locked:false} — but ONLY if the user explicitly allowed editing this element.");
        }

        private static void SettleSceneAfterMutation()
        {
            SceneChangeTracker.SettleDerivedLinks();
            var hl = Object.FindAnyObjectByType<ElementHighlighter>();
            if (hl != null)
                hl.RefreshHighlights();
        }

        private static bool HasViolations(KitchenElement element)
        {
            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0) return false;
            var vr = ConstraintValidator.Validate(all);
            return vr != null && vr.violations.Contains(element);
        }

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
                var data = McpFacadeIssues.Of(facade, all);
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

        private static object BuildMutationResult(KitchenElement el)
        {
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;
            return new
            {
                ok = true,
                element = ElementInfoBuilder.Build(el, all, includeFacadeValidation: false, validation: vr),
                violations = BuildElementViolations(el, all, vr),
                sceneViolationCount = vr != null ? vr.violations.Count : 0
            };
        }
    }
}
