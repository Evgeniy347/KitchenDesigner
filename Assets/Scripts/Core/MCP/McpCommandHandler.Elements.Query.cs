using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleGetAllElements(McpRequest req)
        {
            var elements = PartRegistry.GetAll();
            var vr = elements != null && elements.Count > 0 ? ConstraintValidator.Validate(elements) : null;
            var list = new List<ElementInfo>();
            if (elements == null) return McpResponse.Error(req.id, -1, "PartRegistry is not initialized");
            foreach (var el in elements)
            {
                if (el == null) continue;
                list.Add(ElementInfoBuilder.Build(el, elements, false, vr));
            }

            var etag = ComputeEtag(list);

            if (req.Headers != null
                && req.Headers.TryGetValue("If-None-Match", out var clientEtag)
                && clientEtag == etag)
            {
                return McpResponse.NotModified(req.id, etag);
            }

            var result = McpResponse.Result(req.id, list);
            result.etag = etag;
            return result;
        }

        private McpResponse HandleGetElements(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsGetElements>() ?? new ParamsGetElements();
            var all = PartRegistry.GetAll();
            var vr = all != null && all.Count > 0 ? ConstraintValidator.Validate(all) : null;

            var matched = new List<KitchenElement>();
            var missing = new List<string>();

            if (p.names != null && p.names.Length > 0)
            {
                foreach (var name in p.names)
                {
                    var el = FindElementByName(name);
                    if (el == null) missing.Add(name);
                    else matched.Add(el);
                }
            }

            if (!string.IsNullOrEmpty(p.filter) && all != null)
            {
                foreach (var el in all)
                    if (el != null && !matched.Contains(el) && NameMatchesFilter(el.PartName, p.filter!))
                        matched.Add(el);
            }

            if ((p.names == null || p.names.Length == 0) && string.IsNullOrEmpty(p.filter) && all != null)
            {
                foreach (var el in all)
                    if (el != null) matched.Add(el);
            }

            object elements;
            if (p.summary)
            {
                var list = new List<object>();
                foreach (var el in matched)
                {
                    var pos = el.GetComponent<Wall>() is Wall w ? w.FullPosition : el.transform.position;
                    list.Add(new
                    {
                        name = el.PartName,
                        type = el.GetType().Name,
                        posX = pos.x, posY = pos.y, posZ = pos.z,
                        dimX = el.DimensionsMM.x, dimY = el.DimensionsMM.y, dimZ = el.DimensionsMM.z,
                        rotY = el.transform.eulerAngles.y,
                        locked = !el.Movable,
                        hasViolations = vr != null && vr.violations.Contains(el)
                    });
                }
                elements = list;
            }
            else
            {
                var list = new List<ElementInfo>();
                foreach (var el in matched)
                    list.Add(ElementInfoBuilder.Build(el, all, false, vr));
                elements = list;
            }

            return McpResponse.Result(req.id, new
            {
                count = matched.Count,
                elements,
                missing = missing.Count > 0 ? missing : null
            });
        }

        private McpResponse HandleGetElementDebug(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var results = new List<object>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { missing.Add(name); continue; }
                results.Add(BuildElementDebugInfo(el));
            }
            return McpResponse.Result(req.id, new { count = results.Count, results, missing = missing.Count > 0 ? missing : null });
        }

        private McpResponse HandleGetElementGaps(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var all = PartRegistry.GetAll();
            var results = new List<object>();
            var missing = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { missing.Add(name); continue; }
                var gaps = all != null ? McpAabb.AxisGaps(el, all) : new List<AxisGapInfo>();
                results.Add(new ElementGapsResult { name = el.PartName, gaps = gaps });
            }
            return McpResponse.Result(req.id, new { count = results.Count, results, missing = missing.Count > 0 ? missing : null });
        }

        private McpResponse HandleGetViolations(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsGetViolations>();
            HashSet<string>? nameFilter = null;
            if (p?.names != null && p.names.Length > 0)
                nameFilter = new HashSet<string>(p.names, StringComparer.OrdinalIgnoreCase);
            bool Wanted(KitchenElement e) => nameFilter == null || nameFilter.Contains(e.PartName);

            var all = PartRegistry.GetAll();
            if (all == null || all.Count == 0)
                return McpResponse.Result(req.id, new { violations = new string[0], count = 0 });

            var result = ConstraintValidator.Validate(all);
            var facadeIssues = ComputeFacadeViolations(all);
            var drawerIssues = ComputeDrawerViolations(all);

            var list = new List<object>();
            var seen = new HashSet<KitchenElement>();

            foreach (var el in result.violations)
            {
                seen.Add(el);
                if (!Wanted(el)) continue;
                var overlaps = ComputeViolationOverlaps(el, all);
                var facadeFields = BuildFacadeViolationFields(el, all);
                list.Add(new {
                    name = el.PartName,
                    type = el.GetType().Name,
                    overlapsWith = overlaps,
                    disconnected = overlaps.Count == 0,
                    facadeFields.faceNormal,
                    facadeFields.faceInward,
                    facadeFields.faceObstructions,
                    facadeFields.openingViolations
                });
            }

            foreach (var kvp in facadeIssues)
            {
                var el = kvp.Key;
                if (seen.Contains(el)) continue;
                seen.Add(el);
                if (!Wanted(el)) continue;
                list.Add(new {
                    name = el.PartName,
                    type = el.GetType().Name,
                    overlapsWith = new List<object>(),
                    disconnected = false,
                    kvp.Value.faceNormal,
                    kvp.Value.faceInward,
                    kvp.Value.faceObstructions,
                    kvp.Value.openingViolations
                });
            }

            foreach (var kvp in drawerIssues)
            {
                var el = kvp.Key;
                if (seen.Contains(el)) continue;
                seen.Add(el);
                if (!Wanted(el)) continue;
                list.Add(new {
                    name = el.PartName,
                    type = el.GetType().Name,
                    overlapsWith = new List<object>(),
                    disconnected = false,
                    faceNormal = (object?)null,
                    faceInward = false,
                    faceObstructions = (object?)null,
                    openingViolations = (object?)null,
                    drawerValidationErrors = kvp.Value
                });
            }

            var issues = new List<object>();
            foreach (var iss in KitchenDesigner.Core.Analysis.SceneAnalyzer.Analyze())
            {
                if (nameFilter != null)
                {
                    bool match = (iss.Target != null && nameFilter.Contains(iss.Target.PartName))
                              || (iss.Secondary != null && nameFilter.Contains(iss.Secondary.PartName));
                    if (!match) continue;
                }
                issues.Add(new
                {
                    level = iss.Level.ToString().ToLowerInvariant(),
                    code = iss.Code,
                    detail = iss.Detail,
                    message = iss.Message,
                    target = iss.Target != null ? iss.Target.PartName : null,
                    secondary = iss.Secondary != null ? iss.Secondary.PartName : null,
                });
            }

            return McpResponse.Result(req.id, new
            {
                violations = list, count = list.Count,
                issues, issueCount = issues.Count
            });
        }

        private McpResponse HandleGetFloorInfo(McpRequest req)
        {
            var plate = FindFloor();
            if (plate == null || plate.Element == null)
                return McpResponse.Error(req.id, -1, "Floor not found");

            var el = plate.Element;
            var dims = el.DimensionsMM;
            var pos = el.transform.position;
            return McpResponse.Result(req.id, new
            {
                name = el.PartName,
                dimX = dims.x, dimY = dims.y, dimZ = dims.z,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                hasViolations = HasViolations(el)
            });
        }

        private McpResponse HandleSnapDiagnose(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSnapDiagnose>();
            if (p == null || p.ops == null || p.ops.Length == 0)
                return McpResponse.Error(req.id, -32602, "ops required (non-empty array)");

            var results = new List<object>();
            var missing = new List<string>();
            foreach (var op in p.ops)
            {
                var element = FindElementByName(op.name);
                if (element == null) { missing.Add(op.name); continue; }
                var pos = McpAnchor.PositionForAnchorMm(op.anchor_x_mm, op.anchor_y_mm, op.anchor_z_mm,
                    McpAnchor.MinCornerOffset(element), element.transform.position);
                var diagnosis = SnapSystem.Diagnose(element, PartRegistry.GetAll(), pos);
                results.Add(diagnosis);
            }
            return McpResponse.Result(req.id, new { results, missing = missing.Count > 0 ? missing : null });
        }

        private McpResponse HandleGetFreeSpace(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsGetFreeSpace>();
            if (p == null || p.between == null || p.between.Length != 2)
                return McpResponse.Error(req.id, -32602, "between: exactly 2 board names required");

            var elA = FindElementByName(p.between[0]);
            if (elA == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.between[0]}");
            var elB = FindElementByName(p.between[1]);
            if (elB == null) return McpResponse.Error(req.id, -1, $"Element not found: {p.between[1]}");

            var a = McpAabb.Of(elA.GetVertices());
            var b = McpAabb.Of(elB.GetVertices());
            float[] aMin = { a.minX, a.minY, a.minZ }, aMax = { a.maxX, a.maxY, a.maxZ };
            float[] bMin = { b.minX, b.minY, b.minZ }, bMax = { b.maxX, b.maxY, b.maxZ };

            int sepAxis = -1;
            float bestGap = Tolerance.ContactMm * AppConstants.MM_TO_UNITS;
            for (int axis = 0; axis < 3; axis++)
            {
                float gap = Mathf.Max(bMin[axis] - aMax[axis], aMin[axis] - bMax[axis]);
                if (gap > bestGap) { bestGap = gap; sepAxis = axis; }
            }
            if (sepAxis < 0)
                return McpResponse.Result(req.id, new
                {
                    free = false,
                    message = "The two boards overlap or touch — there is no free box between them."
                });

            var lo = new float[3];
            var hi = new float[3];
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == sepAxis)
                {
                    if (aMax[axis] <= bMin[axis]) { lo[axis] = aMax[axis]; hi[axis] = bMin[axis]; }
                    else { lo[axis] = bMax[axis]; hi[axis] = aMin[axis]; }
                }
                else
                {
                    lo[axis] = Mathf.Max(aMin[axis], bMin[axis]);
                    hi[axis] = Mathf.Min(aMax[axis], bMax[axis]);
                    if (hi[axis] <= lo[axis])
                        return McpResponse.Result(req.id, new
                        {
                            free = false,
                            message = $"The boards do not face each other: their projections do not overlap on the {(axis == 0 ? "X" : axis == 1 ? "Y" : "Z")} axis."
                        });
                }
            }

            var box = new AabbInfo { minX = lo[0], minY = lo[1], minZ = lo[2], maxX = hi[0], maxY = hi[1], maxZ = hi[2] };
            var blockers = new List<object>();
            foreach (var other in PartRegistry.GetAll())
            {
                if (other == null || other == elA || other == elB) continue;
                var o = McpAabb.Of(other.GetVertices());
                bool intersects =
                    Tolerance.IntervalsOverlap(o.minX, o.maxX, box.minX, box.maxX) &&
                    Tolerance.IntervalsOverlap(o.minY, o.maxY, box.minY, box.maxY) &&
                    Tolerance.IntervalsOverlap(o.minZ, o.maxZ, box.minZ, box.maxZ);
                if (intersects) blockers.Add(new { name = other.PartName, type = other.GetType().Name });
            }

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            return McpResponse.Result(req.id, new
            {
                free = true,
                separationAxis = sepAxis == 0 ? "x" : sepAxis == 1 ? "y" : "z",
                sizeMmX = Mathf.RoundToInt((hi[0] - lo[0]) * toMm),
                sizeMmY = Mathf.RoundToInt((hi[1] - lo[1]) * toMm),
                sizeMmZ = Mathf.RoundToInt((hi[2] - lo[2]) * toMm),
                minX = lo[0], minY = lo[1], minZ = lo[2],
                maxX = hi[0], maxY = hi[1], maxZ = hi[2],
                centerX = (lo[0] + hi[0]) * 0.5f,
                centerY = (lo[1] + hi[1]) * 0.5f,
                centerZ = (lo[2] + hi[2]) * 0.5f,
                blockers = blockers.Count > 0 ? blockers : null
            });
        }
    }
}
