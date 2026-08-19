using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// ПРИКРЕПЛЕНИЕ дощечек: деталь можно объявить дочерней к другой детали или
    /// к фасаду, и дальше она едет за родителем — и при перетаскивании, и при
    /// открывании фасада. Ради этого механика и существует: нестандартный ящик
    /// собирают из обычных деталей («фасад ← дно ← задняя стенка, боковины»), и
    /// без связи анимация открывания уносила бы один фасад, оставив короб.
    ///
    /// Связь держится на ИМЕНИ родителя (<see cref="KitchenElement.AttachedToName"/>)
    /// — ровно как <see cref="IFacadeHost.AttachedFacadeName"/>: переживает
    /// сохранение, чинится при переименовании (<see cref="DrawerLinks.Rename"/>),
    /// у удалённого родителя просто не находится, и деталь молча отцепляется.
    ///
    /// Направление связи одностороннее: родитель тащит детей, ребёнок родителя —
    /// никогда. Ресайз не передаётся вовсе (деталь меняет свой габарит, а не
    /// габарит поддерева).
    /// </summary>
    public static class AttachLinks
    {
        /// <summary>Этот элемент можно прикрепить к родителю: СТАТИЧНАЯ деталь —
        /// дощечка, ДВП-панель, полка, столешница, опора. Всё, у чего есть
        /// СВОЯ кинематика или свой хозяин, — нет:
        ///   • фасад, ящик, посудомойка, духовка, окно, дверь — открываются сами
        ///     и уже кем-то ведомы (фасад машины — вовсе пассажир);
        ///   • мойка и варочная — врезаны в деталь и ходят за ней (SnapToPart);
        ///   • стена, пол, светильник — не части сборки.
        /// Два хозяина у одного трансформа — это разъезжающаяся поза, а не
        /// удобство, поэтому список закрытый.</summary>
        public static bool CanBeChild(KitchenElement? e) =>
            e != null
            && !(e is FacadeElement) && !(e is DrawerElement) && !(e is DishwasherElement)
            && !(e is OvenElement) && !(e is WindowElement) && !(e is DoorElement)
            && !(e is IPartCutout) && !(e is FloorElement) && !(e is LightSourceElement)
            && e.GetComponent<Wall>() == null && e.GetComponent<BasePlate>() == null;

        /// <summary>К этому элементу можно прикреплять: всё, что годится в дети,
        /// плюс ФАСАД. Фасад — ради того и затевалось: он и есть лицо
        /// нестандартного ящика, и его открывание должно везти короб.</summary>
        public static bool CanBeParent(KitchenElement? e) =>
            e != null && (CanBeChild(e) || e is FacadeElement);

        /// <summary>Родитель по имени, либо null (имя пусто, родителя удалили,
        /// он больше не годится в родители). Отсутствие родителя — не ошибка:
        /// удаление родителя ОТЦЕПЛЯЕТ детей, а undo удаления возвращает связь.</summary>
        public static KitchenElement? Parent(KitchenElement? e)
        {
            if (e == null || string.IsNullOrEmpty(e.AttachedToName)) return null;
            foreach (var other in PartRegistry.All)
                if (other != null && other != e && other.PartName == e.AttachedToName)
                    return CanBeParent(other) ? other : null;
            return null;
        }

        /// <summary>Прямые дети элемента (порядок — как в реестре).</summary>
        public static void Children(KitchenElement? parent, List<KitchenElement> into)
        {
            if (parent == null || string.IsNullOrEmpty(parent.PartName)) return;
            foreach (var e in PartRegistry.All)
                if (e != null && e != parent && CanBeChild(e) && e.AttachedToName == parent.PartName)
                    into.Add(e);
        }

        public static List<KitchenElement> Children(KitchenElement? parent)
        {
            var list = new List<KitchenElement>();
            Children(parent, list);
            return list;
        }

        /// <summary>Всё поддерево БЕЗ самого корня, родители раньше детей.
        /// Обход помечает посещённых: битая связь-петля (её можно принести из
        /// файла проекта) обязана останавливать обход, а не вешать кадр.</summary>
        public static void Descendants(KitchenElement? root, List<KitchenElement> into)
        {
            if (root == null) return;
            var seen = new HashSet<KitchenElement> { root };
            var queue = new Queue<KitchenElement>();
            queue.Enqueue(root);
            var buffer = new List<KitchenElement>();
            while (queue.Count > 0)
            {
                buffer.Clear();
                Children(queue.Dequeue(), buffer);
                foreach (var child in buffer)
                {
                    if (!seen.Add(child)) continue;
                    into.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        public static List<KitchenElement> Descendants(KitchenElement? root)
        {
            var list = new List<KitchenElement>();
            Descendants(root, list);
            return list;
        }

        /// <summary>Прикрепление создало бы цикл: <paramref name="parent"/> сам
        /// лежит в поддереве <paramref name="child"/> (или это он и есть).</summary>
        public static bool WouldCycle(KitchenElement child, KitchenElement parent)
        {
            if (child == null || parent == null) return false;
            if (child == parent) return true;
            foreach (var d in Descendants(child))
                if (d == parent) return true;
            return false;
        }

        /// <summary>Прикрепление допустимо: роли подходят, цикла нет.
        /// КОНТАКТ здесь НЕ проверяется — он проверяется при подборе кандидатов
        /// в списке и потом сторожится анализатором (ATT-01): деталь вправе
        /// разъехаться с родителем, это дефект сборки, а не запрет на связь.</summary>
        public static bool CanAttach(KitchenElement? child, KitchenElement? parent) =>
            CanBeChild(child) && CanBeParent(parent) && !WouldCycle(child!, parent!);

        /// <summary>Деталь и родитель стоят ВПЛОТНУЮ (грань к грани). Меряется по
        /// позам покоя — у открытого фасада это закрытая поза, иначе всякое
        /// открывание объявляло бы сборку разъехавшейся.</summary>
        public static bool InContact(KitchenElement? child, KitchenElement? parent)
        {
            if (child == null || parent == null) return false;
            return ConstraintValidator.AreInFaceToFaceContact(child, parent);
        }

        /// <summary>Между деталью и её родителем появился зазор — связь есть,
        /// контакта нет. Родителя не нашли (удалён) — не разрыв: деталь
        /// отцеплена.</summary>
        public static bool IsDetached(KitchenElement? child)
        {
            var parent = Parent(child);
            return parent != null && !InContact(child, parent);
        }

        // ── Поза покоя и езда за родителем ──────────────────────────────
        // «Покой» — логическая поза, в которой деталь стоит, когда ничего не
        // анимируется: у фасада это ЗАКРЫТАЯ поза (у открытого трансформ уехал
        // петлёй), у обычной детали — её собственный трансформ, пока она сама не
        // едет за кем-то.

        public static Vector3 RestPosition(KitchenElement e) =>
            e is FacadeElement f ? f.ClosedPosition
            : e is DrawerElement d ? d.ClosedPosition
            : e.AttachRestPosition;

        public static Quaternion RestRotation(KitchenElement e) =>
            e is FacadeElement f ? f.ClosedRotation
            : e is DrawerElement d ? d.ClosedRotation
            : e.AttachRestRotation;

        /// <summary>Вернуть деталь в позу покоя, захлопнув анимированных
        /// предков (фасад, ящик). Зовётся ПЕРЕД правкой позиции/поворота: у
        /// едущей детали трансформом владеет <see cref="AttachRider"/>, и
        /// запись в него всё равно была бы затёрта следующим кадром — а в стек
        /// отмены при этом легла бы поза из середины анимации.
        ///
        /// Тот же приём, что окно свойств уже применяет к самому фасаду
        /// (ForceClose перед применением полей); здесь он лишь поднимается по
        /// цепочке прикрепления.</summary>
        public static void ForceRest(KitchenElement? e)
        {
            var parent = Parent(e);
            for (int guard = 0; parent != null && guard < 16; guard++)
            {
                if (parent is FacadeElement facade) facade.ForceClose();
                else if (parent is DrawerElement drawer) drawer.ForceClose();
                parent = Parent(parent);
            }
            AttachRider.Step();
        }

        /// <summary>Элемент СДВИНУТ со своей позы покоя (открывается фасад,
        /// выдвигается ящик, едет за своим родителем) — значит детей надо везти.</summary>
        public static bool IsDisplaced(KitchenElement e)
        {
            if (e == null) return false;
            var restPos = RestPosition(e);
            var restRot = RestRotation(e);
            return (e.transform.position - restPos).sqrMagnitude > Tolerance.EpsilonSqr
                || Quaternion.Angle(e.transform.rotation, restRot) > 0.01f;
        }
    }
}
