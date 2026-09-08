using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SpecificationManagerTests
{
    private static KitchenElement CreateElement(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        return element;
    }

    [Test]
    public void SurfaceArea_800x400x18_Is0_6832()
    {
        float area = SpecificationManager.SurfaceAreaM2(new Vector3Int(800, 400, 18));
        Assert.AreEqual(0.6832f, area, 0.0001f);
    }

    [Test]
    public void Build_TwoIdenticalOneDifferent_TwoGroups()
    {
        var a = CreateElement("Board", new Vector3Int(800, 400, 18));
        var b = CreateElement("Board", new Vector3Int(800, 400, 18));
        var c = CreateElement("Board", new Vector3Int(600, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b, c });

        Assert.AreEqual(2, result.lines.Count);
        Assert.AreEqual(3, result.totalCount);
        Assert.AreEqual(2, result.lines[0].count);
        Assert.AreEqual(1, result.lines[1].count);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
        Object.DestroyImmediate(c.gameObject);
    }

    /// <summary>Имена элементов уникальны в рамках проекта (ElementNaming даёт
    /// «Bokovina», «Bokovina_1»), поэтому в ключ группировки имя не входит —
    /// иначе ведомость раскроя выродилась бы в строки по одной штуке.</summary>
    [Test]
    public void Build_DifferentNamesSameGeometry_OneGroup()
    {
        var a = CreateElement("Bokovina", new Vector3Int(600, 720, 18));
        var b = CreateElement("Bokovina_1", new Vector3Int(600, 720, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        Assert.AreEqual(1, result.lines.Count, "уникальные имена не должны дробить группу");
        Assert.AreEqual(2, result.lines[0].count);
        Assert.AreEqual("Bokovina", result.lines[0].name, "имя берётся от первой детали группы");

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void Build_EmptyList_ZeroBoardsZeroArea()
    {
        var result = SpecificationManager.Build(new List<KitchenElement>());
        Assert.AreEqual(0, result.lines.Count);
        Assert.AreEqual(0, result.totalCount);
        Assert.AreEqual(0f, result.totalAreaM2, 0.0001f);
    }

    /// <summary>D1: ведомость раскроя показывает площадь ПЛАСТИ (Ш×В по двум наибольшим
    /// измерениям), а не полную площадь поверхности бруска — кромка уже считается отдельно
    /// погонными метрами, торцы не должны входить в S ещё и через эту колонку. Для 800×400×18
    /// пласть даёт 0,32 м², полная поверхность (SurfaceAreaM2) дала бы 0,6832 м² — вдвое больше
    /// нужного, что и было дефектом.</summary>
    [Test]
    public void Build_TotalArea_SumsFaceAreaNotFullSurface()
    {
        var a = CreateElement("Board", new Vector3Int(800, 400, 18));
        var b = CreateElement("Board", new Vector3Int(800, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        Assert.AreEqual(2, result.totalCount);
        Assert.AreEqual(0.32f * 2f, result.totalAreaM2, 0.0001f,
            "площадь пласти (0,8×0,4), а не полная поверхность бруска (0,6832 на деталь)");

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    /// <summary>Реальный кейс D1: деталь может храниться повёрнутой в размерах — толщина не
    /// всегда третья координата. 552×720×18 и 18×720×552 — одна и та же деталь, площадь пласти
    /// обязана совпасть и не зависеть от порядка осей.</summary>
    [Test]
    public void Build_RotatedDimensions_SameFaceAreaRegardlessOfAxisOrder()
    {
        var normal = CreateElement("Board", new Vector3Int(552, 720, 18));
        var rotated = CreateElement("Board_1", new Vector3Int(18, 720, 552));

        var resultNormal = SpecificationManager.Build(new List<KitchenElement> { normal });
        var resultRotated = SpecificationManager.Build(new List<KitchenElement> { rotated });

        Assert.AreEqual(resultNormal.lines[0].areaPerBoardM2, resultRotated.lines[0].areaPerBoardM2, 0.0001f,
            "0,552×0,720 в обоих случаях — толщина 18 мм не должна попасть в множители");
        Assert.AreEqual(0.552f * 0.720f, resultNormal.lines[0].areaPerBoardM2, 0.0001f);

        Object.DestroyImmediate(normal.gameObject);
        Object.DestroyImmediate(rotated.gameObject);
    }

    /// <summary>552×720×18 ×9 — числа из дефекта D1: старая формула (полная поверхность бруска)
    /// давала 7,57 м², пласть даёт 3,58 м².</summary>
    [Test]
    public void Build_552x720x18_Times9_MatchesDefectNumbers()
    {
        var elements = new List<KitchenElement>();
        for (int i = 0; i < 9; i++)
            elements.Add(CreateElement($"Board_{i}", new Vector3Int(552, 720, 18)));

        var result = SpecificationManager.Build(elements);

        Assert.AreEqual(9, result.totalCount);
        Assert.AreEqual(3.58f, result.totalAreaM2, 0.005f, "новое число из ТЗ");
        Assert.AreNotEqual(7.57f, result.totalAreaM2, 0.05f, "старое (вдвое большее) число не должно вернуться");

        foreach (var e in elements) Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void ToCsv_TotalRow_CountAndAreaInCorrectColumns()
    {
        var a = CreateElement("Board", new Vector3Int(800, 400, 18));
        var b = CreateElement("Board", new Vector3Int(800, 400, 18));
        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        string csv = SpecificationExport.ToCsv(result);
        var rows = csv.Replace("\r\n", "\n").Trim().Split('\n');
        var header = rows[0].Split(';');
        var total = rows[rows.Length - 1].Split(';');

        Assert.AreEqual(header.Length, total.Length,
            "в итоговой строке столько же колонок, сколько в шапке");

        int countCol = System.Array.IndexOf(header, "Count");
        int areaCol = System.Array.IndexOf(header, "TotalArea_m2");
        Assert.AreEqual("2", total[countCol], "кол-во должно стоять в колонке Count");
        Assert.IsTrue(float.TryParse(total[areaCol], out float area), "площадь — число");
        Assert.AreEqual(0.32f * 2f, area, 0.001f, "площадь пласти в колонке TotalArea_m2, не полная поверхность");

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    /// <summary>D2: ведомость раскроя должна группироваться по материалу — деталей с разными
    /// декорами не должно быть вперемешку. Решение: строгая сортировка строк по имени материала
    /// (Ordinal), без дополнительной «умной» логики. Элементы добавлены в ОБРАТНОМ алфавиту
    /// порядке, чтобы тест мог упасть, если сортировка не сработает.</summary>
    [Test]
    public void Build_MixedMaterials_SortedAlphabeticallyByMaterial()
    {
        MaterialCatalog.Register(new MaterialDef("mat-z", "Ясень тёмный", "ЛДСП", Color.gray));
        MaterialCatalog.Register(new MaterialDef("mat-a", "Белый", "ЛДСП", Color.white));

        var darkWood = CreateElement("Board", new Vector3Int(800, 400, 18));
        darkWood.MaterialId = "mat-z";
        var white = CreateElement("Board_1", new Vector3Int(600, 400, 18));
        white.MaterialId = "mat-a";

        var result = SpecificationManager.Build(new List<KitchenElement> { darkWood, white });

        Assert.AreEqual(2, result.lines.Count);
        Assert.AreEqual("Белый", result.lines[0].material, "по алфавиту «Белый» раньше «Ясень тёмный»");
        Assert.AreEqual("Ясень тёмный", result.lines[1].material);

        Object.DestroyImmediate(darkWood.gameObject);
        Object.DestroyImmediate(white.gameObject);
        MaterialCatalog.Reset();
    }

    // Стабильность сортировки при одинаковом материале уже закрыта
    // Build_TwoIdenticalOneDifferent_TwoGroups выше — обе группы там из материала по
    // умолчанию, и порядок «2 шт, потом 1 шт» переживает сортировку по материалу без изменений.

    [Test]
    public void Build_ExcludesBasePlate()
    {
        var plate = CreateElement("BasePlate", new Vector3Int(3000, 18, 3000));
        plate.gameObject.AddComponent<BasePlate>();
        var board = CreateElement("Board", new Vector3Int(800, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { plate, board });

        Assert.AreEqual(1, result.lines.Count);
        Assert.AreEqual(1, result.totalCount);
        Assert.AreEqual("Board", result.lines[0].name);

        Object.DestroyImmediate(plate.gameObject);
        Object.DestroyImmediate(board.gameObject);
    }
}
