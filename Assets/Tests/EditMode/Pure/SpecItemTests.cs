using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ключ группировки <see cref="SpecItem.GroupKey"/> — то, что решает, сольются ли
/// две объявленные позиции в одну строку с суммарным количеством, или останутся раздельными
/// позициями. Разная единица или раздел обязаны дать разные ключи: иначе кубометры бетона
/// и погонные метры арматуры сложились бы в одну "количество" строку.</summary>
public class SpecItemTests
{
    [Test]
    public void GroupKey_SameSectionNameMaterialUnit_Matches()
    {
        var a = new SpecItem("Фундамент", "Бетон B20", "Бетон", SpecUnit.VolumeM3, 1.2f);
        var b = new SpecItem("Фундамент", "Бетон B20", "Бетон", SpecUnit.VolumeM3, 0.8f);

        Assert.AreEqual(a.GroupKey(), b.GroupKey(), "одна и та же позиция — один ключ вне зависимости от qty");
    }

    [Test]
    public void GroupKey_DifferentUnit_DoesNotMatch()
    {
        var volume = new SpecItem("Фундамент", "Бетон B20", "Бетон", SpecUnit.VolumeM3, 1.2f);
        var mass = new SpecItem("Фундамент", "Бетон B20", "Бетон", SpecUnit.Kilograms, 1.2f);

        Assert.AreNotEqual(volume.GroupKey(), mass.GroupKey(),
            "м³ бетона и кг бетона — разные строки, единица участвует в ключе");
    }

    [Test]
    public void GroupKey_DifferentSection_DoesNotMatch()
    {
        var wall = new SpecItem("Стены", "Кирпич", "Керамика", SpecUnit.Pieces, 100f);
        var fence = new SpecItem("Забор", "Кирпич", "Керамика", SpecUnit.Pieces, 100f);

        Assert.AreNotEqual(wall.GroupKey(), fence.GroupKey(),
            "раздел — данные строки, а не сортировка: одинаковый кирпич в разных разделах не сливается");
    }

    [Test]
    public void GroupKey_DimsMatter_WhenHasDims()
    {
        var a = new SpecItem("Мебель", "Полка", "ЛДСП", SpecUnit.AreaM2, 0.3f,
            new Vector3Int(800, 400, 18), hasDims: true);
        var b = new SpecItem("Мебель", "Полка", "ЛДСП", SpecUnit.AreaM2, 0.3f,
            new Vector3Int(600, 400, 18), hasDims: true);

        Assert.AreNotEqual(a.GroupKey(), b.GroupKey(), "разные габариты — разные позиции раскроя");
    }

    [Test]
    public void GroupKey_HasDimsFalse_IgnoresDimsField()
    {
        var a = new SpecItem("Фундамент", "Песок", "Песок", SpecUnit.VolumeM3, 2f,
            new Vector3Int(1, 2, 3), hasDims: false);
        var b = new SpecItem("Фундамент", "Песок", "Песок", SpecUnit.VolumeM3, 5f,
            new Vector3Int(9, 9, 9), hasDims: false);

        Assert.AreEqual(a.GroupKey(), b.GroupKey(),
            "сыпучий материал без габаритов — dims не участвует в ключе, даже если поле заполнено мусором");
    }
}
