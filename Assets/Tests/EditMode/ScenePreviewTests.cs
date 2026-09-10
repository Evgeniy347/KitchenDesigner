using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Два прямых поведения `ScenePreview`, ниже уровня панели портов.
///
/// Первое — `Hover` со `spawn`, вернувшим `null`, раньше молча откатывался в
/// `Leave` и не гасил заменяемый объект: показать «как будет, если ничего не
/// поставить» было нечем. Теперь отсутствие призрака — тоже показ: заменяемое
/// гаснет, а `IsShowing` остаётся true, пока с пункта не ушли.
///
/// Второе — превью анкерится на хозяине порта в момент наведения; уедь хозяин
/// (перетаскивание, правка позиции) под открытым превью — без `Sync` призрак
/// остаётся висеть там, где хозяин был раньше.</summary>
public class ScenePreviewTests
{
    private readonly System.Collections.Generic.List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        ScenePreview.Leave();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private KitchenElement Owner()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 600, "Run", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private KitchenElement Replaced()
    {
        var go = ElementFactory.CreatePipeCap("Seated", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private static int EnabledRenderers(KitchenElement element)
    {
        int enabled = 0;
        foreach (var renderer in ElementRenderers.BodyOf(element))
            if (renderer != null && renderer.enabled) enabled++;
        return enabled;
    }

    [Test]
    public void Hover_WithASpawnThatReturnsNull_StillMutesTheReplacedObject()
    {
        var owner = Owner();
        var replaced = Replaced();
        int lit = EnabledRenderers(replaced);
        Assume.That(lit, Is.GreaterThan(0));

        ScenePreview.Hover("remove", () => null, replaced, owner);

        Assert.IsTrue(ScenePreview.IsShowing,
            "показ «ничего не будет» — тоже показ, а не тишина");
        Assert.IsNull(ScenePreview.Ghost, "спавнить нечего — призрака и не должно быть");
        Assert.AreEqual(0, EnabledRenderers(replaced),
            "без гашения заменяемого нечем показать «как будет, если выбрать „нет"»");

        ScenePreview.Leave();

        Assert.AreEqual(lit, EnabledRenderers(replaced),
            "ушли с пункта — погашенное обязано вернуться");
    }

    [Test]
    public void Sync_WithNoOwnerGiven_NeverThrows_AndLeavesTheGhostAlone()
    {
        ScenePreview.Hover("dn25", () => ElementFactory.CreatePipeCap("ghost", Vector3.zero),
            null);
        var pos = ScenePreview.Ghost!.transform.position;

        Assert.DoesNotThrow(ScenePreview.Sync,
            "превью, построенное без хозяина (owner не передан), не обязано знать, "
            + "за чем следить — Sync тогда просто ничего не делает");
        Assert.AreEqual(pos, ScenePreview.Ghost!.transform.position);
    }

    [Test]
    public void Sync_MovesTheGhost_ToFollowItsMovingOwner()
    {
        var owner = Owner();
        ScenePreview.Hover("dn25",
            () => ElementFactory.CreatePipeCap("ghost", owner.transform.position), null, owner);
        var before = ScenePreview.Ghost!.transform.position;

        owner.transform.position += new Vector3(0f, 0f, 2f);

        Assert.AreEqual(before, ScenePreview.Ghost!.transform.position,
            "хозяин уже уехал, но без Sync призрак ещё стоит на старом месте");

        ScenePreview.Sync();

        Assert.AreNotEqual(before, ScenePreview.Ghost!.transform.position,
            "Sync обязан пересобрать превью на новом месте хозяина");
    }
}
