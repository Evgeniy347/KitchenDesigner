using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

public class ContextMenuLayoutEngineTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private RectTransform MakeRect(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        _spawned.Add(go);
        return (RectTransform)go.transform;
    }

    [Test]
    public void EveryRow_IsAnchoredToThePanelTop_SoPanelHeightDoesNotMoveContent()
    {
        var layout = new ContextMenuLayout();
        var row = MakeRect("Row");
        layout.Add(24f, 7f, row);

        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.AreEqual(new Vector2(0.5f, 1f), row.anchorMin,
            "строка должна якориться к верху панели: иначе рост панели вниз потащил бы за собой всё содержимое");
        Assert.AreEqual(new Vector2(0.5f, 1f), row.anchorMax, "верхний якорь — обе стороны");
        Assert.AreEqual(new Vector2(0.5f, 1f), row.pivot,
            "pivot тоже сверху, иначе anchoredPosition.y перестанет быть отступом верхней кромки");
    }

    [Test]
    public void RowsAreStackedTopDown_ByHeightPlusGap()
    {
        var layout = new ContextMenuLayout();
        var first = MakeRect("First");
        var second = MakeRect("Second");
        layout.Add(24f, 7f, first);
        layout.Add(28f, 8f, second);

        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.AreEqual(-12f, first.anchoredPosition.y, 0.001f,
            "первая строка стоит ровно на верхнем отступе панели");
        Assert.AreEqual(-(12f + 24f + 7f), second.anchoredPosition.y, 0.001f,
            "вторая строка сдвинута на высоту первой плюс отступ под ней");
    }

    [Test]
    public void PanelHeight_CoversTheLastVisibleRow_WithoutItsTrailingGap()
    {
        var layout = new ContextMenuLayout();
        layout.Add(24f, 7f, MakeRect("First"));
        layout.Add(28f, 8f, MakeRect("Second"));

        float contentBottom = layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.AreEqual(12f + 24f + 7f + 28f, contentBottom, 0.001f,
            "низ содержимого = последняя видимая строка; её собственный отступ снизу в высоту панели не входит");
    }

    [Test]
    public void HiddenRow_IsDeactivated_NotLeftAtItsPositionFromThePreviousLayout()
    {
        var layout = new ContextMenuLayout();
        var conditional = MakeRect("Conditional");
        bool visible = true;
        layout.AddWhen(() => visible, 24f, 7f, conditional);
        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);
        Assert.IsTrue(conditional.gameObject.activeSelf, "при истинном условии строка видна");

        visible = false;
        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.IsFalse(conditional.gameObject.activeSelf,
            "строку с visibleWhen надо гасить: иначе она осталась бы на экране в позиции от прошлой раскладки");
    }

    [Test]
    public void HiddenRow_DoesNotConsumeVerticalSpace()
    {
        var layout = new ContextMenuLayout();
        var hidden = MakeRect("Hidden");
        var after = MakeRect("After");
        layout.AddWhen(() => false, 24f, 7f, hidden);
        layout.Add(28f, 8f, after);

        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.AreEqual(-12f, after.anchoredPosition.y, 0.001f,
            "скрытая строка не оставляет дырки — следующая занимает её место");
    }

    [Test]
    public void UnconditionalRow_IsNeverDeactivatedByTheLayout()
    {
        var layout = new ContextMenuLayout();
        var always = MakeRect("Always");
        always.gameObject.SetActive(false);
        layout.Add(24f, 7f, always);

        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.IsFalse(always.gameObject.activeSelf,
            "безусловную строку раскладка не трогает: её видимостью управляет тот, кто её создал");
    }

    [Test]
    public void RowShownForAFacet_IsVisibleOnlyWhenThatFacetIsPresent()
    {
        var layout = new ContextMenuLayout();
        var drawerRow = MakeRect("DrawerRow");
        layout.AddFor(ElementFacet.Drawer, 24f, 7f, drawerRow);

        layout.Apply(ElementFacet.Facade, showRotationXZ: true, topPadding: 12f);
        Assert.IsFalse(drawerRow.gameObject.activeSelf, "строка ящика у фасада скрыта");

        layout.Apply(ElementFacet.Drawer, showRotationXZ: true, topPadding: 12f);
        Assert.IsTrue(drawerRow.gameObject.activeSelf, "строка ящика у ящика видна");
    }

    [Test]
    public void RowHiddenForAFacet_DisappearsWhenThatFacetIsPresent()
    {
        var layout = new ContextMenuLayout();
        var rotationRow = MakeRect("RotationRow");
        layout.AddExcept(ElementFacet.Window, 24f, 7f, rotationRow);

        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);
        Assert.IsTrue(rotationRow.gameObject.activeSelf, "у обычной детали повороты показываются");

        layout.Apply(ElementFacet.Window, showRotationXZ: true, topPadding: 12f);
        Assert.IsFalse(rotationRow.gameObject.activeSelf,
            "у окна ориентацию диктует стена — строка поворотов скрыта");
    }

    [Test]
    public void FacetRowWithCondition_NeedsBothTheFacetAndTheCondition()
    {
        var layout = new ContextMenuLayout();
        var row = MakeRect("Row");
        bool expanded = false;
        layout.AddFor(ElementFacet.Light, () => expanded, 24f, 7f, row);

        layout.Apply(ElementFacet.Light, showRotationXZ: true, topPadding: 12f);
        Assert.IsFalse(row.gameObject.activeSelf,
            "калибровка лампы спрятана под раскрывашкой: полей десяток, а трогают их раз в жизни");

        expanded = true;
        layout.Apply(ElementFacet.Light, showRotationXZ: true, topPadding: 12f);
        Assert.IsTrue(row.gameObject.activeSelf, "раскрытая настройка показывает строку");

        layout.Apply(ElementFacet.Drawer, showRotationXZ: true, topPadding: 12f);
        Assert.IsFalse(row.gameObject.activeSelf, "у ящика строки лампы нет даже в раскрытом состоянии");
    }

    [Test]
    public void TriRow_KeepsLabelsAndFieldsInTwoRows_SoColumnsCannotDrift()
    {
        var layout = new ContextMenuLayout();
        var labelA = MakeRect("LabelA");
        var fieldA = MakeRect("FieldA");
        var labelB = MakeRect("LabelB");
        var fieldB = MakeRect("FieldB");
        layout.AddTriColumn(labelA, fieldA);
        layout.AddTriColumn(labelB, fieldB);
        layout.EndTriRow(18f, 2f, 24f, 7f, ElementFacet.None);

        layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.AreEqual(labelA.anchoredPosition.y, labelB.anchoredPosition.y, 0.001f,
            "подписи трёх колонок раскладываются одной строкой, иначе они разъезжаются по вертикали");
        Assert.AreEqual(fieldA.anchoredPosition.y, fieldB.anchoredPosition.y, 0.001f,
            "поля трёх колонок — тоже одна строка");
        Assert.AreEqual(-(12f + 18f + 2f), fieldA.anchoredPosition.y, 0.001f,
            "строка полей идёт сразу под строкой подписей");
    }

    [Test]
    public void TriColumns_AreReadableBeforeEndTriRow_ThenCleared()
    {
        var layout = new ContextMenuLayout();
        layout.AddTriColumn(MakeRect("LabelX"), MakeRect("FieldX"));

        Assert.AreEqual(1, layout.PendingTriLabels.Count,
            "накопленные колонки видны до закрытия строки — на них подписывается скрытие поворотов X и Z");

        layout.EndTriRow(18f, 2f, 24f, 7f, ElementFacet.None);

        Assert.AreEqual(0, layout.PendingTriLabels.Count, "после закрытия строки накопитель подписей пуст");
        Assert.AreEqual(0, layout.PendingTriFields.Count, "после закрытия строки накопитель полей пуст");
    }

    [Test]
    public void RotationXZ_IsHiddenForYawOnlyAppliances_WithoutChangingRowHeights()
    {
        var layout = new ContextMenuLayout();
        var rotX = MakeRect("RotX");
        var rotY = MakeRect("RotY");
        layout.Add(24f, 7f, rotX, rotY);
        layout.AddRotationXZ(rotX);
        var next = MakeRect("Next");
        layout.Add(28f, 8f, next);

        layout.Apply(ElementFacet.None, showRotationXZ: false, topPadding: 12f);

        Assert.IsFalse(rotX.gameObject.activeSelf,
            "у встраиваемой техники поворот по X убирается совсем, а не гасится наполовину");
        Assert.IsTrue(rotY.gameObject.activeSelf, "поворот по Y технике нужен и живёт в той же строке");
        Assert.AreEqual(-(12f + 24f + 7f), next.anchoredPosition.y, 0.001f,
            "высоту строки держит оставшаяся колонка Y — дырки в панели не появляется");
    }

    [Test]
    public void Clear_DropsEveryRow_SoRebuildDoesNotStackTwoMenus()
    {
        var layout = new ContextMenuLayout();
        layout.Add(24f, 7f, MakeRect("Row"));
        layout.Clear();

        float contentBottom = layout.Apply(ElementFacet.None, showRotationXZ: true, topPadding: 12f);

        Assert.AreEqual(12f, contentBottom, 0.001f, "после очистки в раскладке не осталось ни одной строки");
    }
}
