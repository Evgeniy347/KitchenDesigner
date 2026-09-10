using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Tests.Geometry;

/// <summary>Наведение красит сцену двумя красками — красный участок
/// (<see cref="PartHighlighter"/>) и зелёный призрак (<see cref="ScenePreview"/>) —
/// и у обеих один смысл: «то, над чем сейчас курсор». `docs/UI-GUIDELINES.md` §9
/// требует гасить их ОДНОЙ дверью: потребитель, позвавший только половину, оставляет
/// вторую висеть в сцене после того, как навёл мимо. <see cref="HoverPreviewGate"/> —
/// эта дверь; тест проверяет и её саму, и то, что единственный потребитель
/// (<c>PipePortHover</c>) ходит именно через неё, а не собирает пару звонков
/// заново на своём месте.</summary>
public class HoverPreviewGateTests
{
    private readonly System.Collections.Generic.List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        HoverPreviewGate.HideAll();
        CommandStack.Clear();
        foreach (var element in PartRegistry.GetAll())
            if (element != null) _spawned.Add(element.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private PipeElement Pipe()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 600, "Run", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    [Test]
    public void HideAll_ClearsBothTheRedRegion_AndTheGreenGhost()
    {
        var pipe = Pipe();
        PartHighlighter.ShowPipeEnd(pipe, PartEnd.Start);
        ScenePreview.Hover("dn25", () => ElementFactory.CreatePipeCap("ghost", Vector3.zero),
            null, pipe);

        Assume.That(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.Start)),
            Is.True, "красный участок обязан гореть до вызова двери");
        Assume.That(ScenePreview.IsShowing, Is.True, "и призрак обязан стоять до вызова двери");

        HoverPreviewGate.HideAll();

        Assert.IsFalse(PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.Start)),
            "одна дверь гасит красный участок");
        Assert.IsFalse(ScenePreview.IsShowing, "и она же гасит зелёного призрака — "
            + "оба состояния несут один смысл и обязаны гаснуть вместе");
    }

    [Test]
    public void PipePortHover_RoutesItsClear_ThroughTheOneDoor()
    {
        var source = File.ReadAllText(RepoPaths.Subdir("Assets", "Scripts", "Core", "UI",
            "PipePortHover.cs"));

        StringAssert.Contains("public void Clear() => HoverPreviewGate.HideAll();", source,
            "потребитель обязан гасить ОБА состояния одним вызовом — собранная на месте "
            + "пара ScenePreview.Leave()+PartHighlighter.Hide() это то же самое поведение, "
            + "но забытая наполовину она гасит только одну краску");
    }
}
