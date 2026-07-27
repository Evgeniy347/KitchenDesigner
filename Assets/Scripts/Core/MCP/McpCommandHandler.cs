using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using KitchenDesigner.Core.MCP.Contract;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KitchenDesigner.Core.MCP
{
    /// <summary>
    /// Диспетчер MCP-команд. Каждый метод обрабатывается в отдельном partial-файле.
    ///
    /// Карта файлов (редактируй сразу нужный, не читай этот целиком):
    /// — McpCommandHandler.Scene.cs               → ping, status, hierarchy, raw-transform
    /// — McpCommandHandler.Elements.Query.cs      → get_* elements, violations, floor, gaps, snap
    /// — McpCommandHandler.Elements.Mutation.cs   → edit/clone/align/distribute/create/convert/delete/select/resize
    /// — McpCommandHandler.Modules.cs             → module create/dissolve/add/remove/edit
    /// — McpCommandHandler.Info.cs                → settings, spec, export, screenshot, drawers, materials
    /// — McpCommandHandler.Helpers.cs             → Find*, Compute*, BuildElementInfo, валидация
    ///
    /// Протокольные соглашения:
    /// - ВСЕ адресные операции — батчи (names[]/ops[]/items[]); одноэлементных нет.
    /// - Батчи атомарны: любая невалидная операция отклоняет весь батч.
    /// - edit_elements — единственный редактор свойств; dry_run:true — симуляция.
    /// - После каждой мутации смотри violations/sceneViolationCount в ответе.
    /// - dimZ всегда толщина детали (Board convention в AGENTS.md).
    /// </summary>
    public partial class McpCommandHandler
    {
        /// <summary>
        /// Диспетчер MCP-команд. Каждый метод обрабатывается в отдельном handler'е.
        /// Карта файлов — в xml-doc класса выше.
        /// </summary>
        public McpResponse Handle(McpRequest request)
        {
            FrameRateManager.KeepAwake(1f);
            try
            {
                switch (request.method)
                {
                    // ── Meta (→ McpCommandHandler.Scene.cs) ────────────
                    case "ping": return HandlePing(request);
                    case "get_status": return HandleGetStatus(request);

                    // ── Advanced: raw objects (→ .Scene.cs) ─────────────
                    case "get_scene_hierarchy": return HandleGetSceneHierarchy(request);
                    case "find_objects": return HandleFindObjects(request);
                    case "get_object_info": return HandleGetObjectInfo(request);
                    case "set_object_active": return HandleSetActive(request);
                    case "delete_object": return HandleDeleteObject(request);
                    case "set_position": return HandleSetPosition(request);
                    case "set_rotation": return HandleSetRotation(request);
                    case "set_scale": return HandleSetScale(request);
                    case "execute_menu_item": return HandleExecuteMenuItem(request);
                    case "enter_play_mode": return HandleEnterPlayMode(request);
                    case "exit_play_mode": return HandleExitPlayMode(request);

                    // ── Elements: query (→ .Elements.Query.cs) ─────────
                    case "get_all_elements": return HandleGetAllElements(request);
                    case "get_elements": return HandleGetElements(request);
                    case "get_element_debug": return HandleGetElementDebug(request);
                    case "get_element_gaps": return HandleGetElementGaps(request);
                    case "get_violations": return HandleGetViolations(request);
                    case "get_floor_info": return HandleGetFloorInfo(request);
                    case "snap_diagnose": return HandleSnapDiagnose(request);
                    case "get_free_space": return HandleGetFreeSpace(request);

                    // ── Elements: mutation (→ .Elements.Mutation.cs) ───
                    case "edit_elements": return HandleEditElements(request);
                    case "clone_elements": return HandleCloneElements(request);
                    case "align_elements": return HandleAlignElements(request);
                    case "distribute_evenly": return HandleDistributeEvenly(request);
                    case "create_elements": return HandleCreateElements(request);
                    case "convert_elements": return HandleConvertElements(request);
                    case "delete_elements": return HandleDeleteElements(request);
                    case "select_elements": return HandleSelectElements(request);
                    case "resize_floor": return HandleResizeFloor(request);

                    // ── Modules (→ .Modules.cs) ─────────────────────────
                    case "get_modules": return HandleGetModules(request);
                    case "module_info": return HandleModuleInfo(request);
                    case "create_module": return HandleCreateModule(request);
                    case "dissolve_module": return HandleDissolveModule(request);
                    case "add_to_module": return HandleAddToModule(request);
                    case "remove_from_module": return HandleRemoveFromModule(request);
                    case "enter_module_edit": return HandleEnterModuleEdit(request);
                    case "exit_module_edit": return HandleExitModuleEdit(request);

                    // ── Diagnostics / settings (→ .Info.cs) ─────────────
                    case "get_specification": return HandleGetSpecification(request);
                    case "export_specification_csv": return HandleExportCsv(request);
                    case "get_console_logs": return HandleConsoleLogs(request);
                    case "get_settings": return HandleGetSettings(request);
                    case "get_project_instructions": return HandleGetProjectInstructions(request);
                    case "set_project_instructions": return HandleSetProjectInstructions(request);

                    // ── v2: массовые/реляционные операции (→ .Bulk.cs) ──
                    case "get_scene_tree": return HandleGetSceneTree(request);
                    case "get": return HandleGetCompact(request);
                    case "preview_floorplan": return HandlePreviewFloorplan(request);
                    case "set_attr": return HandleSetAttr(request);
                    case "move": return HandleMove(request);
                    case "resize_module": return HandleResizeModule(request);
                    case "group": return HandleGroupV2(request);
                    case "align": return HandleAlignSelection(request);
                    case "create_walls": return HandleCreateWalls(request);
                    case "create_floor": return HandleCreateFloorV2(request);
                    case "add_opening": return HandleAddOpening(request);
                    case "set_setting": return HandleSetSetting(request);
                    case "set_snap_verbose": return HandleSetSnapVerbose(request);
                    case "take_screenshot": return HandleTakeScreenshot(request);
                    case "cycle_drawer_animation": return HandleCycleDrawerAnimation(request);
                    case "list_materials": return HandleListMaterials(request);
                    case "reload_textures": return HandleReloadTextures(request);

                    default:
                        return McpResponse.Error(request.id, -32601, $"Unknown method: {request.method}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Error handling '{request.method}': {ex.Message}\n{ex.StackTrace}");
                return McpResponse.Error(request.id, -1, $"Internal error: {ex.Message}");
            }
        }
    }
}
