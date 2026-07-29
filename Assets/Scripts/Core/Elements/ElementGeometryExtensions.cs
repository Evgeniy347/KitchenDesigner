using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Мост между сценой и ядром: превращает деталь в
    /// <see cref="ElementGeometry"/>. Всё, что знает про MonoBehaviour, пазы и
    /// подтипы, остаётся ЗДЕСЬ — ядро получает голую геометрию.
    ///
    /// Снимок берётся один раз на расчёт. Двигать деталь ради того, чтобы
    /// прочитать её грани в другой позиции, больше не нужно: для этого есть
    /// перегрузка с явной позицией.</summary>
    public static class ElementGeometryExtensions
    {
        /// <summary>Снимок в ТЕКУЩЕЙ позе детали.</summary>
        public static ElementGeometry ToGeometry(this KitchenElement element)
        {
            if (element == null) return default;

            var faces = FaceCache.GetFaces(element);
            ElementGeometry.BoundsOf(element.GetVertices(), out var min, out var max);

            return new ElementGeometry(
                element.GetInstanceID(),
                element.PartName,
                faces,
                element.GetGrooveSeatFaces(),
                element.GetGrooveWallFaces(),
                min, max,
                element is PanelElement);
        }

        /// <summary>Снимок для ГИПОТЕТИЧЕСКОЙ позиции — деталь при этом не
        /// двигается. Ради этого и заведены GetFacesAt/GetVerticesAt: раньше
        /// примерка шла через запись в transform.position с возвратом назад,
        /// а каждая такая запись грязнит поддерево трансформов, и пересчёт
        /// оплачивал тот, кто следующим читал геометрию.
        ///
        /// Кэш граней здесь неприменим: он хранит грани в ТЕКУЩЕЙ позе.</summary>
        public static ElementGeometry ToGeometryAt(this KitchenElement element, Vector3 position)
        {
            if (element == null) return default;

            ElementGeometry.BoundsOf(element.GetVerticesAt(position), out var min, out var max);

            return new ElementGeometry(
                element.GetInstanceID(),
                element.PartName,
                element.GetFacesAt(position),
                element.GetGrooveSeatFacesAt(position),
                element.GetGrooveWallFacesAt(position),
                min, max,
                element is PanelElement);
        }

        /// <summary>Снимки набора деталей. Неактивные отсеиваются здесь: ядро
        /// сцены не видит и об «выключенности» знать не может.</summary>
        public static List<ElementGeometry> ToGeometry(this IEnumerable<KitchenElement> elements)
        {
            var result = new List<ElementGeometry>();
            if (elements == null) return result;

            foreach (var e in elements)
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                result.Add(e.ToGeometry());
            }
            return result;
        }
    }
}
