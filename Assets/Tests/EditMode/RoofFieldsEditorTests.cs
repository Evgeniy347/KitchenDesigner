using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class RoofFieldsEditorTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    [SetUp]
    public void Setup()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private RoofElement Roof()
    {
        var go = ElementFactory.CreateRoof("Крыша", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<RoofElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_InputField Field(string node) =>
        Panel().Find($"F_{node}")!.GetComponent<TMP_InputField>();

    [Test]
    public void EditingTheOverhang_DoesNotRoundThePitchToAWholeDegree()
    {
        var roof = Roof();
        roof.PitchDeg = 30.4f;
        _menu!.Open(roof);

        Field(RoofFieldsEditor.OverhangNode).text = "500";
        Field(RoofFieldsEditor.OverhangNode).onEndEdit.Invoke("500");

        Assert.AreEqual(30.4f, roof.PitchDeg, 0.05f,
            "правка соседнего поля (свес) не должна округлять уклон крыши до целого градуса - " +
            "NumberFieldsEditor.Apply переписывает все поля редактора при любой правке");
    }

    [Test]
    public void ThePitchField_ShowsOneDecimalDigit()
    {
        var roof = Roof();
        roof.PitchDeg = 30.4f;
        _menu!.Open(roof);

        Assert.AreEqual(30.4f.ToString("F1"), Field(RoofFieldsEditor.PitchNode).text.Replace("​", ""),
            "UI-GUIDELINES §1: углы показываются с одним знаком после запятой");
    }
}
