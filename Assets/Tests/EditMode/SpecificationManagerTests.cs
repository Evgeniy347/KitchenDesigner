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
        Assert.That(result.totalAreaM2, Is.Not.EqualTo(7.57f).Within(0.05f),
            "старое (вдвое большее) число не должно вернуться");

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
        var totalRow = System.Array.Find(rows, r => r.StartsWith("Total;"));
        Assert.IsNotNull(totalRow, "в CSV есть строка Total");
        var total = totalRow!.Split(';');

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

    // ── IQuantifies: обобщённая строка спецификации, тест-элемент, продакшн не трогаем ──

    /// <summary>Фиктивный элемент только для теста: доказывает, что "элемент объявляет свои
    /// строки спецификации с их единицами" работает для единицы, которой сегодня нет ни у
    /// одного производственного элемента (карта §3.2, этап 0 — фундамент считает бетон в м³
    /// позже; здесь только скелет). Ни один продакшн-класс не меняется.</summary>
    private class FakeConcreteBlock : KitchenElement, IQuantifies
    {
        public string Section = "Фундамент";
        public string ItemName = "Бетон B20";
        public float VolumeM3 = 1f;

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return new SpecItem(Section, ItemName, "Бетон", SpecUnit.VolumeM3, VolumeM3);
        }
    }

    private static FakeConcreteBlock CreateConcreteBlock(string name, float volumeM3)
    {
        var go = new GameObject(name);
        var element = go.AddComponent<FakeConcreteBlock>();
        element.PartName = name;
        element.ItemName = name;
        element.VolumeM3 = volumeM3;
        return element;
    }

    [Test]
    public void Build_LegacyBoard_DefaultsToFurnitureSectionAndAreaUnit()
    {
        var board = CreateElement("Board", new Vector3Int(800, 400, 18));

        var result = SpecificationManager.Build(new List<KitchenElement> { board });

        Assert.AreEqual("Мебель", result.lines[0].section);
        Assert.AreEqual(SpecUnit.AreaM2, result.lines[0].unit);

        Object.DestroyImmediate(board.gameObject);
    }

    [Test]
    public void Build_IQuantifiesElement_ReportsOwnUnit()
    {
        var block = CreateConcreteBlock("Бетон B20", 1.2f);

        var result = SpecificationManager.Build(new List<KitchenElement> { block });

        Assert.AreEqual(1, result.lines.Count);
        Assert.AreEqual(SpecUnit.VolumeM3, result.lines[0].unit);
        Assert.AreEqual("Фундамент", result.lines[0].section);
        Assert.AreEqual(1.2f, result.lines[0].qtyTotal, 0.0001f);

        Object.DestroyImmediate(block.gameObject);
    }

    [Test]
    public void Build_IQuantifiesElement_DoesNotLeakIntoLegacyAreaTotal()
    {
        var board = CreateElement("Board", new Vector3Int(800, 400, 18));
        var block = CreateConcreteBlock("Бетон B20", 1.2f);

        var result = SpecificationManager.Build(new List<KitchenElement> { board, block });

        Assert.AreEqual(0.32f, result.totalAreaM2, 0.0001f,
            "кубометры бетона не должны попасть в старый агрегат площади м²");

        Object.DestroyImmediate(board.gameObject);
        Object.DestroyImmediate(block.gameObject);
    }

    [Test]
    public void Build_IQuantifiesElement_TotalsByUnit_KeepsVolumeSeparateFromArea()
    {
        var board = CreateElement("Board", new Vector3Int(800, 400, 18));
        var block = CreateConcreteBlock("Бетон B20", 1.2f);

        var result = SpecificationManager.Build(new List<KitchenElement> { board, block });

        Assert.AreEqual(0.32f, result.totalsByUnit[SpecUnit.AreaM2], 0.0001f);
        Assert.AreEqual(1.2f, result.totalsByUnit[SpecUnit.VolumeM3], 0.0001f,
            "итог по м³ считается отдельно от итога по м², а не складывается в общую кучу");

        Object.DestroyImmediate(board.gameObject);
        Object.DestroyImmediate(block.gameObject);
    }

    [Test]
    public void Build_TwoIdenticalIQuantifiesItems_GroupIntoOneLineWithSummedQty()
    {
        var a = CreateConcreteBlock("Бетон B20", 0.5f);
        var b = CreateConcreteBlock("Бетон B20", 0.5f);

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        Assert.AreEqual(1, result.lines.Count, "одинаковая позиция бетона — одна строка");
        Assert.AreEqual(2, result.lines[0].count);
        Assert.AreEqual(1.0f, result.lines[0].qtyTotal, 0.0001f);

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    // ── Приёмка возврата: §1 сумма разных слагаемых ──

    /// <summary>Дефект из приёмки: сумма считалась как count × qtyPerItem первой строки, а не
    /// как накопление реальных qty. 1,2 + 0,8 обязаны дать 2,0, а не 2,4 (2 × 1,2) — слагаемые
    /// намеренно РАЗНЫЕ, чтобы тест мог упасть на старой формуле.</summary>
    [Test]
    public void Build_TwoIQuantifiesItems_DifferentQty_SumsRealQtyNotFirstTimesCount()
    {
        var a = CreateConcreteBlock("Песок", 1.2f);
        var b = CreateConcreteBlock("Песок", 0.8f);

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        Assert.AreEqual(1, result.lines.Count);
        Assert.AreEqual(2, result.lines[0].count);
        Assert.AreEqual(2.0f, result.lines[0].qtyTotal, 0.0001f,
            "1,2 + 0,8 = 2,0; старая формула (2 × 1,2 = 2,4) не должна вернуться");

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    /// <summary>Тот же дефект на штучных деталях с большими числами: 1240 + 800 = 2040, а
    /// старая формула (2 × 1240 = 2480) — нет.</summary>
    [Test]
    public void Build_TwoIQuantifiesItems_DifferentPieceCounts_SumsRealQty()
    {
        var a = CreateConcreteBlock("Кирпич", 1240f);
        var b = CreateConcreteBlock("Кирпич", 800f);

        var result = SpecificationManager.Build(new List<KitchenElement> { a, b });

        Assert.AreEqual(1, result.lines.Count);
        Assert.AreEqual(2040f, result.lines[0].qtyTotal, 0.0001f,
            "1240 + 800 = 2040; старая формула (2 × 1240 = 2480) не должна вернуться");

        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    // ── Приёмка возврата: §2 IQuantifies спрашивается у КОМПОНЕНТОВ, стена не блокирует ──

    private class FakeWallQuantities : MonoBehaviour, IQuantifies
    {
        public float LengthM = 1f;

        public IEnumerable<SpecItem> GetSpecItems(IReadOnlyList<KitchenElement> allElements)
        {
            yield return new SpecItem("Стены", "Кладка", "Кирпич", SpecUnit.LinearMeters, LengthM);
        }
    }

    /// <summary>D12: хозяин со стеной (`Wall`) когда-то выбрасывался из спецификации ДО проверки
    /// `IQuantifies` — компонент вроде `WallQuantities` не смог бы подключиться никогда. Теперь
    /// `Build` спрашивает КОМПОНЕНТЫ элемента, а не тип, и наличие `Wall` на хозяине этому не
    /// мешает.</summary>
    [Test]
    public void Build_WallHostWithQuantifiesComponent_IsCounted()
    {
        var go = new GameObject("Wall");
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Wall";
        go.AddComponent<Wall>();
        var quantifies = go.AddComponent<FakeWallQuantities>();
        quantifies.LengthM = 3f;

        var result = SpecificationManager.Build(new List<KitchenElement> { element });

        Assert.AreEqual(1, result.lines.Count, "стена с IQuantifies обязана попасть в спецификацию");
        Assert.AreEqual(SpecUnit.LinearMeters, result.lines[0].unit);
        Assert.AreEqual(3f, result.lines[0].qtyTotal, 0.0001f);

        Object.DestroyImmediate(go);
    }

    /// <summary>Стена БЕЗ `IQuantifies` — противоположный вход к предыдущему тесту. Она не должна
    /// ни попасть в спецификацию, ни (тем более) залипнуть в старую ветку доски ЛДСП — тип
    /// хозяина совпадает с обычной доской (`KitchenElement`), различает их только `Wall`.</summary>
    [Test]
    public void Build_WallHostWithoutQuantifiesComponent_IsDropped()
    {
        var go = new GameObject("Wall");
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Wall";
        element.DimensionsMM = new Vector3Int(3000, 2700, 100);
        go.AddComponent<Wall>();

        var result = SpecificationManager.Build(new List<KitchenElement> { element });

        Assert.AreEqual(0, result.lines.Count,
            "стена без IQuantifies не умеет себя посчитать — не должна появиться как доска ЛДСП");

        Object.DestroyImmediate(go);
    }

    // ── Приёмка возврата: §3 не-IQuantifies элемент, который не доска, вообще не попадает ──

    /// <summary>Дефект из приёмки: стул шёл в старую ветку и получал «м² пласти», как будто он
    /// доска ЛДСП. Стул не реализует ни `IQuantifies`, ни `ISpecificationParts` и не является
    /// доскообразным типом — он обязан просто не попасть в спецификацию.</summary>
    [Test]
    public void Build_NonBoardElementWithoutQuantifies_IsDroppedNotCountedAsBoard()
    {
        var go = new GameObject("Chair");
        var chair = go.AddComponent<ChairElement>();
        chair.PartName = "Chair";
        chair.DimensionsMM = new Vector3Int(ChairElement.DefaultWidthMM,
            ChairElement.DefaultHeightMM, ChairElement.DefaultDepthMM);

        var result = SpecificationManager.Build(new List<KitchenElement> { chair });

        Assert.AreEqual(0, result.lines.Count,
            "стул не умеет считать себя сам — не должен превратиться в доску ЛДСП");

        Object.DestroyImmediate(go);
    }

    // ── Приёмка возврата: §4 «Всего» не мешает штуки с кубометрами ──

    /// <summary>Дефект из приёмки: `totalCount` складывал число досок с числом фундаментов —
    /// «Всего» переставало быть числом досок. Одна доска + один фундамент (м³) — «Всего»
    /// обязано остаться равным 1 (доска), а не 2.</summary>
    [Test]
    public void Build_BoardPlusFoundation_TotalCountCountsOnlyFurnitureBoards()
    {
        var board = CreateElement("Board", new Vector3Int(800, 400, 18));
        var block = CreateConcreteBlock("Бетон B20", 1.2f);

        var result = SpecificationManager.Build(new List<KitchenElement> { board, block });

        Assert.AreEqual(1, result.totalCount,
            "«Всего» — это счёт досок (м²), кубометры фундамента сюда не входят");

        Object.DestroyImmediate(board.gameObject);
        Object.DestroyImmediate(block.gameObject);
    }

    // ── Дефект 8356a589: список точных типов в SpecificationManager, не самоописание элемента ──

    /// <summary>Тест-элемент только для этого файла: наследник, который явно ЗАЯВЛЯЕТ себя
    /// доскообразным (как `FacadeElement`/`PanelElement`/`DrawerElement` в продакшне), продакшн-
    /// класс не меняется. Раньше (дефект 8356a589) старый код проверял
    /// `e.GetType() == typeof(KitchenElement)` — точное совпадение типа, поэтому ЛЮБОЙ наследник
    /// `KitchenElement` тихо выпадал из ведомости раскроя, даже настоящая доска. Теперь default —
    /// НЕ доска (см. следующий тест), и настоящая доска обязана попасть, но только объявив себя
    /// явно через override => true — вот что доказывает этот тест.</summary>
    private class FakeBoardDescendant : KitchenElement
    {
        public override bool IsFlatBoardElement => true;
    }

    [Test]
    public void Build_DescendantOfKitchenElement_DeclaringItself_IsIncludedAsBoard()
    {
        var go = new GameObject("CustomBoard");
        var element = go.AddComponent<FakeBoardDescendant>();
        element.PartName = "CustomBoard";
        element.DimensionsMM = new Vector3Int(500, 300, 18);

        var result = SpecificationManager.Build(new List<KitchenElement> { element });

        Assert.AreEqual(1, result.lines.Count,
            "наследник, объявивший себя доской (IsFlatBoardElement => true), обязан попасть в ведомость");
        Assert.AreEqual(1, result.totalCount);
        Assert.AreEqual("CustomBoard", result.lines[0].name);

        Object.DestroyImmediate(go);
    }

    // ── Умолчание перевёрнуто: не доска, пока не заявлено обратное (карта §3.2, часть 3) ──

    /// <summary>Противоположный вход к тесту выше: наследник `KitchenElement`, который НИЧЕГО о
    /// себе не объявил (ни `IsFlatBoardElement`, ни `IQuantifies`, ни `ISpecificationParts`) —
    /// ровно форма будущего фундамента/стены-пирога/кровли из части 3. Он обязан просто не
    /// попасть в спецификацию, а не тихо посчитаться в квадратных метрах пласти, как если бы был
    /// доской ЛДСП. Это тест, который поймал бы будущий фундамент.</summary>
    private class FakeUndeclaredElement : KitchenElement { }

    [Test]
    public void Build_DescendantOfKitchenElement_DeclaringNothing_IsDroppedNotCountedAsBoard()
    {
        var go = new GameObject("FutureFoundation");
        var element = go.AddComponent<FakeUndeclaredElement>();
        element.PartName = "FutureFoundation";
        element.DimensionsMM = new Vector3Int(500, 300, 18);

        var result = SpecificationManager.Build(new List<KitchenElement> { element });

        Assert.AreEqual(0, result.lines.Count,
            "наследник, ничего не заявивший о себе, не умеет считать себя сам — не должен стать доской ЛДСП по умолчанию");

        Object.DestroyImmediate(go);
    }
}
