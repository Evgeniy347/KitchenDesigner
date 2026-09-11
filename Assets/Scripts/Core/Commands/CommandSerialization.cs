using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public interface ISerializableCommand
    {
        CommandRecord? ToRecord(Func<KitchenElement, int> indexOf);
    }

    public static class CommandSerialization
    {
        public static IUndoCommand? FromRecord(CommandRecord r, Func<int, KitchenElement> resolve)
        {
            if (r == null) return null;
            switch (r.type)
            {
                case "move":
                {
                    var e = resolve(r.elementIndex);
                    if (e == null) return null;
                    return new MoveCommand(e,
                        CommandRecord.ToV3(r.posBefore), CommandRecord.ToV3(r.posAfter),
                        CommandRecord.ToQuat(r.rotBefore), CommandRecord.ToQuat(r.rotAfter));
                }
                case "resize":
                {
                    var e = resolve(r.elementIndex);
                    if (e == null) return null;
                    return new ResizeCommand(e,
                        CommandRecord.ToVI(r.dimsBefore), CommandRecord.ToVI(r.dimsAfter),
                        CommandRecord.ToV3(r.posBefore), CommandRecord.ToV3(r.posAfter),
                        CommandRecord.ToQuat(r.rotBefore), CommandRecord.ToQuat(r.rotAfter));
                }
                case "convert":
                {
                    var e = resolve(r.elementIndex);
                    if (e == null) return null;
                    return ConvertElementCommand.FromRecord(e, r);
                }
                case "composite":
                {
                    var children = new List<IUndoCommand>();
                    if (r.children != null)
                        foreach (var cr in r.children)
                        {
                            var c = FromRecord(cr, resolve);
                            if (c != null) children.Add(c);
                        }
                    return children.Count > 0 ? new CompositeCommand(r.description, children) : null;
                }
            }
            return null;
        }
    }
}
