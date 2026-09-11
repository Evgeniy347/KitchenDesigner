using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сцена и отпечаток результата для тестов инкрементальной валидации.
/// Живёт отдельным типом, а не копией в каждом файле: два описания одной сцены
/// разъезжаются, и тогда «совпало» и «не совпало» начинают значить разное в
/// соседних тестах.</summary>
public static class ValidationTestScene
{
    public const float MM = 0.001f;

    public static ValidationElement Part(string name, Vector3 centerMm, Vector3 sizeMm,
        ElementKind kind = ElementKind.None)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        var geometry = ElementGeometry.Box(name, center, size);
        return new ValidationElement(geometry, Corners(center, size), kind, 0, null,
            Span.FromCenter(center.y, size.y), ValidationElement.NoIndex);
    }

    private static Vector3[] Corners(Vector3 center, Vector3 size)
    {
        var half = size * 0.5f;
        var verts = new Vector3[8];
        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    verts[i++] = center + new Vector3(half.x * sx, half.y * sy, half.z * sz);
        return verts;
    }

    /// <summary>Кухня из <paramref name="modules"/> модулей по пять деталей плюс
    /// пол-якорь: ряды по 20 модулей, ряды в 700 мм друг от друга — соседние
    /// корпуса почти касаются, как в настоящей плотной кухне. Плюс висящая
    /// стопка из двух досок, которую подпереть можно только мувером: без него
    /// она неподпёрта, с ним — встаёт на место.</summary>
    public static List<ValidationElement> Kitchen(int modules)
    {
        var parts = new List<ValidationElement>
        {
            Part("Floor", new Vector3(6000, -9, 1000), new Vector3(20000, 18, 6000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
        };

        for (int m = 0; m < modules; m++)
        {
            float x0 = (m % 20) * 600f;
            float z0 = (m / 20) * 700f;
            parts.Add(Part($"M{m}_SideL", new Vector3(x0 + 9, 360, z0), new Vector3(18, 720, 560)));
            parts.Add(Part($"M{m}_SideR", new Vector3(x0 + 591, 360, z0), new Vector3(18, 720, 560)));
            parts.Add(Part($"M{m}_Bottom", new Vector3(x0 + 300, 9, z0), new Vector3(564, 18, 560)));
            parts.Add(Part($"M{m}_Shelf", new Vector3(x0 + 300, 400, z0), new Vector3(564, 18, 560)));
            parts.Add(Part($"M{m}_Top", new Vector3(x0 + 300, 739, z0), new Vector3(600, 38, 600)));
        }

        parts.Add(Part("Hanging_Shelf", new Vector3(1200, 1009, 0), new Vector3(400, 18, 400)));
        parts.Add(Part("Hanging_Stack", new Vector3(1200, 1027, 0), new Vector3(400, 18, 400)));
        return parts;
    }

    public static ValidationElement Mover(string name, Vector3 centerMm) =>
        Part(name, centerMm, new Vector3(18, 242, 400));

    /// <summary>Позы, через которые жест протаскивает мувер: воздух, опора под
    /// висящей стопкой, внесение в столешницу, в боковину, в пол, отход в
    /// сторону. Каждая обязана дать тот же ответ, что и полный проход.</summary>
    public static IEnumerable<Vector3> GesturePoses()
    {
        yield return new Vector3(4000, 2000, 2500);
        yield return new Vector3(1200, 879, 0);
        yield return new Vector3(1200, 739, 0);
        yield return new Vector3(609, 360, 0);
        yield return new Vector3(1500, 100, 0);
        yield return new Vector3(1700, 879, 0);
        yield return new Vector3(1200, 939, 0);
        yield return new Vector3(3000, 121, 700);
        yield return new Vector3(6000, 360, 1400);
        yield return new Vector3(300, 400, 0);
    }

    /// <summary>Отпечаток ВСЕГО результата: нарушения, диагностики, контакты,
    /// изолированные группы, флаг валидности. Порядок внутри списков
    /// каноникализуется, и это не послабление: полный проход укладывает
    /// нарушение в момент обработки пары, инкрементальный — сначала
    /// замороженные, потом мувера, а СОСТАВ обязан совпасть до элемента. Ни один
    /// потребитель (<c>ElementHighlighter</c>, <c>SceneViolations</c>,
    /// <c>EditGate</c>) порядок не читает — все три спрашивают «есть ли эта
    /// деталь с этим кодом».</summary>
    public static string Fingerprint(CoreValidationResult r, IReadOnlyList<ValidationElement> all)
    {
        var violations = r.Violations.Select(i => all[i].Name).Distinct()
            .OrderBy(s => s, System.StringComparer.Ordinal);

        var diagnostics = (r.Diagnostics ?? new List<CoreViolation>())
            .Select(d => $"{all[d.Element].Name}|" +
                         $"{(d.Other >= 0 ? all[d.Other].Name : "-")}|{d.Kind}")
            .OrderBy(s => s, System.StringComparer.Ordinal);

        var contacts = r.Contacts
            .Select(c => $"{all[c.A].Name}|{all[c.B].Name}|{c.FaceA}|{c.FaceB}|" +
                         $"{c.Area.ToString("F9", System.Globalization.CultureInfo.InvariantCulture)}|" +
                         $"{c.IsFaceToFace}")
            .OrderBy(s => s, System.StringComparer.Ordinal);

        var groups = r.IsolatedGroups
            .Select(g => string.Join("+", g.Select(i => all[i].Name)
                .OrderBy(s => s, System.StringComparer.Ordinal)))
            .OrderBy(s => s, System.StringComparer.Ordinal);

        return $"valid={r.IsValid}\n" +
               $"violations[{r.Violations.Count}]:\n  {string.Join("\n  ", violations)}\n" +
               $"diagnostics:\n  {string.Join("\n  ", diagnostics)}\n" +
               $"groups:\n  {string.Join("\n  ", groups)}\n" +
               $"contacts:\n  {string.Join("\n  ", contacts)}";
    }

    public static string FullFingerprint(List<ValidationElement> parts) =>
        Fingerprint(ValidationCore.Validate(parts), parts);
}
