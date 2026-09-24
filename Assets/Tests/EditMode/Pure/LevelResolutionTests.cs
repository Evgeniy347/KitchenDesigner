using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// L1 (план LEVELS): чистое разрешение уровня для элемента без сцены. Старый проект без
/// <see cref="ProjectData.levels"/> обязан читаться как ровно один уровень «1 этаж» на
/// отметке 0, а элемент без <see cref="ElementData.levelId"/> (или с чужим id) — как
/// принадлежащий первому уровню. Ни один метод здесь не имеет права записать что-либо
/// обратно во входной массив — вызывающая сторона держит на нём ссылку на файл проекта.
/// </summary>
public class LevelResolutionTests
{
    [Test]
    public void EffectiveLevels_EmptyArray_SynthesisesExactlyOneLevelAtZero()
    {
        var result = LevelResolution.EffectiveLevels(new Level[0], 3000);

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(0, result[0].floorElevationMm);
        Assert.AreEqual(3000, result[0].heightMm);
        Assert.AreEqual(LevelResolution.DefaultLevelName, result[0].name);
        Assert.AreEqual(LevelResolution.DefaultLevelId, result[0].id);
    }

    [Test]
    public void EffectiveLevels_Null_SynthesisesExactlyOneLevelAtZero()
    {
        var result = LevelResolution.EffectiveLevels(null, 2500);

        Assert.AreEqual(1, result.Length);
        Assert.AreEqual(0, result[0].floorElevationMm);
        Assert.AreEqual(2500, result[0].heightMm);
    }

    [Test]
    public void EffectiveLevels_SynthesisedHeight_ComesFromTheGivenDefault_NotAHardcodedNumber()
    {
        var atA = LevelResolution.EffectiveLevels(null, 2000);
        var atB = LevelResolution.EffectiveLevels(null, 6000);

        Assert.AreEqual(2000, atA[0].heightMm);
        Assert.AreEqual(6000, atB[0].heightMm);
    }

    [Test]
    public void EffectiveLevels_NonEmptyInput_IsReturnedAsIs()
    {
        var input = new[] { new Level("7", "Мой этаж", 3000, 2800) };

        var result = LevelResolution.EffectiveLevels(input, 3000);

        Assert.AreSame(input, result, "непустой список уровней проекта не подменяется синтезированным");
    }

    [Test]
    public void EffectiveLevels_NeverMutatesTheInputArray()
    {
        Level[]? input = null;
        var before = input;

        LevelResolution.EffectiveLevels(input, 3000);

        Assert.IsNull(before, "функция не имеет права писать через ref/out в переданный проектом массив");
    }

    [Test]
    public void ResolveElementLevel_EmptyLevelId_ResolvesToFirstLevel()
    {
        var levels = new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        };

        var resolved = LevelResolution.ResolveElementLevel("", levels);

        Assert.AreSame(levels[0], resolved);
    }

    [Test]
    public void ResolveElementLevel_NullLevelId_ResolvesToFirstLevel()
    {
        var levels = new[] { new Level("1", "1 этаж", 0, 3000) };

        Assert.AreSame(levels[0], LevelResolution.ResolveElementLevel(null, levels));
    }

    [Test]
    public void ResolveElementLevel_UnknownLevelId_ResolvesToFirstLevel()
    {
        var levels = new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        };

        var resolved = LevelResolution.ResolveElementLevel("does-not-exist", levels);

        Assert.AreSame(levels[0], resolved,
            "неизвестный (например, удалённый) уровень обязан падать на первый, а не бросать исключение");
    }

    [Test]
    public void ResolveElementLevel_KnownLevelId_ResolvesToTheMatchingLevel_NotTheFirstOne()
    {
        var levels = new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        };

        var resolved = LevelResolution.ResolveElementLevel("2", levels);

        Assert.AreSame(levels[1], resolved,
            "проверка обязана уметь падать не только на первый уровень — иначе она не может быть красной");
    }

    [Test]
    public void ResolveElementLevel_IdComparison_IsOrdinal_NotCaseInsensitive()
    {
        var levels = new[] { new Level("A", "1 этаж", 0, 3000), new Level("a", "Подвал", -3000, 2500) };

        var resolved = LevelResolution.ResolveElementLevel("a", levels);

        Assert.AreSame(levels[1], resolved, "id уровня — точный идентификатор, а не заголовок без регистра");
    }
}
