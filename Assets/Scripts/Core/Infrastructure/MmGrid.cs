using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Инвариант миллиметровой сетки: ГРАНИ детали стоят на целых миллиметрах.
    ///
    /// Проверять надо именно грани, а не центр. Размер детали — целые мм
    /// (<see cref="KitchenElement.DimensionsMM"/> — Vector3Int), поэтому у детали
    /// нечётного габарита центр ОБЯЗАН лежать на половине миллиметра: доска
    /// 1967 мм, стоящая гранями на 1208 и 3175, имеет центр 2191.5. Это норма.
    /// Ошибка — когда на половине оказывается сама ГРАНЬ: в такой проём ни одна
    /// деталь целого размера уже не встанет, и разъехавшийся на 0.5 мм стык
    /// нельзя вылечить перемещением — деталь просто не того размера.
    ///
    /// Позиция живёт в float-метрах и свободна, а размер квантован — значит любое
    /// место, где в позицию попадает произвольное дробное число (центр стены как
    /// полусумма концов, деление пролёта в distribute_evenly, дельта модуля мимо
    /// округления), рождает дробные грани. Их и подрезает эта утилита.
    /// </summary>
    public static class MmGrid
    {
        /// <summary>Порог, ниже которого координата считается уже стоящей на
        /// сетке (0.01 мм). На порядок мельче самого мелкого реального
        /// отклонения и на два порядка крупнее точности float32 на координатах
        /// в десяток метров.</summary>
        public const float EpsMm = 0.01f;

        /// <summary>Округление до целых мм. Половина всегда идёт ВВЕРХ, а не
        /// «к чётному», как у Mathf.Round: иначе две одинаковые координаты .5
        /// уезжают в разные стороны (1208.5 вниз, 1207.5 вверх) и стык, который
        /// был на полмиллиметра, становится миллиметровым.
        ///
        /// Смещение на <see cref="EpsMm"/> — не косметика: ровной половины во
        /// float32 не существует. Координата 1.2005f — это 1192.49995 мм, и
        /// «половина вверх» без допуска молча превратилась бы в «вниз», а её
        /// близнец 1192.50004 ушёл бы вверх. Всё, что лежит в пределах допуска
        /// от половины, считается половиной и округляется в одну сторону.</summary>
        public static float RoundMm(float mm) => Mathf.Floor(mm + 0.5f + EpsMm);

        /// <summary>Поворот кратен 90°: каждая локальная ось смотрит вдоль
        /// мировой. У повёрнутых иначе деталей (радиусные полки, косые стены)
        /// «грань на мм-сетке» смысла не имеет — их не трогаем.</summary>
        public static bool IsAxisAligned(Quaternion rot)
        {
            var m = Matrix4x4.Rotate(rot);
            for (int col = 0; col < 3; col++)
            {
                var axis = m.GetColumn(col);
                float max = Mathf.Max(Mathf.Abs(axis.x), Mathf.Max(Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
                if (max < 0.9999f) return false;
            }
            return true;
        }

        /// <summary>Сдвиг, который поставит ближнюю грань детали по каждой мировой
        /// оси на целый миллиметр. Vector3.zero — деталь уже на сетке либо её
        /// трогать нельзя.
        ///
        /// Считается по РЕАЛЬНЫМ мировым вершинам, а не по DimensionsMM: у фасада
        /// габарит расширен технологическими зазорами (EffectiveScale), и сетку
        /// обязана держать та коробка, которую видят валидация и прилипание.</summary>
        public static Vector3 OffsetToGrid(KitchenElement element)
        {
            if (element == null) return Vector3.zero;

            // Открытая дверца и выдвинутый ящик строят коробку от закрытой позы —
            // запись в transform.position её не двигает (см. PoseFollowsTransform).
            if (!element.PoseFollowsTransform) return Vector3.zero;
            if (!IsAxisAligned(element.transform.rotation)) return Vector3.zero;

            // Полускрытая стена временно опущена ради обзора: её вершины считаются
            // от ПОЛНОЙ геометрии, и правка позиции по ним увела бы стену.
            var wall = element.GetComponent<Wall>();
            if (wall != null && wall.IsLowered) return Vector3.zero;

            var verts = element.GetVertices();
            if (verts == null || verts.Length == 0) return Vector3.zero;

            Vector3 min = verts[0];
            foreach (var v in verts) min = Vector3.Min(min, v);

            float toMm = 1f / AppConstants.MM_TO_UNITS;
            var offset = Vector3.zero;
            bool any = false;
            for (int axis = 0; axis < 3; axis++)
            {
                float mm = min[axis] * toMm;
                float deltaMm = RoundMm(mm) - mm;
                if (Mathf.Abs(deltaMm) <= EpsMm) continue;
                offset[axis] = deltaMm * AppConstants.MM_TO_UNITS;
                any = true;
            }
            return any ? offset : Vector3.zero;
        }

        /// <summary>Поставить грани детали на целые миллиметры. true — деталь
        /// сдвинулась. Размер не меняется: если деталь не того размера для своего
        /// проёма, сетка это не лечит — лечит правка габарита.</summary>
        public static bool Snap(KitchenElement element)
        {
            var offset = OffsetToGrid(element);
            if (offset == Vector3.zero) return false;
            element.transform.position += offset;
            return true;
        }

        /// <summary>Выровненный вариант ЕЩЁ НЕ ПРИМЕНЁННОЙ позиции — для операций,
        /// которые считают координату заранее и кладут её в undo-команду (перенос
        /// выборки, выравнивание, растяжение модуля). Снимок вершин зависит от
        /// трансформа, поэтому позиция примеряется и тут же возвращается назад.</summary>
        public static Vector3 SnapPosition(KitchenElement element, Vector3 desired)
        {
            if (element == null) return desired;
            var prev = element.transform.position;
            element.transform.position = desired;
            var offset = OffsetToGrid(element);
            element.transform.position = prev;
            return desired + offset;
        }
    }
}
