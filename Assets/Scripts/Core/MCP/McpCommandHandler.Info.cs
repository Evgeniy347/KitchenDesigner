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
        private const int ScreenshotMsaaSamples = 4;

        private McpResponse HandleGetSpecification(McpRequest req)
        {
            var spec = SpecificationManager.Build(PartRegistry.GetAll());
            var lines = spec.lines.Select(l => new SpecLineInfo
            {
                name = l.name,
                dimXMm = l.dimensionsMM.x, dimYMm = l.dimensionsMM.y, dimZMm = l.dimensionsMM.z,
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
            var data = new System.Collections.Generic.Dictionary<string, object>(
                System.StringComparer.Ordinal);
            foreach (var key in SettingKeys.All) data[key.Field] = key.Read();
            return McpResponse.Result(req.id, data);
        }

        private McpResponse HandleGetProjectInstructions(McpRequest req)
        {
            return McpResponse.Result(req.id, new { text = ProjectInstructions.Text });
        }

        private McpResponse HandleSetProjectInstructions(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetProjectInstructions>();
            if (p == null)
                return McpResponse.Error(req.id, -32602, "text required");
            ProjectInstructions.Text = p.text ?? "";
            Debug.Log($"[MCP] Project instructions updated ({ProjectInstructions.Text.Length} chars)");
            return McpResponse.Result(req.id, new { ok = true });
        }

        private McpResponse HandleSetSetting(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetSetting>();
            if (p == null || string.IsNullOrEmpty(p.name))
                return McpResponse.Error(req.id, -32602, "name and value required");

            var s = KitchenSettings.Instance;
            if (s == null) return McpResponse.Error(req.id, -1, "KitchenSettings not loaded");

            var key = SettingKeys.Find(p.name.ToLowerInvariant());
            if (key == null)
                return McpResponse.Error(req.id, -32602, $"Unknown setting: {p.name}");

            if (key.IsNumber)
            {
                if (!p.number.HasValue)
                    return McpResponse.Error(req.id, -32602,
                        $"Setting '{p.name}' takes a number: send 'number', not 'value'");
                key.WriteNumber!(p.number.Value);
            }
            else
            {
                if (!p.value.HasValue)
                    return McpResponse.Error(req.id, -32602,
                        $"Setting '{p.name}' is on/off: send 'value', not 'number'");
                key.WriteFlag!(p.value.Value);
            }

            PhotoMode.RefreshIfActive();

            var stored = key.Read();
            Debug.Log($"[MCP] Setting '{p.name}' = {stored}");
            return McpResponse.Result(req.id, new { ok = true, name = p.name, value = stored });
        }

        private McpResponse HandleSetSnapVerbose(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsSetEnabled>();
            if (p == null) return McpResponse.Error(req.id, -32602, "enabled required");
            SnapSystem.VerboseLog = p.enabled;
            Debug.Log($"[MCP] Snap verbose log: {p.enabled}");
            return McpResponse.Result(req.id, new { ok = true, enabled = p.enabled });
        }

        private McpResponse HandleSetPhotoCamera(McpRequest req)
        {
            var p = req.Params?.ToObjectStrict<ParamsPhotoCamera>();
            if (p == null) return McpResponse.Error(req.id, -32602, "params required");

            var cam = Object.FindAnyObjectByType<CameraController>();
            if (cam == null) return McpResponse.Error(req.id, -1, "CameraController not found");

            var state = cam.GetState();
            state.photoTargetX = p.target_x_mm.HasValue ? McpAnchor.FromMm(p.target_x_mm.Value) : state.photoTargetX;
            state.photoTargetY = p.target_y_mm.HasValue ? McpAnchor.FromMm(p.target_y_mm.Value) : state.photoTargetY;
            state.photoTargetZ = p.target_z_mm.HasValue ? McpAnchor.FromMm(p.target_z_mm.Value) : state.photoTargetZ;
            state.photoAngleX = p.angle_x ?? state.photoAngleX;
            state.photoAngleY = p.angle_y ?? state.photoAngleY;
            state.photoDistance = p.distance_mm.HasValue ? McpAnchor.FromMm(p.distance_mm.Value) : state.photoDistance;
            cam.SetState(state);

            var applied = cam.GetState();
            return McpResponse.Result(req.id, new
            {
                ok = true,
                photoActive = PhotoMode.Active,
                target_x_mm = McpAnchor.ToMm(applied.photoTargetX),
                target_y_mm = McpAnchor.ToMm(applied.photoTargetY),
                target_z_mm = McpAnchor.ToMm(applied.photoTargetZ),
                angle_x_deg = applied.photoAngleX,
                angle_y_deg = applied.photoAngleY,
                distance_mm = McpAnchor.ToMm(applied.photoDistance)
            });
        }

        private McpResponse HandleTakeScreenshot(McpRequest req)
        {
            var cam = Camera.main;
            if (cam == null) return McpResponse.Error(req.id, -1, "No main camera");

            var shot = req.Params?.ToObjectStrict<ParamsScreenshot>();
            int renders = Mathf.Clamp(shot?.renders ?? 1, 1, 64);

            var path = Path.Combine(Application.temporaryCachePath, "mcp_screenshot.png");
            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);

            var s = KitchenSettings.Instance;
            bool photo = PhotoMode.Active && s != null;
            float scale = photo && s!.PhotoSupersampling ? s.PhotoRenderScalePct * 0.01f : 1f;
            int msaa = photo && s!.PhotoAntiAliasing ? ScreenshotMsaaSamples : 1;
            int rw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
            int rh = Mathf.Max(1, Mathf.RoundToInt(h * scale));

            var rt = RenderTexture.GetTemporary(rw, rh, 24, RenderTextureFormat.Default,
                RenderTextureReadWrite.Default, msaa);
            var flat = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            double renderMs = 0;
            try
            {
                cam.targetTexture = rt;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < renders; i++) cam.Render();
                Graphics.Blit(rt, flat);
                RenderTexture.active = flat;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                renderMs = clock.Elapsed.TotalMilliseconds;
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(flat);
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(tex);
            }
            return McpResponse.Result(req.id, new
            {
                ok = true, path, widthPx = w, heightPx = h,
                renderWidthPx = rw, renderHeightPx = rh, msaaSamples = msaa, renders,
                renderMs = System.Math.Round(renderMs, 2)
            });
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
            {
                var tile = MaterialManager.TileMM(m);
                list.Add(new
                {
                    id = m.id,
                    name = m.displayName,
                    kind = m.kind,
                    hasTexture = m.HasTextureFile,
                    textureLoaded = m.texture != null,
                    tileWidthMM = tile.x,
                    tileHeightMM = tile.y
                });
            }
            return McpResponse.Result(req.id, new { materials = list, defaultId = MaterialCatalog.DefaultId });
        }

        private McpResponse HandleReloadTextures(McpRequest req)
        {
            int n = TextureLibrary.Reload();
            SettleSceneAfterMutation();

            Debug.Log($"[MCP] reload_textures: {n} decors from {TextureLibrary.DirectoryPath}");
            return McpResponse.Result(req.id, new
            {
                ok = true,
                loaded = n,
                directory = TextureLibrary.DirectoryPath,
                totalMaterials = MaterialCatalog.All.Count
            });
        }
    }
}
