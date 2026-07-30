namespace KitchenDesigner.Core
{
    /// <summary>Врезная техника, прорезающая проём в детали-столешнице: мойка и
    /// варочная поверхность. Список проёмов ведёт сама ДЕТАЛЬ (см.
    /// <see cref="KitchenElement.RegisterCutout"/>), поэтому ей нужен один общий
    /// интерфейс, а не проверки на конкретный класс.
    ///
    /// Реализуют MonoBehaviour-ы, и ссылка на уничтоженный объект здесь НЕ
    /// становится «фейковым null» (это работает только для типов Unity) —
    /// сравнивать надо через <c>as UnityEngine.Object</c>.</summary>
    public interface IPartCutout
    {
        /// <summary>Имя врезного элемента — для диагностики и сравнения.</summary>
        string PartName { get; }

        /// <summary>Проём в нормализованных координатах той плоскости, в которой
        /// его режет <see cref="GrooveMesh"/> (оси задаёт <see cref="HoleAxisIn"/>).
        /// Доли считаются от ТЕКУЩИХ габаритов детали, поэтому её ресайз двигает
        /// и перемасштабирует вырез сам.</summary>
        GrooveMesh.Rect2 CutoutRectIn(KitchenElement part);

        /// <summary>Локальная ось детали, ПОПЕРЁК которой режется проём (та, что
        /// смотрит вверх).</summary>
        int HoleAxisIn(KitchenElement part);
    }
}
