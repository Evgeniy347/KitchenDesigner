using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Снимок детали С ПАЗОМ для тестов ядра. В сцене дно и стенки паза
/// строит <c>KitchenElement.GetGrooveSeatFacesAt/GetGrooveWallFacesAt</c> из
/// списка пазов; здесь та же геометрия задаётся числами напрямую — ядру всё
/// равно, откуда пришли грани, а тест читается без разбора GrooveMesh.
///
/// Соглашения повторяют сценовые:
/// • паз режется в пласти <b>+Z</b>; дно — обычная грань с нормалью +Z,
///   поднятая внутрь детали на глубину паза;
/// • стенок две, обе с ОДНОЙ нормалью (плоскость двусторонняя, снэп это знает),
///   их <c>rightAxis</c> — вдоль длины паза, размер = длина × глубина.</summary>
public static class GroovedGeometry
{
    private const float MM = 0.001f;

    /// <summary>Деталь с одним пазом. Всё в миллиметрах и относительно центра
    /// детали: <paramref name="fromMm"/>..<paramref name="toMm"/> — положение
    /// стенок паза поперёк его длины.</summary>
    public static ElementGeometry Board(string name, Vector3 centerMm, Vector3 sizeMm,
        float depthMm, float fromMm, float toMm, bool alongX = true, bool isPanel = false)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        var box = ElementGeometry.Box(name, center, size, isPanel);

        float depth = depthMm * MM;
        float from = fromMm * MM;
        float to = toMm * MM;
        float halfZ = size.z * 0.5f;
        float seatZ = center.z + halfZ - depth;
        float wallZ = center.z + halfZ - depth * 0.5f;

        Face seat;
        Face[] walls;
        if (alongX)
        {
            // Паз идёт вдоль X, стенки перпендикулярны Y.
            seat = new Face(new Vector3(center.x, center.y + (from + to) * 0.5f, seatZ),
                Vector3.forward, new Vector2(size.x, to - from), Vector3.right, Vector3.up);
            walls = new[]
            {
                new Face(new Vector3(center.x, center.y + from, wallZ), Vector3.up,
                    new Vector2(size.x, depth), Vector3.right, Vector3.forward),
                new Face(new Vector3(center.x, center.y + to, wallZ), Vector3.up,
                    new Vector2(size.x, depth), Vector3.right, Vector3.forward),
            };
        }
        else
        {
            // Паз идёт вдоль Y, стенки перпендикулярны X.
            seat = new Face(new Vector3(center.x + (from + to) * 0.5f, center.y, seatZ),
                Vector3.forward, new Vector2(to - from, size.y), Vector3.right, Vector3.up);
            walls = new[]
            {
                new Face(new Vector3(center.x + from, center.y, wallZ), Vector3.right,
                    new Vector2(size.y, depth), Vector3.up, Vector3.forward),
                new Face(new Vector3(center.x + to, center.y, wallZ), Vector3.right,
                    new Vector2(size.y, depth), Vector3.up, Vector3.forward),
            };
        }

        return new ElementGeometry(box.Id, box.Name, box.Faces, new[] { seat }, walls,
            box.Min, box.Max, box.IsPanel);
    }
}
