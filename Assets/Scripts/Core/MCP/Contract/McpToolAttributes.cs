using System;

namespace KitchenDesigner.Core.MCP.Contract
{
    public enum McpToolKind
    {
        Read,
        Write,
        Destructive
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class McpParamAttribute : Attribute
    {
        public string Description;

        public bool Required;

        public string AgentName = string.Empty;

        public string[] Enum = Array.Empty<string>();

        public double Min = double.NaN;

        public double Max = double.NaN;

        public McpParamAttribute(string description)
        {
            Description = description;
        }

        public bool HasMin { get { return !double.IsNaN(Min); } }
        public bool HasMax { get { return !double.IsNaN(Max); } }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class McpIgnoreAttribute : Attribute
    {
    }

    public sealed class McpToolDef
    {
        public string Name;
        public string Title;
        public string Description;
        public McpToolKind Kind;
        public Type? ParamsType;
        public bool Cached;
        public bool StaticText;
        public bool OpenWorld;

        public McpToolDef(string name, string title, string description,
            McpToolKind kind, Type? paramsType,
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
