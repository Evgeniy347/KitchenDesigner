using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

/// <summary>Приёмка дефекта «каталог как данные»: новая позиция каталога — либо
/// TypeRow (новая плитка), либо PresetRow (вариант существующей). PresetRow не
/// принимает заголовок вовсе — заголовок принадлежит плитке, а не варианту,
/// и взять его неоткуда, кроме TypeRow, которым плитка была начата.</summary>
public class SidebarCatalogTableTests
{
    private static SidebarCatalog.Item Probe(SidebarItemKind kind, string name = "Проба") =>
        new SidebarCatalog.Item(name, new Vector3Int(1, 1, 1), kind);

    [Test]
    public void PresetRow_CarriesNoTitleOfItsOwn()
    {
        var row = SidebarCatalogRow.PresetRow(SidebarGroupKey.Sanitary, Probe(SidebarItemKind.PipeFitting));

        Assert.IsFalse(row.IsTypeRow,
            "PresetRow не начинает новую плитку — заголовок плитки уже задан её TypeRow");
        Assert.IsNull(row.TileTitle,
            "у PresetRow нет собственного заголовка: добавить пресет — значит добавить ОДНУ "
            + "такую строку, не трогая ни SidebarItemKind, ни IElementSpawns, ни "
            + "ElementSpawner, ни SidebarSpawnRouter");
    }

    [Test]
    public void TypeRow_CarriesItsOwnExplicitTitleAndGroup()
    {
        var row = SidebarCatalogRow.TypeRow(SidebarGroupKey.Furniture, "Проба",
            Probe(SidebarItemKind.Chair));

        Assert.IsTrue(row.IsTypeRow);
        Assert.AreEqual("Проба", row.TileTitle,
            "заголовок плитки — данные самой строки, а не вычисление по имени пресета");
        Assert.AreEqual(SidebarGroupKey.Furniture, row.Group);
        Assert.AreEqual(SidebarItemKind.Chair, row.Item.kind);
    }
}
