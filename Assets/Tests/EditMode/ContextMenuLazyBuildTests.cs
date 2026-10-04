using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Инспектор строится при первом открытии, а не в Build: его ~двадцать секций и тридцать
/// редакторов полей стоили ~0,37 с из ~0,5 с запуска интерфейса (замер 2026-10-04, PlayMode-сборка
/// Bootstrap), а открывают его не в каждом сеансе. Шапка и регистрация окна остаются в Build.
/// В тихие кадры после запуска остаток достраивается по шагу за кадр, чтобы первое открытие не
/// платило за всё сразу.
/// </summary>
public class ContextMenuLazyBuildTests
{
    private readonly List<GameObject> _made = new();

    [TearDown]
    public void DropWhatWasMade()
    {
        foreach (var go in _made)
            if (go != null) Object.DestroyImmediate(go);
        _made.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
    }

    private (ContextMenuUI ctx, Transform canvas) BuiltPanel(string name)
    {
        var canvasGo = new GameObject(name);
        _made.Add(canvasGo);
        canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        var ctx = canvasGo.AddComponent<ContextMenuUI>();
        ctx.Build(canvasGo.transform);
        return (ctx, canvasGo.transform);
    }

    private static List<string> NodeNames(Transform panel) =>
        panel.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();

    [Test]
    public void Build_LeavesTheRowsForTheFirstOpening()
    {
        var (ctx, canvas) = BuiltPanel("LazyInspector");

        Assert.IsNotNull(canvas.FindNode(InspectorNodes.Panel), "окно с шапкой есть сразу");
        Assert.IsNull(canvas.FindNode("CtxLock"), "строк инспектора до открытия нет");
        Assert.IsFalse(ctx.ContentBuilt);

        var board = ElementFactory.CreatePart(new Vector3Int(400, 400, 18), "LazyBoard", Vector3.zero)
            .GetComponent<KitchenElement>();
        ctx.Open(board);

        Assert.IsTrue(ctx.ContentBuilt);
        Assert.IsNotNull(canvas.FindNode("CtxLock"), "первое открытие строит строки");
        Assert.IsTrue(canvas.FindNode(InspectorNodes.Panel).IsShown());
    }

    [Test]
    public void ReadingAnInspectorSection_BuildsTheRowsItNeeds()
    {
        var (ctx, canvas) = BuiltPanel("LazyInspectorSection");

        Assert.IsNotNull(ctx.Rows);

        Assert.IsTrue(ctx.ContentBuilt, "секция, к которой обратились до открытия, не должна быть пустой оболочкой");
        Assert.IsNotNull(canvas.FindNode("CtxLock"));
    }

    [Test]
    public void PrewarmSteps_BuildTheSameRowsInSeveralFramesAsTheFirstOpeningBuildsAtOnce()
    {
        var (steppedCtx, steppedCanvas) = BuiltPanel("SteppedInspector");
        var (openedCtx, openedCanvas) = BuiltPanel("OpenedInspector");
        int steps = 0;
        while (steppedCtx.PrewarmStep(DeferredBuild.QuietFramesBeforePrewarm, false)) steps++;
        Assert.IsNotNull(openedCtx.Rows);

        Assert.Greater(steps, 3, "прогрев идёт по шагу за кадр, а не одним рывком");
        Assert.IsTrue(steppedCtx.ContentBuilt);
        CollectionAssert.AreEqual(
            NodeNames(openedCanvas.FindNode(InspectorNodes.Panel)),
            NodeNames(steppedCanvas.FindNode(InspectorNodes.Panel)),
            "ступенчатая сборка обязана дать то же дерево, что и сборка при открытии");
    }

    [Test]
    public void PrewarmStep_InTheFirstFramesAfterStartup_BuildsNothing()
    {
        var (ctx, _) = BuiltPanel("EarlyInspector");

        Assert.IsFalse(ctx.PrewarmStep(DeferredBuild.QuietFramesBeforePrewarm - 1, false));

        Assert.IsFalse(ctx.ContentBuilt, "запуск не должен платить за окно, которое ещё никто не открыл");
    }
}
