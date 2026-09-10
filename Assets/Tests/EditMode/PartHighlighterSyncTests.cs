using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Tests.Geometry;

/// <summary>`PartHighlighter.Sync()` работал только по совпадению: труба и
/// накладка-под-кромку красятся одним и тем же `HighlightOverlay`, и его
/// действительно синхронизировал `ContextMenuUI.Update()` — но звал он
/// `SideHighlighter.Sync()`, ни разу не назвав `PartHighlighter`. Уйди труба
/// в отдельную накладку — синхронизация трубы пропала бы, и ни один тест
/// этого не заметил бы, потому что ни один не звал `PartHighlighter.Sync()`
/// напрямую.
///
/// Здесь два слоя: поведенческий — что `Sync()` вообще делает — и сторож
/// проводки, который краснеет, если явный вызов в `ContextMenuUI.Update()`
/// снова исчезнет.</summary>
public class PartHighlighterSyncTests
{
    private readonly System.Collections.Generic.List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        PartHighlighter.Hide();
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
    public void Sync_HidesTheSleeve_WhenTheHighlightedPipeDisappears()
    {
        var pipe = Pipe();
        PartHighlighter.ShowPipeEnd(pipe, PartEnd.Start);
        Assume.That(
            PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.Start)),
            Is.True);

        foreach (var renderer in ElementRenderers.BodyOf(pipe))
            if (renderer != null) renderer.enabled = false;

        PartHighlighter.Sync();

        Assert.IsFalse(
            PartHighlighter.IsShown(pipe, PartHighlighter.PipeEndRegion(PartEnd.Start)),
            "труба спрятана (например, удалена своим набором команд) — гильза без Sync "
            + "осталась бы висеть в воздухе на месте, где трубы больше нет");
    }

    [Test]
    public void Sync_RebuildsTheSleeve_WhenTheHighlightedPipeMoves()
    {
        var pipe = Pipe();
        PartHighlighter.ShowPipeEnd(pipe, PartEnd.Start);
        var before = HighlightOverlay.PieceObjects[0].transform.position;

        pipe.transform.position += new Vector3(1f, 0f, 0f);
        PartHighlighter.Sync();

        Assert.AreNotEqual(before, HighlightOverlay.PieceObjects[0].transform.position,
            "труба сдвинулась — гильза обязана переехать вместе с ней, а не остаться "
            + "красной полосой в старой точке пространства");
    }

    [Test]
    public void ContextMenuUI_Update_KeepsThePipeHighlightInSync()
    {
        var source = File.ReadAllText(RepoPaths.Subdir("Assets", "Scripts", "Core", "UI",
            "ContextMenuUI.cs"));

        StringAssert.Contains("HoverPreviewGate.Sync();", source,
            "раньше подсветку трубы синхронизировал ТОЛЬКО побочный эффект "
            + "SideHighlighter.Sync() — оба потребителя красят один HighlightOverlay, "
            + "и вызов совпадения работал, пока накладка одна. Явный вызов "
            + "HoverPreviewGate.Sync() (который зовёт PartHighlighter.Sync() сам, "
            + "а не по случайности) обязан остаться в Update()");
    }
}
