using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>test-results/review-persistence.md #1, "Secondary": every *JsonTrim class trimmed
/// `basePlate` through its OWN `RemoveFromNamedObject`, which handed `RemoveMember` the FULL
/// document (not just the basePlate object) once per removed key — about 38 full-document
/// copies per save across the seven families that touch basePlate. `BasePlateFamilyJsonTrim`
/// extracts the basePlate object ONCE, runs every family's own removal on that small slice,
/// and splices it back ONCE, so the per-key cost is paid against basePlate's own size, not the
/// document's — regardless of how many elements the project holds.
///
/// `NewPipeline_ProducesByteIdenticalOutput_ToTheOldPerTrimChain` is the safety net: collapsing
/// seven passes into one must not change a single byte of what gets written to a user's
/// project file.</summary>
public class BasePlateFamilyJsonTrimTests
{
    private static long TakeCharsProcessed() =>
        JsonText.TakeCharsProcessedByRemoveMember() + JsonObjectEdit.TakeCharsRead();

    private static string BuildProject(int elementCount)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"elements\": [\n");
        for (int i = 0; i < elementCount; i++)
        {
            if (i > 0) sb.Append(",\n");
            sb.Append("    {\"name\": \"E").Append(i).Append("\", \"movable\": true}");
        }
        sb.Append("\n  ],\n");
        sb.Append("  \"basePlateValid\": true,\n");
        sb.Append("  \"basePlate\": {\n");
        sb.Append("    \"name\": \"BasePlate\",\n");
        sb.Append("    \"levelId\": \"\",\n");
        sb.Append("    \"isFence\": false,\n");
        sb.Append("    \"fencePostSectionMm\": 60,\n");
        sb.Append("    \"fencePostStepMm\": 2500,\n");
        sb.Append("    \"fencePitDepthMm\": 1200,\n");
        sb.Append("    \"fenceSheetMark\": 0,\n");
        sb.Append("    \"isFloorSlab\": false,\n");
        sb.Append("    \"slabTechnology\": 0,\n");
        sb.Append("    \"slabConcreteGrade\": 0,\n");
        sb.Append("    \"slabRebarDiameterMm\": 0,\n");
        sb.Append("    \"slabRebarStepMm\": 0,\n");
        sb.Append("    \"isFoundation\": false,\n");
        sb.Append("    \"foundationSoilKind\": 0,\n");
        sb.Append("    \"foundationSandMm\": 0,\n");
        sb.Append("    \"foundationGravelMm\": 0,\n");
        sb.Append("    \"foundationCompacted\": false,\n");
        sb.Append("    \"foundationConcreteGrade\": 0,\n");
        sb.Append("    \"foundationRebarDiameterMm\": 0,\n");
        sb.Append("    \"foundationRebarStepMm\": 0,\n");
        sb.Append("    \"foundationCoverMm\": 0,\n");
        sb.Append("    \"isInsulation\": false,\n");
        sb.Append("    \"isVentGap\": false,\n");
        sb.Append("    \"isCladding\": false,\n");
        sb.Append("    \"wallLayerHostWallName\": \"\",\n");
        sb.Append("    \"ventGapBattenStepMm\": 0,\n");
        sb.Append("    \"isRoof\": false,\n");
        sb.Append("    \"roofType\": 1,\n");
        sb.Append("    \"roofRidgeAxis\": 0,\n");
        sb.Append("    \"roofPitchDeg\": 0,\n");
        sb.Append("    \"roofOverhangMm\": 0,\n");
        sb.Append("    \"roofRafterStepMm\": 0,\n");
        sb.Append("    \"isDuct\": false,\n");
        sb.Append("    \"ductProfileKind\": 0,\n");
        sb.Append("    \"ductDiameterMm\": 0,\n");
        sb.Append("    \"ductWidthMm\": 0,\n");
        sb.Append("    \"ductHeightMm\": 0,\n");
        sb.Append("    \"ductAirflowM3PerHour\": 0,\n");
        sb.Append("    \"isGrille\": false,\n");
        sb.Append("    \"grilleAirflowM3PerHour\": 0,\n");
        sb.Append("    \"movable\": false\n");
        sb.Append("  }\n}");
        return sb.ToString();
    }

    private static string OldPerTrimChain(string project)
    {
        var result = FoundationJsonTrim.RemoveWhenNotFoundation(project);
        result = FloorSlabJsonTrim.RemoveWhenNotFloorSlab(result);
        result = FenceJsonTrim.RemoveWhenNotFence(result);
        result = WallLayerJsonTrim.RemoveWhenNotWallLayer(result);
        result = RoofJsonTrim.RemoveWhenNotRoof(result);
        result = DuctJsonTrim.RemoveWhenNotDuct(result);
        result = LevelsJsonTrim.RemoveWhenEmpty(result);
        return result;
    }

    private static string NewElementsOnlyThenCollapsedBasePlate(string project)
    {
        var result = LevelsJsonTrim.RemoveEmptyLevelIdFromElements(project);
        result = FoundationJsonTrim.RemoveFromElementsArray(result);
        result = FloorSlabJsonTrim.RemoveFromElementsArray(result);
        result = FenceJsonTrim.RemoveFromElementsArray(result);
        result = WallLayerJsonTrim.RemoveFromElementsArray(result);
        result = RoofJsonTrim.RemoveFromElementsArray(result);
        result = DuctJsonTrim.RemoveFromElementsArray(result);
        result = BasePlateFamilyJsonTrim.RemoveWhenNotNeeded(result);
        result = LevelsJsonTrim.RemoveEmptyLevelsArray(result);
        return result;
    }

    [Test]
    public void NewPipeline_ProducesByteIdenticalOutput_ToTheOldPerTrimChain()
    {
        string project = BuildProject(50);

        string oldWay = OldPerTrimChain(project);
        string newWay = NewElementsOnlyThenCollapsedBasePlate(project);

        Assert.AreEqual(oldWay, newWay,
            "коллапс семи проходов по basePlate в один не имеет права изменить итоговый JSON "
            + "ни на байт - ключи, которые трогает каждая семья, не пересекаются, поэтому порядок "
            + "склейки не должен быть виден в результате");
    }

    [Test]
    public void RemoveWhenNotNeeded_DropsEveryFamilyKey_FromBasePlate()
    {
        string trimmed = BasePlateFamilyJsonTrim.RemoveWhenNotNeeded(BuildProject(1));

        var root = JsonText.RootObject(trimmed);
        var basePlate = JsonText.MemberValue(trimmed, root, "basePlate");
        Assert.IsTrue(basePlate.Found, "basePlate сам по себе обязан остаться");

        foreach (var key in new[]
                 {
                     "levelId", "isFence", "fencePostSectionMm", "isFloorSlab", "slabTechnology",
                     "isFoundation", "foundationCoverMm", "isInsulation", "isVentGap", "isCladding",
                     "wallLayerHostWallName", "ventGapBattenStepMm", "isRoof", "roofRafterStepMm",
                     "isDuct", "ductAirflowM3PerHour", "isGrille", "grilleAirflowM3PerHour",
                 })
        {
            Assert.IsFalse(JsonText.MemberValue(trimmed, basePlate, key).Found,
                $"{key} - мусорный ключ подложки, обязан пропасть при единственном проходе");
        }

        Assert.IsTrue(JsonText.MemberValue(trimmed, basePlate, "name").Found,
            "имя подложки - не мусор, обязано пережить срез");
        Assert.IsTrue(JsonText.MemberValue(trimmed, basePlate, "movable").Found,
            "поле после всех удалённых семей обязано пережить единственный проход");
    }

    [Test]
    public void CollapsedBasePlatePass_CharsProcessed_DoesNotScaleWithDocumentSize()
    {
        string small = NewElementsOnlyFor(BuildProject(50));
        string large = NewElementsOnlyFor(BuildProject(2000));

        TakeCharsProcessed();
        BasePlateFamilyJsonTrim.RemoveWhenNotNeeded(small);
        long smallChars = TakeCharsProcessed();

        BasePlateFamilyJsonTrim.RemoveWhenNotNeeded(large);
        long largeChars = TakeCharsProcessed();

        Assert.Greater(smallChars, 0, "basePlate несёт удаляемые ключи, его срез обязан быть прочитан");
        Assert.Less(largeChars, smallChars * 3,
            $"elements вырос в 40 раз (50 -> 2000), а обработка basePlate {smallChars} -> {largeChars} "
            + "символов почти не изменилась: единственный проход разбирает СРЕЗ basePlate, "
            + "а не документ целиком");
    }

    private static string NewElementsOnlyFor(string project) =>
        NewElementsOnlyThenCollapsedBasePlateSansBasePlate(project);

    private static string NewElementsOnlyThenCollapsedBasePlateSansBasePlate(string project)
    {
        var result = LevelsJsonTrim.RemoveEmptyLevelIdFromElements(project);
        result = FoundationJsonTrim.RemoveFromElementsArray(result);
        result = FloorSlabJsonTrim.RemoveFromElementsArray(result);
        result = FenceJsonTrim.RemoveFromElementsArray(result);
        result = WallLayerJsonTrim.RemoveFromElementsArray(result);
        result = RoofJsonTrim.RemoveFromElementsArray(result);
        result = DuctJsonTrim.RemoveFromElementsArray(result);
        return result;
    }
}
