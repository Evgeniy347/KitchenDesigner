using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Команда, которую можно сохранить в историю проекта. indexOf отдаёт
    /// индекс целевого объекта в ProjectData.elements (или -1, если объект не
    /// попадает в сохранение — тогда команда в историю не пишется).</summary>
    public interface ISerializableCommand
    {
        CommandRecord? ToRecord(Func<KitchenElement, int> indexOf);
    }

    /// <summary>Восстановление команд из записей при загрузке проекта. resolve
    /// отдаёт объект по индексу (или null — тогда команда пропускается).</summary>
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
