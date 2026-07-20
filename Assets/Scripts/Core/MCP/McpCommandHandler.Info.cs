using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public partial class McpCommandHandler
    {
        private McpResponse HandleGetSpecification(McpRequest req)
        {
            var spec = SpecificationManager.Build(PartRegistry.GetAll());
            var lines = spec.lines.Select(l => new SpecLineInfo
            {
                name = l.name,
                dimX = l.dimensionsMM.x, dimY = l.dimensionsMM.y, dimZ = l.dimensionsMM.z,
                count = l.count, areaPerBoardM2 = l.areaPerBoardM2, totalAreaM2 = l.totalAreaM2
            }).ToList();

            return McpResponse.Result(req.id, new SpecInfo
            {
                lines = lines, totalCount = spec.totalCount, totalAreaM2 = spec.totalAreaM2
            });
        }

        private McpResponse HandleExportCsv(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsExportCsv>();
            if (p == null || string.IsNullOrEmpty(p.path))
                return McpResponse.Error(req.id, -32602, "path required");
            var spec = SpecificationManager.Build(PartRegistry.GetAll());
            SpecificationExport.SaveToFile(spec, p.path);
            return McpResponse.Result(req.id, new { ok = true, path = p.path });
        }

        private McpResponse HandleConsoleLogs(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsLogCount>();
            int count = (p != null && p.count > 0) ? Mathf.Min(p.count, 200) : 50;
            var entries = ConsoleLogCapture.GetRecent(count);
            return McpResponse.Result(req.id, entries);
        }

        private McpResponse HandleGetSettings(McpRequest req)
        {
            var s = KitchenSettings.Instance;
            if (s == null) return McpResponse.Error(req.id, -1, "KitchenSettings not loaded");
            return McpResponse.Result(req.id, new
            {
                snapEnabled = s.SnapEnabled,
                snapThresholdMM = s.SnapThreshold,
                gridEnabled = s.GridEnabled,
                gridStepMM = s.GridStep,
                blockOnViolation = s.BlockOnViolation,
                autoSave = s.AutoSave,
                autoSaveIntervalSec = s.AutoSaveInterval,
                snapVerboseLog = SnapSystem.VerboseLog,
                cameraPanFree = s.CameraPanFree
            });
        }

        private McpResponse HandleSetSetting(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetSetting>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name and value required");

            var s = KitchenSettings.Instance;
            if (s == null) return McpResponse.Error(req.id, -1, "KitchenSettings not loaded");

            switch (p.name.ToLowerInvariant())
            {
                case "lower_near_walls": s.LowerNearWalls = p.value; break;
                case "snap_enabled": s.SnapEnabled = p.value; break;
                case "grid_enabled": s.GridEnabled = p.value; break;
                case "walls_enabled": s.WallsEnabled = p.value; break;
                case "camera_pan_free": s.CameraPanFree = p.value; break;
                default:
                    return McpResponse.Error(req.id, -32602, $"Unknown setting: {p.name}");
            }

            Debug.Log($"[MCP] Setting '{p.name}' = {p.value}");
            return McpResponse.Result(req.id, new { ok = true, name = p.name, value = p.value });
        }

        private McpResponse HandleSetSnapVerbose(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetEnabled>();
            if (p == null) return McpResponse.Error(req.id, -32602, "enabled required");
            SnapSystem.VerboseLog = p.enabled;
            Debug.Log($"[MCP] Snap verbose log: {p.enabled}");
            return McpResponse.Result(req.id, new { ok = true, enabled = p.enabled });
        }

        private McpResponse HandleTakeScreenshot(McpRequest req)
        {
            var path = Path.Combine(Application.temporaryCachePath, "mcp_screenshot.png");
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            var bytes = ImageConversion.EncodeToPNG(tex);
            File.WriteAllBytes(path, bytes);
            Object.Destroy(tex);
            return McpResponse.Result(req.id, new { ok = true, path });
        }

        private McpResponse HandleCycleDrawerAnimation(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsNames>();
            if (p == null || p.names == null || p.names.Length == 0)
                return McpResponse.Error(req.id, -32602, "names required (non-empty array)");

            var results = new List<object>();
            var errors = new List<string>();
            foreach (var name in p.names)
            {
                var el = FindElementByName(name);
                if (el == null) { errors.Add($"Element not found: {name}"); continue; }
                var drawer = el as DrawerElement;
                if (drawer == null) { errors.Add($"Element '{name}' is not a drawer"); continue; }

                if (drawer.IsDouble)
                    drawer.CycleDoubleState();
                else
                    drawer.ToggleOpen();
                results.Add(new { name, isDouble = drawer.IsDouble, isOpen = drawer.IsOpen, doubleState = drawer.DoubleState.ToString() });
            }
            if (errors.Count > 0)
                return McpResponse.Error(req.id, -1,
                    "cycle_drawer_animation rejected, NOTHING was cycled: " + string.Join(" | ", errors));

            Debug.Log($"[MCP] Cycled {results.Count} drawers");
            return McpResponse.Result(req.id, new { ok = true, results, errors = errors.Count > 0 ? errors : null });
        }

        private McpResponse HandleListMaterials(McpRequest req)
        {
            var list = new List<object>();
            foreach (var m in MaterialCatalog.All)
                list.Add(new
                {
                    id = m.id,
                    name = m.displayName,
                    kind = m.kind,
                    hasTexture = m.texture != null || !string.IsNullOrEmpty(m.baseMapResource),
                    tileWidthMM = m.tileSizeMM,
                    tileHeightMM = m.TileHeightMM
                });
            return McpResponse.Result(req.id, new { materials = list, defaultId = MaterialCatalog.DefaultId });
        }

        private McpResponse HandleReloadTextures(McpRequest req)
        {
            int n = ExternalTextureCatalog.LoadAll();

            var all = PartRegistry.GetAll();
            if (all != null)
                foreach (var el in all)
                    if (el != null) MaterialManager.ApplyById(el, el.MaterialId);
            RefreshElementHighlights();

            Debug.Log($"[MCP] reload_textures: {n} decors from {ExternalTextureCatalog.DirectoryPath}");
            return McpResponse.Result(req.id, new
            {
                ok = true,
                loaded = n,
                directory = ExternalTextureCatalog.DirectoryPath,
                totalMaterials = MaterialCatalog.All.Count
            });
        }
    }
}
