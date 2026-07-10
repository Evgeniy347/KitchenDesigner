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
        CommandRecord ToRecord(Func<KitchenElement, int> indexOf);
    }

    /// <summary>Сериализуемая запись одной команды undo/redo. Плоская структура с
    /// тегом <see cref="type"/> вместо наследников — JsonUtility не умеет
    /// полиморфные массивы. elementIndex ссылается на объект по позиции в
    /// ProjectData.elements (стабильна в пределах одного сохранения).</summary>
    [Serializable]
    public class CommandRecord
    {
        public string type;          // move | resize | composite
        public string description;
        public int elementIndex = -1;
        public float[] posBefore;
        public float[] posAfter;
        public float[] rotBefore;
        public float[] rotAfter;
        public int[] dimsBefore;
        public int[] dimsAfter;
        public CommandRecord[] children;

        public static float[] V3(Vector3 v) => new[] { v.x, v.y, v.z };
        public static float[] V4(Quaternion q) => new[] { q.x, q.y, q.z, q.w };
        public static int[] VI(Vector3Int v) => new[] { v.x, v.y, v.z };

        public static Vector3 ToV3(float[] a) =>
            a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
        public static Quaternion ToQuat(float[] a) =>
            a != null && a.Length >= 4 ? new Quaternion(a[0], a[1], a[2], a[3]) : Quaternion.identity;
        public static Vector3Int ToVI(int[] a) =>
            a != null && a.Length >= 3 ? new Vector3Int(a[0], a[1], a[2]) : Vector3Int.one;
    }

    /// <summary>Восстановление команд из записей при загрузке проекта. resolve
    /// отдаёт объект по индексу (или null — тогда команда пропускается).</summary>
    public static class CommandSerialization
    {
        public static IUndoCommand FromRecord(CommandRecord r, Func<int, KitchenElement> resolve)
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
