using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KitchenDesigner.Core.MCP.Contract;

namespace McpContractGen;

public static partial class McpContractEmitter
{
    public const string OutputRelativePath = "mcp-server/src/tools.generated.ts";

    public const string RegenerateCommand = "npm run gen:tools";

    public static int ToolCount => McpToolRegistry.Tools.Count;

    public static string Emit()
    {
        var sb = new StringBuilder();
        sb.Append(
"""
// ===========================================================================
//  AUTO-GENERATED — DO NOT EDIT BY HAND.
//  Source of truth: Assets/Scripts/Core/MCP/Contract/*.cs (McpToolRegistry).
//  Regenerate with:  npm run gen:tools   (in mcp-server/)
// ===========================================================================
import { z } from "zod";

export type GenToolKind = "read" | "write" | "destructive";

export interface GenTool {
  name: string;
  title: string;
  description: string;
  kind: GenToolKind;
  cached?: boolean;
  staticText?: boolean;
  openWorld?: boolean;
  inputSchema?: Record<string, z.ZodTypeAny>;
  /** agent-facing param name -> Unity wire field name (applied when forwarding). */
  rename?: Record<string, string>;
}

export const GEN_TOOLS: GenTool[] = [

""");

        foreach (var tool in McpToolRegistry.Tools)
        {
            sb.Append("  {\n");
            sb.Append(CultureInfo.InvariantCulture, $"    name: {TsString(tool.Name)},\n");
            sb.Append(CultureInfo.InvariantCulture, $"    title: {TsString(tool.Title)},\n");
            sb.Append(CultureInfo.InvariantCulture, $"    description: {TsString(tool.Description)},\n");
            sb.Append(CultureInfo.InvariantCulture, $"    kind: {TsString(KindStr(tool.Kind))},\n");
            if (tool.Cached) sb.Append("    cached: true,\n");
            if (tool.StaticText) sb.Append("    staticText: true,\n");
            if (tool.OpenWorld) sb.Append("    openWorld: true,\n");

            if (tool.ParamsType != null)
            {
                var rename = new List<(string agent, string wire)>();
                var lines = new List<string>();
                foreach (var f in tool.ParamsType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.GetCustomAttribute<McpIgnoreAttribute>() != null) continue;
                    var p = f.GetCustomAttribute<McpParamAttribute>();
                    if (p == null) continue;

                    var agentName = string.IsNullOrEmpty(p.Name) ? f.Name : p.Name!;
                    if (agentName != f.Name) rename.Add((agentName, f.Name));
                    lines.Add($"      {agentName}: {BuildZod(f, p)},");
                }

                if (lines.Count > 0)
                {
                    sb.Append("    inputSchema: {\n");
                    sb.Append(string.Join("\n", lines));
                    sb.Append("\n    },\n");
                }
                if (rename.Count > 0)
                {
                    var pairs = rename.Select(r => $"{TsObjectKey(r.agent)}: {TsString(r.wire)}");
                    sb.Append(CultureInfo.InvariantCulture, $"    rename: {{ {string.Join(", ", pairs)} }},\n");
                }
            }

            sb.Append("  },\n");
        }

        sb.Append("];\n");

        sb.Append("\n/** guide topic -> cheat-sheet text (served locally by the bridge). */\n");
        sb.Append(CultureInfo.InvariantCulture, $"export const GUIDE_DEFAULT_TOPIC = {TsString(McpGuideTexts.DefaultTopic)};\n");
        sb.Append("export const GUIDE_TEXTS: Record<string, string> = {\n");
        foreach (var kv in McpGuideTexts.Topics)
            sb.Append(CultureInfo.InvariantCulture, $"  {TsObjectKey(kv.Key)}: {TsString(kv.Value)},\n");
        sb.Append("};\n");

        return sb.ToString().Replace("\r\n", "\n");
    }

    private static string KindStr(McpToolKind k) => k switch
    {
        McpToolKind.Read => "read",
        McpToolKind.Destructive => "destructive",
        _ => "write",
    };

    private static string TsString(string s) => JsonSerializer.Serialize(s);

    private static string TsObjectKey(string s) => PlainIdentifier().IsMatch(s) ? s : TsString(s);

    private static string BuildZod(FieldInfo field, McpParamAttribute p)
    {
        var t = field.FieldType;
        var u = Nullable.GetUnderlyingType(t) ?? t;
        string expr;

        if (p.Enum is { Length: > 0 })
        {
            expr = "z.enum([" + string.Join(", ", p.Enum.Select(TsString)) + "])";
        }
        else if (u == typeof(string))
        {
            expr = "z.string()";
            if (p.Required) expr += ".min(1)";
        }
        else if (u == typeof(int) || u == typeof(long))
        {
            expr = "z.number().int()";
            if (p.HasMin) expr += $".min({(long)p.Min})";
            if (p.HasMax) expr += $".max({(long)p.Max})";
        }
        else if (u == typeof(float) || u == typeof(double))
        {
            expr = "z.number().finite()";
            if (p.HasMin) expr += $".min({Num(p.Min)})";
            if (p.HasMax) expr += $".max({Num(p.Max)})";
        }
        else if (u == typeof(bool))
        {
            expr = "z.boolean()";
        }
        else if (t == typeof(string[]))
        {
            expr = "z.array(z.string().min(1))";
            if (p.HasMin) expr += $".min({(long)p.Min})";
        }
        else if (t.IsArray && t.GetElementType() is { IsClass: true } et && et != typeof(string))
        {
            var inner = new List<string>();
            foreach (var f2 in et.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f2.GetCustomAttribute<McpIgnoreAttribute>() != null) continue;
                var p2 = f2.GetCustomAttribute<McpParamAttribute>();
                if (p2 == null) continue;
                if (!string.IsNullOrEmpty(p2.Name) && p2.Name != f2.Name)
                    throw new InvalidOperationException(
                        "Nested param rename is not supported (the bridge applies rename only to " +
                        $"top-level fields): {et.Name}.{f2.Name} -> {p2.Name}");
                inner.Add($"{TsObjectKey(f2.Name)}: {BuildZod(f2, p2)}");
            }
            expr = "z.array(z.object({ " + string.Join(", ", inner) + " }))";
            if (p.HasMin) expr += $".min({(long)p.Min})";
            if (p.HasMax) expr += $".max({(long)p.Max})";
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported param type {t.Name} on {field.DeclaringType?.Name}.{field.Name}");
        }

        if (!p.Required) expr += ".optional()";
        expr += $".describe({TsString(p.Description)})";
        return expr;
    }

    private static string Num(double d) => d.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex PlainIdentifier();
}
