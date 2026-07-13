using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using KitchenDesigner.Core.MCP.Contract;

// ============================================================================
//  McpContractGen — emits mcp-server/src/tools.generated.ts from the C# tool
//  contract (McpToolRegistry). The generated Zod tool table drives the LOCAL
//  stdio bridge (index.ts); the ASP.NET server builds its tools/list from the
//  same contract at runtime. Run: `npm run gen:tools` in mcp-server/.
//
//  Usage: McpContractGen [outputPath]   (default: src/tools.generated.ts)
// ============================================================================

var outPath = args.Length > 0 ? args[0] : Path.Combine("src", "tools.generated.ts");

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
    sb.Append($"    name: {JsStr(tool.Name)},\n");
    sb.Append($"    title: {JsStr(tool.Title)},\n");
    sb.Append($"    description: {JsStr(tool.Description)},\n");
    sb.Append($"    kind: {JsStr(KindStr(tool.Kind))},\n");
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
            var pairs = rename.Select(r => $"{JsKey(r.agent)}: {JsStr(r.wire)}");
            sb.Append($"    rename: {{ {string.Join(", ", pairs)} }},\n");
        }
    }

    sb.Append("  },\n");
}

sb.Append("];\n");

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, sb.ToString().Replace("\r\n", "\n"));
Console.WriteLine($"[McpContractGen] wrote {McpToolRegistry.Tools.Count} tools -> {Path.GetFullPath(outPath)}");


static string KindStr(McpToolKind k) => k switch
{
    McpToolKind.Read => "read",
    McpToolKind.Destructive => "destructive",
    _ => "write",
};

// JSON string == valid TS string literal (quotes, escapes, unicode handled).
static string JsStr(string s) => JsonSerializer.Serialize(s);

// Object key: only quote if not a plain identifier.
static string JsKey(string s) =>
    System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Za-z_][A-Za-z0-9_]*$") ? s : JsStr(s);

static string BuildZod(FieldInfo field, McpParamAttribute p)
{
    var t = field.FieldType;
    var u = Nullable.GetUnderlyingType(t) ?? t;
    string expr;

    if (p.Enum is { Length: > 0 })
    {
        expr = "z.enum([" + string.Join(", ", p.Enum.Select(JsStr)) + "])";
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
    else
    {
        throw new InvalidOperationException(
            $"Unsupported param type {t.Name} on {field.DeclaringType?.Name}.{field.Name}");
    }

    if (!p.Required) expr += ".optional()";
    expr += $".describe({JsStr(p.Description)})";
    return expr;
}

static string Num(double d) => d.ToString(CultureInfo.InvariantCulture);
