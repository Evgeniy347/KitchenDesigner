using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Порядок строк вкладки «Управление» обязан быть детерминированным: заголовок группы,
/// затем все действия этой группы в порядке объявления в <see cref="InputActionCatalog"/>,
/// и так по каждой группе из <see cref="InputActionGroup"/> подряд.
/// </summary>
public class KeybindingRowsTests
{
    [Test]
    public void Build_IsDeterministic_AcrossCalls()
    {
        var first = KeybindingRowList.Build();
        var second = KeybindingRowList.Build();

        Assert.AreEqual(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.AreEqual(first[i].IsGroupHeader, second[i].IsGroupHeader, $"row {i}");
            Assert.AreEqual(first[i].Group, second[i].Group, $"row {i}");
            Assert.AreEqual(first[i].Action, second[i].Action, $"row {i}");
        }
    }

    [Test]
    public void Build_StartsEachGroup_WithItsHeader()
    {
        var rows = KeybindingRowList.Build();

        for (int i = 0; i < KeybindingRowList.GroupOrder.Length; i++)
        {
            var group = KeybindingRowList.GroupOrder[i];
            int headerIndex = IndexOfHeader(rows, group);
            Assert.GreaterOrEqual(headerIndex, 0, $"нет заголовка для группы {group}");
            Assert.IsTrue(rows[headerIndex].IsGroupHeader);
        }
    }

    [Test]
    public void Build_ListsEveryActionExactlyOnce_UnderItsOwnGroup()
    {
        var rows = KeybindingRowList.Build();
        var actionRows = rows.Where(r => !r.IsGroupHeader).ToList();

        Assert.AreEqual(InputActionCatalog.All.Length, actionRows.Count,
            "каждое действие каталога обязано появиться ровно одной строкой");

        var seen = new HashSet<InputAction>();
        foreach (var row in actionRows)
        {
            Assert.IsTrue(seen.Add(row.Action), $"действие {row.Action} встретилось дважды");
            Assert.AreEqual(InputActionCatalog.GroupOf(row.Action), row.Group);
        }
    }

    [Test]
    public void Build_KeepsActionsInCatalogDeclarationOrder_WithinAGroup()
    {
        var rows = KeybindingRowList.Build();
        var byGroup = new Dictionary<InputActionGroup, List<InputAction>>();
        foreach (var row in rows)
        {
            if (row.IsGroupHeader) continue;
            if (!byGroup.TryGetValue(row.Group, out var list))
                byGroup[row.Group] = list = new List<InputAction>();
            list.Add(row.Action);
        }

        foreach (var pair in byGroup)
        {
            var expected = InputActionCatalog.All.Where(a => InputActionCatalog.GroupOf(a) == pair.Key).ToList();
            CollectionAssert.AreEqual(expected, pair.Value, $"группа {pair.Key}");
        }
    }

    [Test]
    public void Build_OrdersGroups_AsDeclaredInTheGroupEnum()
    {
        var rows = KeybindingRowList.Build();
        var headerOrder = rows.Where(r => r.IsGroupHeader).Select(r => r.Group).ToList();

        CollectionAssert.AreEqual(KeybindingRowList.GroupOrder, headerOrder);
    }

    private static int IndexOfHeader(IReadOnlyList<KeybindingRow> rows, InputActionGroup group)
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].IsGroupHeader && rows[i].Group == group) return i;
        return -1;
    }
}
