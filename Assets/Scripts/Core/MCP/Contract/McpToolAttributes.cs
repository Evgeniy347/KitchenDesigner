using System;

// ============================================================================
//  MCP tool contract — SINGLE SOURCE OF TRUTH for the tool surface.
// ----------------------------------------------------------------------------
//  These plain POCO + attribute types describe every MCP tool ONCE, in C#.
//  They are compiled BOTH by Unity (KitchenDesigner.Runtime asmdef, for the
//  in-app command handler) AND by the server (KitchenServer.McpContract links
//  the same physical files). From this one description we generate:
//    • mcp-server/src/tools.generated.ts  (Zod tool table for the local bridge)
//    • the ASP.NET server's tools/list     (real MCP over Streamable HTTP)
//
//  HARD CONSTRAINT: this folder must stay Unity-safe — NO UnityEngine, NO
//  Newtonsoft, only System / System.Collections.Generic — because Unity's Mono
//  and the .NET 10 server both compile it. Keep to conservative C# (block
//  namespaces, public fields, no records/init-only).
// ============================================================================

namespace KitchenDesigner.Core.MCP.Contract
{
    /// <summary>Tool category — drives the MCP annotation hints (read/write/destructive).</summary>
    public enum McpToolKind
    {
        Read,       // readOnlyHint: true
        Write,      // mutating, non-destructive
        Destructive // mutating, destructive (delete/dissolve)
    }

    /// <summary>
    /// Describes one field of a params class as an agent-facing tool parameter.
    /// Absence of this attribute on a public field means the field is NOT part of
    /// the tool schema (treat like <see cref="McpIgnoreAttribute"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class McpParamAttribute : Attribute
    {
        /// <summary>Human/LLM-facing description. Units MUST be stated (mm vs m).</summary>
        public string Description;

        /// <summary>true → required in the schema; false → optional.</summary>
        public bool Required;

        /// <summary>Agent-facing JSON name when it differs from the C# field name
        /// (e.g. wire field "gapLeft" exposed to the agent as "gap_left"). Null → use
        /// the field name. The generator/server apply this rename when forwarding.</summary>
        public string Name;

        /// <summary>Allowed string values → generates z.enum([...]) / JSON-schema enum.</summary>
        public string[] Enum;

        /// <summary>Numeric lower bound (inclusive). NaN → unset. For string[] it is minItems.</summary>
        public double Min = double.NaN;

        /// <summary>Numeric upper bound (inclusive). NaN → unset.</summary>
        public double Max = double.NaN;

        public McpParamAttribute(string description)
        {
            Description = description;
        }

        public bool HasMin { get { return !double.IsNaN(Min); } }
        public bool HasMax { get { return !double.IsNaN(Max); } }
    }

    /// <summary>
    /// Marks a public field of a params class as WIRE-ONLY: it participates in the
    /// JSON the Unity handler deserializes, but is NOT exposed to the agent as a
    /// tool parameter (e.g. legacy aliases dimX/dimY/dimZ, or template_name).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class McpIgnoreAttribute : Attribute
    {
    }

    /// <summary>One entry in the tool registry: everything needed to advertise a tool.</summary>
    public sealed class McpToolDef
    {
        public string Name;          // MCP tool name == Unity method name
        public string Title;         // short human title
        public string Description;   // LLM-facing description
        public McpToolKind Kind;
        public Type ParamsType;      // params POCO, or null for a no-argument tool
        public bool Cached;          // large read → ETag cache (get_all_elements)
        public bool StaticText;      // answered locally without a Unity call (guide)
        public bool OpenWorld;       // openWorldHint (execute_menu_item)

        public McpToolDef(string name, string title, string description,
            McpToolKind kind, Type paramsType,
            bool cached = false, bool staticText = false, bool openWorld = false)
        {
            Name = name;
            Title = title;
            Description = description;
            Kind = kind;
            ParamsType = paramsType;
            Cached = cached;
            StaticText = staticText;
            OpenWorld = openWorld;
        }
    }
}
