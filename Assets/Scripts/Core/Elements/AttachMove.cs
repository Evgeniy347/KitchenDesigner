using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// РЕДАКТИРУЮЩЕЕ перемещение прикреплённого поддерева: родителя сдвинули или
    /// повернули мышью, полями панели или через MCP — дети обязаны поехать за
    /// ним и попасть в ТУ ЖЕ запись отмены. (Анимация — другая история, её
    /// ведёт <see cref="AttachRider"/> и в стек отмены она не пишет.)
    ///
    /// Один помощник на все точки правки, а не копия математики в каждой:
    /// поворот вокруг родителя легко «почти правильно» повторить по-разному, и
    /// расхождение вылезло бы как разъехавшаяся сборка после Ctrl+Z.
    /// </summary>
    public static class AttachMove
    {
        /// <summary>Новая поза ребёнка при переносе родителя из позы «до» в позу
        /// «после»: жёсткая связка — ребёнок хранит смещение и разворот
        /// ОТНОСИТЕЛЬНО родителя.</summary>
        public static void Follow(Vector3 childPos, Quaternion childRot,
            Vector3 posBefore, Quaternion rotBefore, Vector3 posAfter, Quaternion rotAfter,
            out Vector3 newPos, out Quaternion newRot)
        {
            var delta = rotAfter * Quaternion.Inverse(rotBefore);
            newPos = posAfter + delta * (childPos - posBefore);
            newRot = delta * childRot;
        }

        /// <summary>Дополнить набор перемещаемых элементов их поддеревьями.
        /// Дубли не добавляются: в мультивыделении родитель и ребёнок вполне
        /// могут быть выбраны оба, и второй сдвиг увёз бы ребёнка вдвое.</summary>
        public static void ExpandWithDescendants(List<KitchenElement> set)
        {
            if (set == null || set.Count == 0) return;
            var seen = new HashSet<KitchenElement>(set);
            var buffer = new List<KitchenElement>();
            for (int i = 0; i < set.Count; i++)
            {
                buffer.Clear();
                AttachLinks.Descendants(set[i], buffer);
                foreach (var d in buffer)
                    if (d != null && seen.Add(d)) set.Add(d);
            }
        }

        /// <summary>Команды, увозящие поддерево <paramref name="root"/> вслед за
        /// его переносом. Команды НЕ выполняются — их выполняет вызывающий (там
        /// же, где и свою собственную), поэтому позы считаются от ТЕКУЩИХ
        /// (ещё не сдвинутых) поз детей.</summary>
        public static void AppendFollowers(List<IUndoCommand> commands, KitchenElement root,
            Vector3 posBefore, Quaternion rotBefore, Vector3 posAfter, Quaternion rotAfter,
            ICollection<KitchenElement>? skip = null)
        {
            if (commands == null || root == null) return;
            if ((posAfter - posBefore).sqrMagnitude <= Tolerance.EpsilonSqr
                && Quaternion.Angle(rotBefore, rotAfter) <= 0.01f) return;

            foreach (var child in AttachLinks.Descendants(root))
            {
                if (child == null || (skip != null && skip.Contains(child))) continue;
                // Поза покоя, а не трансформ: ребёнок мог в этот момент ехать за
                // открытым родителем, и сдвигать надо именно покой.
                var childPos = child.AttachRestPosition;
                var childRot = child.AttachRestRotation;
                Follow(childPos, childRot, posBefore, rotBefore, posAfter, rotAfter,
                    out var newPos, out var newRot);
                commands.Add(new MoveCommand(child, childPos, newPos, childRot, newRot));
            }
        }

        /// <summary>Та же операция одной командой (или null, если двигать
        /// нечего) — для точек, где вызывающий кладёт в стек ровно одну.</summary>
        public static IUndoCommand? FollowersCommand(KitchenElement root,
            Vector3 posBefore, Quaternion rotBefore, Vector3 posAfter, Quaternion rotAfter)
        {
            var cmds = new List<IUndoCommand>();
            AppendFollowers(cmds, root, posBefore, rotBefore, posAfter, rotAfter);
            if (cmds.Count == 0) return null;
            return cmds.Count == 1 ? cmds[0] : new CompositeCommand("Move attached", cmds);
        }
    }
}
