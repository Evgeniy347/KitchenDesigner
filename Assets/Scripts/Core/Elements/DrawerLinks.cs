using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Связи ящика держатся на именах (PairedDrawerName / AttachedFacadeName) —
    /// здесь собраны операции, которые обязаны сохранять их целостность:
    /// создание парного ящика и переименование с обновлением обратных ссылок.
    /// </summary>
    public static class DrawerLinks
    {
        /// <summary>
        /// Сделать ящик двойным: над <paramref name="source"/> создаётся верхний
        /// внутренний ящик (всегда тип A) — вплотную над контуром нижнего, той же
        /// ширины и цвета, той же длины. Верхний жёстко привязан к нижнему
        /// (следует за ним, своих свойств кроме L не имеет) и заблокирован от
        /// перемещения. Возвращает верхний ящик (null, если source — верхний
        /// или пара уже есть).
        /// </summary>
        public static DrawerElement? CreatePair(DrawerElement source)
        {
            if (source == null || source.IsUpperDrawer) return null;
            if (source.FindPaired() != null) return null; // пара уже существует

            var upperType = DrawerConstants.UPPER_DRAWER_TYPE;
            // Контуры проёмов идут друг над другом: шаг = полусумма высот контуров.
            float step = (DrawerConstants.GetMinOpeningHeight(source.Type)
                        + DrawerConstants.GetMinOpeningHeight(upperType)) * 0.5f * AppConstants.MM_TO_UNITS;
            var pos = source.ClosedPosition + source.ClosedRotation * Vector3.up * step;

            string pairName = UniqueName(source.PartName + "_top");
            var go = ElementFactory.CreateDrawer(upperType, source.NominalLength,
                source.Color, source.InternalWidth, pairName, pos, source.System);
            if (go == null) return null;
            go.transform.rotation = source.ClosedRotation;

            var pair = go.GetComponent<DrawerElement>();
            if (pair == null) return null;

            pair.IsDouble = true;
            pair.IsUpperDrawer = true;
            pair.PairedDrawerName = source.PartName;
            pair.Movable = false; // двигается только вместе с нижним

            source.IsDouble = true;
            source.IsUpperDrawer = false;
            source.PairedDrawerName = pair.PartName;
            return pair;
        }

        /// <summary>Убрать верхний ящик пары и снять с нижнего пометку двойного.
        /// Возвращает GameObject верхнего (удаление — на вызывающем, чтобы он
        /// мог провести его через CommandStack).</summary>
        public static GameObject? DetachPair(DrawerElement source)
        {
            if (source == null || source.IsUpperDrawer) return null;
            var pair = source.FindPaired();

            source.IsDouble = false;
            source.PairedDrawerName = "";
            if (pair == null) return null;

            pair.IsDouble = false;
            pair.PairedDrawerName = "";
            return pair.gameObject;
        }

        /// <summary>
        /// Переименовать элемент, обновив все ссылающиеся на старое имя связи:
        /// у пары ящика — PairedDrawerName, у ящиков с этим фасадом — AttachedFacadeName.
        /// Имя приводится к допустимому алфавиту и делается уникальным (ElementNaming),
        /// поэтому фактическое имя может отличаться от запрошенного — обратные ссылки
        /// проставляются уже по нему.
        /// </summary>
        public static void Rename(KitchenElement element, string newName)
        {
            if (element == null || string.IsNullOrEmpty(newName)) return;
            newName = ElementNaming.Normalize(newName, element);
            string oldName = element.PartName;
            if (oldName == newName) return;

            if (element is DrawerElement)
            {
                foreach (var e in PartRegistry.All)
                    if (e is DrawerElement d && d != element && d.PairedDrawerName == oldName)
                        d.PairedDrawerName = newName;
            }
            else if (element is FacadeElement)
            {
                // Хозяев фасада двое — ящик и посудомоечная машина; спрашиваем
                // их одним интерфейсом, иначе третий тип пришлось бы дописывать
                // сюда отдельной веткой и однажды бы забыли.
                foreach (var e in PartRegistry.All)
                    if (e is IFacadeHost host && host.AttachedFacadeName == oldName)
                        host.AttachedFacadeName = newName;
            }

            element.PartName = newName;
        }

        /// <summary>Имя, свободное в PartRegistry и приведённое к допустимому
        /// алфавиту (при коллизии — суффикс «_1», «_2»…). См. ElementNaming.</summary>
        public static string UniqueName(string baseName) => ElementNaming.Normalize(baseName);

        /// <summary>Фасад НАВЕШЕН на своего хозяина (<see cref="IFacadeHost"/>).
        /// У ящика проверка идёт по ЗАКРЫТОЙ позе (перегрузка ниже), у
        /// неподвижного хозяина — по текущей: посудомойка не выдвигается, и
        /// подменять ей позу нечем.
        ///
        /// Порог берётся у самого хозяина (<see cref="IFacadeHost.FacadeMountGapMm"/>):
        /// фронт ящика прикручен заподлицо и требует прямого контакта, фасад
        /// посудомойки висит на кронштейнах с монтажным зазором. Общего «допуска
        /// касания» на оба случая нет и быть не может — это разный крепёж.</summary>
        public static bool IsFacadeInContact(IFacadeHost host, FacadeElement facade)
        {
            if (host == null || facade == null) return false;
            if (host is DrawerElement drawer) return IsFacadeInContact(drawer, facade);
            if (!(host is KitchenElement element)) return false;
            return ConstraintValidator.AreFacadeMountable(element, facade, host.FacadeMountGapMm);
        }

        public static bool IsFacadeInContact(DrawerElement drawer, FacadeElement facade)
        {
            if (drawer == null || facade == null) return false;
            var savedPos = drawer.transform.position;
            var savedRot = drawer.transform.rotation;
            try
            {
                drawer.transform.position = drawer.ClosedPosition;
                drawer.transform.rotation = drawer.ClosedRotation;
                // FacadeMountGapMm ящика — ноль, так что это ровно прежний
                // строгий контакт; путь один и тот же на обоих хозяев, чтобы
                // ослабление допуска нельзя было внести только в одну ветку.
                return ConstraintValidator.AreFacadeMountable(drawer, facade,
                    drawer.FacadeMountGapMm);
            }
            finally
            {
                drawer.transform.position = savedPos;
                drawer.transform.rotation = savedRot;
            }
        }
    }
}
