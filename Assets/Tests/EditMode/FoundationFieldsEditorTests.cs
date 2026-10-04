using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

public class FoundationFieldsEditorTests
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
        if (_menu != null) { _menu!.Close(); _menu!.TestHooks.ForgetSectionStates(); }
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private FoundationElement Foundation()
    {
        var go = ElementFactory.CreateFoundation(700, 2300, "Фундамент", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<FoundationElement>();
    }

    private Transform Panel() => _canvas!.transform.FindNode("ContextMenu");

    [Test]
    public void FrostDepthField_DoesNotShowTheUnitTwice()
    {
        var foundation = Foundation();
        _menu!.Open(foundation);

        var value = Panel().FindNode("Val_" + FoundationFieldsEditor.FrostDepthLabel)!.GetComponent<TMP_Text>();

        Assert.AreEqual(foundation.FrostDepthText, value.text,
            "строка «только чтение» показывает FrostDepthText как есть - тот уже несёт свою единицу (или прочерк)");
        Assert.IsNull(Panel().FindNode("F_" + FoundationFieldsEditor.FrostDepthLabel),
            "вычисляемое значение - текст без рамки и без серого суффикса, который задвоил бы единицу " +
            "(«1079 мм мм», «> 2500 мм мм», «— мм»)");
    }
}
