using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Проводные имена режимов открывания. Их три копии: таблица в
/// FacadeDoor.WireNames (что мы ОТДАЁМ), разбор в McpWireEnums (что мы
/// ПРИНИМАЕМ) и список Enum у поля mode в контракте (что мы ОБЪЯВЛЯЕМ).
///
/// Три копии одного словаря расходятся молча и в разные стороны: агент видит
/// в ответе имя, которого не примет обратно, или шлёт объявленное имя, которое
/// не разбирается. Тесты сверяют их попарно и в ОБЕ стороны.</summary>
public class FacadeDoorWireNameTests
{
    private static IEnumerable<DoorMode> AllModes()
        => (DoorMode[])Enum.GetValues(typeof(DoorMode));

    [Test]
    public void EveryMode_HasAWireName_ThatParsesBackToTheSameMode()
    {
        foreach (var mode in AllModes())
        {
            string wire = FacadeDoor.WireName(mode);
            Assert.IsNotEmpty(wire, $"у режима {mode} нет проводного имени");
            Assert.IsTrue(McpWireEnums.TryParseDoorMode(wire, out var parsed),
                $"агент видит «{wire}» в ответе, но передать его обратно не может");
            Assert.AreEqual(mode, parsed,
                $"«{wire}» разбирается в {parsed}, а отдаётся за {mode}");
        }
    }

    [Test]
    public void WireNames_AreDistinct_AndMatchTheDeclaredEnumBothWays()
    {
        var produced = AllModes().Select(FacadeDoor.WireName).ToList();
        CollectionAssert.AllItemsAreUnique(produced,
            "два режима с одним проводным именем неразличимы для агента");

        var declared = DeclaredModeValues();

        var missingInContract = produced.Except(declared).ToList();
        var missingInTable = declared.Except(produced).ToList();

        Assert.IsEmpty(missingInContract,
            "режим есть в коде, но НЕ объявлен в контракте — рабочая возможность спрятана: "
            + string.Join(", ", missingInContract));
        Assert.IsEmpty(missingInTable,
            "имя объявлено в контракте, но кода за ним нет — агент получит молчаливый успех: "
            + string.Join(", ", missingInTable));
    }

    private static List<string> DeclaredModeValues()
    {
        var field = typeof(EditOp).GetField("mode", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, "поле mode у EditOp переименовали — сверять стало нечего");
        var attr = field!.GetCustomAttribute<McpParamAttribute>();
        Assert.IsNotNull(attr, "у поля mode пропал McpParam — контракт больше ничего не объявляет");
        var declared = attr!.Enum.ToList();
        Assert.AreEqual(FacadeDoor.Count, declared.Count,
            "контракт объявляет не столько режимов, сколько их есть");
        return declared;
    }
}
