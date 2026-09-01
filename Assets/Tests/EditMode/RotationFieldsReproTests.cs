using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Рассинхрон кнопок «Повернуть на 90°» и полей «X, °», «Y, °», «Z, °».
///
/// Жалоба пользователя: элемент повёрнут на Y=90, жмём «X 90°» — и меняется
/// поле Z. Это не опечатка в коде: поворот хранится кватернионом, а углы Эйлера
/// раскладываются из него неоднозначно. Rx(90)·Ry(90) — это ровно Euler(0,90,90),
/// то есть «X» физически некуда было записать.
///
/// Поэтому кнопка теперь делает то же, что ручной ввод в поле: прибавляет 90° к
/// НАЗВАННОЙ оси показанной тройки углов и собирает поворот из неё
/// (<see cref="RotationSteps"/>). Показанная тройка живёт в
/// <see cref="RotationDisplayState"/> и пересобирается из кватерниона только
/// тогда, когда элемент повернул кто-то другой — снап, отмена, MCP.
/// </summary>
public class RotationFieldsReproTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private Transform _panel = null!;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
        _panel = _canvas!.transform.Find("ContextMenu")!;
    }

    [TearDown]
    public void TearDown()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        _menu = null;
        _canvas = null;
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
    }

    private KitchenElement Board(Vector3 euler)
    {
        var go = new GameObject("Board");
        _spawned.Add(go);
        var board = go.AddComponent<KitchenElement>();
        board.PartName = "Board";
        board.DimensionsMM = new Vector3Int(800, 400, 18);
        board.transform.rotation = Quaternion.Euler(euler);
        return board;
    }

    private void Click(string buttonName) =>
        _panel.Find(buttonName)!.GetComponent<Button>().onClick.Invoke();

    private float Shown(string axisLabel)
    {
        var field = _panel.Find("F_" + axisLabel + ", °")!.GetComponent<TMP_InputField>();
        return float.Parse(field.text, NumberStyles.Float, CultureInfo.CurrentCulture);
    }

    private void AssertShown(float x, float y, float z, string because)
    {
        Assert.AreEqual(x, Shown("X"), 0.05f, "поле «X, °»: " + because);
        Assert.AreEqual(y, Shown("Y"), 0.05f, "поле «Y, °»: " + because);
        Assert.AreEqual(z, Shown("Z"), 0.05f, "поле «Z, °»: " + because);
    }

    [Test]
    public void ContextMenu_RotX_AfterYawNinety_MovesTheXFieldAndLeavesZAlone()
    {
        _menu!.Open(Board(new Vector3(0f, 90f, 0f)));

        Click("CtxRotX");

        AssertShown(90f, 90f, 0f,
            "исходная жалоба — при Y=90 кнопка «X 90°» уводила именно Z, а X оставался нулём");
    }

    [Test]
    public void ContextMenu_RotZ_AfterYawNinety_MovesTheZField()
    {
        _menu!.Open(Board(new Vector3(0f, 90f, 0f)));

        Click("CtxRotZ");

        AssertShown(0f, 90f, 90f, "нажали «Z 90°» — растёт Z, и только он");
    }

    [Test]
    public void ContextMenu_RotY_AfterTiltOnX_MovesTheYField()
    {
        _menu!.Open(Board(new Vector3(90f, 0f, 0f)));

        Click("CtxRotY");

        AssertShown(90f, 90f, 0f, "X=90 — это гимбал-лок разложения ZXY, поле Y обязано пережить его");
    }

    [Test]
    public void ContextMenu_RotX_FourTimes_ReturnsTheFieldToZero()
    {
        var board = Board(Vector3.zero);
        _menu!.Open(board);

        for (int i = 0; i < 4; i++) Click("CtxRotX");

        AssertShown(0f, 0f, 0f, "четыре четверти оборота — полный круг");
        Assert.AreEqual(0f, Quaternion.Angle(board.transform.rotation, Quaternion.identity), 0.05f,
            "и сам элемент вернулся в исходную позу, а не только надпись");
    }

    [Test]
    public void ContextMenu_RotX_TurnsTheElementByExactlyNinetyDegrees()
    {
        var board = Board(new Vector3(0f, 90f, 0f));
        var before = board.transform.rotation;
        _menu!.Open(board);

        Click("CtxRotX");

        Assert.AreEqual(90f, Quaternion.Angle(before, board.transform.rotation), 0.05f,
            "кнопка обязана поворачивать элемент на 90°, а не только править поля");
        Assert.AreEqual(0f, Quaternion.Angle(board.transform.rotation,
            Quaternion.Euler(Shown("X"), Shown("Y"), Shown("Z"))), 0.05f,
            "поза элемента и показанные углы описывают один и тот же поворот");
    }

    [Test]
    public void ContextMenu_RotX_Undone_ShowsTheAnglesOfTheRestoredPose()
    {
        var board = Board(new Vector3(0f, 90f, 0f));
        _menu!.Open(board);
        Click("CtxRotX");

        CommandStack.Undo();
        _menu!.RefreshTransformFields();

        AssertShown(0f, 90f, 0f, "отмена вернула позу — поля обязаны показать её, а не остаться на 90");
    }

    [Test]
    public void ContextMenu_RotatedOutsideTheMenu_FieldsFollowTheElement()
    {
        var board = Board(Vector3.zero);
        _menu!.Open(board);
        Click("CtxRotX");

        board.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        _menu!.RefreshTransformFields();

        AssertShown(0f, 45f, 0f,
            "показанную тройку держим, только пока она описывает реальную позу — иначе пересобираем");
    }

    [Test]
    public void ContextMenu_AppliancesIgnoreTheHorizontalAxes()
    {
        var ovenGo = ElementFactory.CreateOven("Oven", Vector3.zero);
        _spawned.Add(ovenGo);
        var oven = ovenGo.GetComponent<KitchenElement>();
        _menu!.Open(oven);
        var before = oven.transform.rotation;

        Click("CtxRotX");
        Click("CtxRotZ");

        Assert.AreEqual(0f, Quaternion.Angle(before, oven.transform.rotation), 0.05f,
            "у встраиваемой техники осмыслен только разворот вокруг вертикали (FixedSize.IsYawOnly)");

        Click("CtxRotY");

        Assert.AreEqual(90f, Quaternion.Angle(before, oven.transform.rotation), 0.05f,
            "а вертикальная ось ей по-прежнему доступна");
    }
}
