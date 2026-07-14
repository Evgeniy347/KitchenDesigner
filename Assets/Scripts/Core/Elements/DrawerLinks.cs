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
        /// Создать парный ящик к <paramref name="source"/>: второй короб того же
        /// типа ставится вплотную сверху (или снизу, если исходный — верхний),
        /// оба помечаются двойными и связываются по именам в обе стороны.
        /// Возвращает созданный ящик (null, если пара уже есть).
        /// </summary>
        public static DrawerElement? CreatePair(DrawerElement source)
        {
            if (source == null) return null;
            if (source.FindPaired() != null) return null; // пара уже существует

            bool newIsUpper = !source.IsUpperDrawer;
            // Шаг пары — высота контурного бокса (проёма): проёмы идут друг над другом.
            float heightUnits = DrawerConstants.GetMinOpeningHeight(source.Type) * AppConstants.MM_TO_UNITS;
            var pos = source.ClosedPosition + (newIsUpper ? Vector3.up : Vector3.down) * heightUnits;

            string pairName = UniqueName(source.PartName + (newIsUpper ? " (верх)" : " (низ)"));
            var go = ElementFactory.CreateDrawer(source.Type, source.NominalLength,
                source.Color, source.InternalWidth, pairName, pos);
            if (go == null) return null;
            go.transform.rotation = source.ClosedRotation;

            var pair = go.GetComponent<DrawerElement>();
            if (pair == null) return null;

            pair.IsDouble = true;
            pair.IsUpperDrawer = newIsUpper;
            pair.PairedDrawerName = source.PartName;

            source.IsDouble = true;
            source.IsUpperDrawer = !newIsUpper;
            source.PairedDrawerName = pair.PartName;
            return pair;
        }

        /// <summary>
        /// Переименовать элемент, обновив все ссылающиеся на старое имя связи:
        /// у пары ящика — PairedDrawerName, у ящиков с этим фасадом — AttachedFacadeName.
        /// </summary>
        public static void Rename(KitchenElement element, string newName)
        {
            if (element == null || string.IsNullOrEmpty(newName)) return;
            string oldName = element.PartName;
            if (oldName == newName) return;

            if (element is DrawerElement)
            {
                foreach (var e in PartRegistry.GetAll())
                    if (e is DrawerElement d && d != element && d.PairedDrawerName == oldName)
                        d.PairedDrawerName = newName;
            }
            else if (element is FacadeElement)
            {
                foreach (var e in PartRegistry.GetAll())
                    if (e is DrawerElement d && d.AttachedFacadeName == oldName)
                        d.AttachedFacadeName = newName;
            }

            element.PartName = newName;
        }

        /// <summary>Имя, свободное в PartRegistry (при коллизии — суффикс " 2", " 3"…).</summary>
        public static string UniqueName(string baseName)
        {
            if (!NameTaken(baseName)) return baseName;
            for (int i = 2; ; i++)
            {
                var candidate = baseName + " " + i;
                if (!NameTaken(candidate)) return candidate;
            }
        }

        private static bool NameTaken(string name)
        {
            foreach (var e in PartRegistry.GetAll())
                if (e != null && e.PartName == name) return true;
            return false;
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
                return ConstraintValidator.AreInFaceToFaceContact(drawer, facade);
            }
            finally
            {
                drawer.transform.position = savedPos;
                drawer.transform.rotation = savedRot;
            }
        }
    }
}
