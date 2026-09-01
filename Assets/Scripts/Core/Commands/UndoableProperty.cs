using System;

namespace KitchenDesigner.Core
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class UndoableAttribute : Attribute
    {
        public int Order { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class NotUndoableAttribute : Attribute
    {
        public string Reason { get; }

        public NotUndoableAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
