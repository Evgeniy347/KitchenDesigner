using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Выделение дивана подкрашивает КАЖДЫЙ его рендерер, в том числе у скрытых подушек:
/// <c>ElementRenderers.BodyOf</c> обходит дерево по Transform, а не по активности, а
/// подушки прячутся через <c>SetActive</c> при раскладывании. Снятие выделения возвращает
/// рендереру то, что с него сняли, и не трогает то, что за это время перекрасил кто-то
/// другой (<c>SelectionTintRestoreTests</c> держит это правило на составном элементе).
///
/// Диван — единственный элемент с двумя слотами декора И частями, которые то появляются,
/// то исчезают без участия выделения; здесь проверены оба пересечения: перекраска слота
/// подушек во время выделения и складывание, пока диван выделен.</summary>
public class SofaSelectionTests
{
    private const string OtherDecorId = "oak";

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject? _selectionGo;
    private SelectionManager? _selection;
    private ElementHighlighter? _highlighter;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        CommandStack.Clear();
        _highlighter = ElementHighlighter.Instance;
        ElementHighlighter.Instance = null;
        _selectionGo = new GameObject("SelectionManager сторожа дивана");
        _selection = _selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = _selection;
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = true;
        if (_selection != null) _selection.DeselectAll();
        SelectionManager.Instance = null;
        if (_selectionGo != null) Object.DestroyImmediate(_selectionGo);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        _selectionGo = null;
        _selection = null;
        ElementHighlighter.Instance = _highlighter;
        _highlighter = null;
        CommandStack.Clear();
        PartRegistry.Clear();
        MaterialManager.ClearCache();
        LogAssert.ignoreFailingMessages = false;
    }

    private SofaElement Sofa()
    {
        var go = ElementFactory.CreateSofa(
            new Vector3Int(SofaLayout.DefaultWidthMM, SofaLayout.OverallHeightMM, SofaLayout.DefaultDepthMM),
            SofaLayout.DefaultCornerRadiusMM, SofaLayout.DefaultSeatHeightMM, "Диван-выделение", Vector3.zero);
        _spawned.Add(go);
        var sofa = go.GetComponent<SofaElement>();
        Assert.IsNotNull(sofa, "фабрика обязана вернуть SofaElement");
        return sofa!;
    }

    private static MeshRenderer RendererOf(SofaElement sofa, string group, string part)
    {
        var node = sofa.transform.Find(group + "/" + part);
        Assert.IsNotNull(node, "нет детали " + part);
        var renderer = node!.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "у детали " + part + " нет рендерера");
        return renderer!;
    }

    private static MeshRenderer Seat(SofaElement sofa) => RendererOf(sofa, SofaLayout.FrontGroupName, SofaLayout.SeatName);

    private static MeshRenderer Backrest(SofaElement sofa) => RendererOf(sofa, SofaLayout.HingeGroupName, SofaLayout.BackrestName);

    private static MeshRenderer[] Cushions(SofaElement sofa) => new[]
    {
        RendererOf(sofa, SofaLayout.FrontGroupName, SofaLayout.ArmCushionLeftName),
        RendererOf(sofa, SofaLayout.FrontGroupName, SofaLayout.ArmCushionRightName),
        RendererOf(sofa, SofaLayout.FrontGroupName, SofaLayout.BackCushionLeftName),
        RendererOf(sofa, SofaLayout.FrontGroupName, SofaLayout.BackCushionRightName),
    };

    private static bool WearsTheSelectionTint(MeshRenderer renderer)
        => renderer.sharedMaterial != null
           && renderer.sharedMaterial.name.StartsWith(ElementTint.SelectionName, System.StringComparison.Ordinal);

    private static Material Shared(string materialId)
    {
        var material = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(materialId));
        Assert.IsNotNull(material, "нет общего материала для декора " + materialId);
        return material!;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Deselect_AfterTheCushionDecorChangedWhileSelected_KeepsTheNewCushions_AndRestoresSeatAndBackrest(
        bool panelRefreshesTheHighlight)
    {
        var sofa = Sofa();
        var seat = Seat(sofa);
        var backrest = Backrest(sofa);
        var cushion = Cushions(sofa)[0];
        var seatBefore = seat.sharedMaterial;
        var backrestBefore = backrest.sharedMaterial;
        var cushionBefore = cushion.sharedMaterial;
        var newCushionDecor = Shared(OtherDecorId);
        Assume.That(newCushionDecor, Is.Not.SameAs(cushionBefore),
            "новый декор подушек обязан отличаться от прежнего, иначе перекраска незаметна");

        _selection!.Select(sofa);
        Assume.That(WearsTheSelectionTint(seat) && WearsTheSelectionTint(backrest) && WearsTheSelectionTint(cushion),
            Is.True, "выделение обязано покрасить сиденье, спинку и подушки — иначе судить не о чем");

        CommandStack.Execute(new SetMaterialCommand(sofa, MaterialSlot.Legs, OtherDecorId));
        if (panelRefreshesTheHighlight) _selection.RefreshHighlight(sofa);
        Assume.That(sofa.SecondaryMaterialId, Is.EqualTo(OtherDecorId), "слот подушек обязан смениться");

        _selection.DeselectAll();

        foreach (var pillow in Cushions(sofa))
            Assert.AreSame(newCushionDecor, pillow.sharedMaterial,
                "подушка " + pillow.name + " после снятия выделения вернула СТАРЫЙ декор: снятие выделения "
                + "стёрло только что выбранный пользователем декор тем, что запомнило при выделении");
        Assert.AreSame(seatBefore, seat.sharedMaterial,
            "сиденье не вернулось к своему декору: слот обивки не менялся, и жёлтый на нём остаться не вправе");
        Assert.AreSame(backrestBefore, backrest.sharedMaterial, "и спинка тоже");
        Assert.AreEqual(OtherDecorId, sofa.SecondaryMaterialId, "а выбранный декор остался в элементе");
    }

    [Test]
    public void Fold_WhileSelected_ShowsTintedCushions_AndDeselectRestoresThem()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Bed);
        var cushions = Cushions(sofa);
        var before = new Material[cushions.Length];
        for (int i = 0; i < cushions.Length; i++) before[i] = cushions[i].sharedMaterial;
        Assume.That(cushions[0].gameObject.activeSelf, Is.False,
            "на кровати подушки сняты: именно это и делает случай особенным");

        _selection!.Select(sofa);
        sofa.SnapToStage(SofaStage.Folded);

        foreach (var pillow in cushions)
        {
            Assert.IsTrue(pillow.gameObject.activeSelf,
                "сложенный диван возвращает подушки на сиденье: " + pillow.name);
            Assert.IsTrue(WearsTheSelectionTint(pillow),
                "подушка " + pillow.name + " вернулась из-под кровати НЕ подкрашенной: выделенный диван "
                + "показал бы одну голую деталь среди жёлтых, хотя выделение обходит и скрытые части");
        }

        _selection.DeselectAll();

        for (int i = 0; i < cushions.Length; i++)
            Assert.AreSame(before[i], cushions[i].sharedMaterial,
                "подушка " + cushions[i].name + " осталась жёлтой после снятия выделения");
        Assert.IsFalse(WearsTheSelectionTint(Seat(sofa)), "и сиденье тоже вернулось");
    }

    [Test]
    public void Unfold_WhileSelected_ThenDeselect_RestoresTheHiddenCushions()
    {
        var sofa = Sofa();
        var cushions = Cushions(sofa);
        var before = new Material[cushions.Length];
        for (int i = 0; i < cushions.Length; i++) before[i] = cushions[i].sharedMaterial;

        _selection!.Select(sofa);
        sofa.SnapToStage(SofaStage.Bed);
        Assume.That(cushions[0].gameObject.activeSelf, Is.False, "на кровати подушки сняты");
        _selection.DeselectAll();

        for (int i = 0; i < cushions.Length; i++)
            Assert.AreSame(before[i], cushions[i].sharedMaterial,
                "скрытая подушка " + cushions[i].name + " осталась подкрашенной: снятие выделения "
                + "пропускает неактивные объекты, и при складывании жёлтые подушки вернулись бы на сиденье");
    }
}
