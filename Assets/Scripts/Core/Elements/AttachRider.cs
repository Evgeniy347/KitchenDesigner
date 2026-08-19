using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Возит прикреплённые детали за АНИМАЦИЕЙ родителя: фасад открывается —
    /// дно, стенки и боковины нестандартного ящика едут вместе с ним.
    ///
    /// Почему отдельный драйвер, а не Transform-иерархия Unity: деталь остаётся
    /// самостоятельным объектом сцены (свой масштаб в мм, свой снэп, свой
    /// ресайз), а родительский transform.localScale немедленно растянул бы
    /// детей вместе с фасадом. Здесь передаётся ТОЛЬКО поза.
    ///
    /// РЕДАКТИРУЮЩИЕ перемещения (мышь, поля панели, MCP) сюда не относятся: там
    /// у ребёнка меняется его собственная поза покоя, и это должно попасть в
    /// стек отмены — см. <see cref="ElementMover.BuildMoveSet"/> и
    /// <see cref="AttachMove"/>. Драйвер вступает, только когда родитель уехал
    /// со своей позы покоя, и возвращает детей на место, когда тот вернулся.
    /// </summary>
    public class AttachRider : MonoBehaviour
    {
        private void LateUpdate() => Step();

        // Карта «родитель → дети» строится заново на каждом шаге и живёт в
        // статических полях: шаг зовётся каждый кадр, и мусор от новых списков
        // на каждой детали был бы дороже самой работы.
        private static readonly Dictionary<string, List<KitchenElement>> _byParent =
            new Dictionary<string, List<KitchenElement>>();
        private static readonly List<KitchenElement> _roots = new List<KitchenElement>();

        /// <summary>Один шаг. Вынесен из LateUpdate по той же причине, что и
        /// <see cref="FacadeElement.StepDoor"/>: в EditMode-тестах кадров нет,
        /// и поведение проверяется прямым вызовом.</summary>
        public static void Step()
        {
            using var _ = PerfMarkers.AttachRiderStep.Auto();
            var all = PartRegistry.All;
            if (all == null || all.Count == 0) return;

            _byParent.Clear();
            bool anyLink = false;
            foreach (var e in all)
            {
                if (e == null || !AttachLinks.CanBeChild(e)) continue;
                var parentName = e.AttachedToName;
                if (string.IsNullOrEmpty(parentName)) continue;
                anyLink = true;
                if (!_byParent.TryGetValue(parentName, out var list))
                    _byParent[parentName] = list = new List<KitchenElement>();
                list.Add(e);
            }
            // Сцена без единой связи — а это обычная сцена — стоит ровно один
            // проход по реестру и ноль аллокаций.
            if (!anyLink) return;

            // Корни: у кого есть дети, а своего родителя нет. Обход идёт сверху
            // вниз, чтобы поза родителя была посчитана раньше, чем понадобится
            // детям. Дорогой поиск родителя по имени делается только для тех,
            // кто сам кому-то родитель, — их единицы.
            _roots.Clear();
            foreach (var e in all)
            {
                if (e == null || string.IsNullOrEmpty(e.PartName)) continue;
                if (!_byParent.ContainsKey(e.PartName)) continue;
                if (!string.IsNullOrEmpty(e.AttachedToName) && AttachLinks.Parent(e) != null) continue;
                _roots.Add(e);
            }
            foreach (var root in _roots) Drive(root, 0);
        }

        // Кольцо в связях (его можно принести файлом проекта) обязано
        // останавливать обход, а не вешать кадр.
        private const int MaxDepth = 16;

        private static void Drive(KitchenElement parent, int depth)
        {
            if (depth >= MaxDepth || parent == null) return;
            if (string.IsNullOrEmpty(parent.PartName)) return;
            if (!_byParent.TryGetValue(parent.PartName, out var children)) return;

            var restPos = AttachLinks.RestPosition(parent);
            var restRot = AttachLinks.RestRotation(parent);
            bool displaced = AttachLinks.IsDisplaced(parent);
            var delta = parent.transform.rotation * Quaternion.Inverse(restRot);

            foreach (var child in children)
            {
                if (child == null || child == parent) continue;
                if (displaced)
                {
                    // Первый кадр езды: трансформ ребёнка — это и есть его поза
                    // покоя, запоминаем её. Дальше он ездит от неё, а не от
                    // позиции прошлого кадра — иначе ошибка копилась бы.
                    if (!child.IsAttachRidden)
                        child.BeginAttachRide(child.transform.position, child.transform.rotation);
                    child.transform.SetPositionAndRotation(
                        parent.transform.position + delta * (child.AttachRestPosition - restPos),
                        delta * child.AttachRestRotation);
                }
                else if (child.IsAttachRidden)
                {
                    // Родитель вернулся в покой — возвращаем и ребёнка ровно
                    // туда, откуда его взяли.
                    child.transform.SetPositionAndRotation(child.AttachRestPosition, child.AttachRestRotation);
                    child.EndAttachRide();
                }
                Drive(child, depth + 1);
            }
        }
    }
}
