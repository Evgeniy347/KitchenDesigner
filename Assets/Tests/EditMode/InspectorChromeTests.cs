using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>
/// Шапка, прибитый футер и высота панели свойств (docs/ui-redesign/inspector.md, пп. 6 и 8):
/// «Дублировать» слева, «Удалить» справа контуром и вне прокрутки, заголовок «Тип — Имя» с правильным
/// типом, «Удалить» видна без прокрутки у стены с шестью текстурами на 1366×768.
/// </summary>
public class InspectorChromeTests
{
    private const float BaseScreenW = 1366f;
    private const float BaseScreenH = 768f;

    private readonly List<GameObject> _spawned = new();
    private Canvas? _canvas;
    private ContextMenuUI? _menu;

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var fit = _canvas!.GetComponent<UiScaleFit>();
        fit.EmulatedScreen = new Vector2(BaseScreenW, BaseScreenH);
        fit.Apply();
        Canvas.ForceUpdateCanvases();
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
        ConfirmDeleteButton.DisarmAll();
    }

    [TearDown]
    public void Teardown()
    {
        ConfirmDeleteButton.DisarmAll();
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    private RectTransform Panel() => (RectTransform)_canvas!.transform.FindNode("ContextMenu");

    private KitchenElement MakeWall(string name, int overlays)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = new Vector3Int(3000, 2500, 100);
        go.AddComponent<Wall>();
        _spawned.Add(go);
        var list = new List<TextureOverlaySpec>();
        for (int i = 0; i < overlays; i++)
            list.Add(i == 0 ? TextureOverlaySpec.FullFace(OverlaySide.A, "oak")
                : new TextureOverlaySpec(OverlaySide.B, "white", 50 * i, 50, 600, 600));
        el.SetTextureOverlays(list);
        return el;
    }

    [Test]
    public void Title_NamesTheWallAsAWall_NotAsAPart()
    {
        var wall = MakeWall("Stena_3000x2500", 0);
        _menu!.Open(wall);

        Assert.AreEqual("Стена — Stena_3000x2500", _menu!.Chrome!.Title.text,
            "решение пользователя: заголовок остаётся «Тип — Имя», но тип у стены — «Стена», а не «Деталь»");
    }

    [Test]
    public void Title_IsLeftAligned_AndEllipsisOnOverflow()
    {
        var wall = MakeWall("Stena_3000x2500", 0);
        _menu!.Open(wall);

        var title = _menu!.Chrome!.Title;
        Assert.AreEqual(TextOverflowModes.Ellipsis, title.overflowMode, "длинное имя режется многоточием");
        Assert.IsTrue((title.alignment & TextAlignmentOptions.Left) != 0, "D5: заголовок слева");
    }

    [Test]
    public void Footer_HoldsDuplicateOnTheLeft_AndDeleteOnTheRight_OutsideTheScroll()
    {
        _menu!.Open(MakeWall("Стена", 0));
        var panel = Panel();
        var dup = (RectTransform)panel.FindNode("CtxDup");
        var del = (RectTransform)panel.FindNode("CtxDel");

        Assert.IsNull(dup.GetComponentInParent<ScrollRect>(), "«Дублировать» не прокручивается вместе с телом");
        Assert.IsNull(del.GetComponentInParent<ScrollRect>(), "«Удалить» не прокручивается вместе с телом");
        Assert.Less(dup.anchorMin.x, 0.5f, "«Дублировать» слева");
        Assert.Greater(del.anchorMin.x, 0.5f, "«Удалить» справа");
    }

    [Test]
    public void Delete_IsAnOutline_UntilArmed_ThenFilledRed()
    {
        var wall = MakeWall("Стена", 0);
        _menu!.Open(wall);
        var del = Panel().FindNode("CtxDel").GetComponent<Button>();
        var outline = del.transform.Find(ButtonRoles.OutlineNode);
        Assert.IsNotNull(outline, "контур DangerText: красная заливка кричит громче полезного");
        Assert.IsTrue(outline.gameObject.activeSelf);
        Assert.AreNotEqual(UIStyle.Danger, ((Image)del.targetGraphic).color, "до взвода кнопка не залита");

        del.onClick.Invoke();

        Assert.AreEqual(UIStyle.Danger, ((Image)del.targetGraphic).color, "заливка Danger — только во взводе ?!");
        Assert.AreEqual(UIStyle.GlyphConfirm, del.GetComponentInChildren<TMP_Text>().text);
        Assert.IsNotNull(wall, "первый клик ещё ничего не удалил");
        Assert.IsTrue(wall.gameObject.activeSelf);
    }

    [Test]
    public void Delete_SecondClick_RemovesTheElementAndClosesThePanel()
    {
        var wall = MakeWall("Стена", 0);
        _menu!.Open(wall);
        var del = Panel().FindNode("CtxDel").GetComponent<Button>();

        del.onClick.Invoke();
        del.onClick.Invoke();
        Assert.IsFalse(_menu!.IsOpen, "удалённый элемент — панель закрылась");
    }

    [Test]
    public void WallWithSixTextures_KeepsTheFooterOnScreen_AtTheScaleFloor()
    {
        _menu!.Open(MakeWall("Стена", 6));
        var panel = Panel();
        var canvas = (RectTransform)_canvas!.transform;

        float budget = canvas.rect.height - UIStyle.ToolbarH - UIStyle.StatusBarH - 2f * UIStyle.Space4;
        Assert.LessOrEqual(panel.sizeDelta.y, budget + 0.5f,
            "панель не выше формулы §7 от канвы: нижние «Дублировать»/«Удалить» не уходят за край (D1)");

        var corners = new Vector3[4];
        ((RectTransform)panel.FindNode("CtxDel")).GetWorldCorners(corners);
        var bottomLocal = canvas.InverseTransformPoint(corners[0]);
        Assert.GreaterOrEqual(bottomLocal.y, canvas.rect.yMin, "кнопка «Удалить» внутри экрана без прокрутки");

        var scroll = panel.GetComponentInChildren<ScrollRect>(true);
        Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height,
            "у шести текстур содержимое длиннее окна: прокручивается тело, а не футер");
    }
}
