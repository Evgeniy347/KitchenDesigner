using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Наведение красит сцену двумя красками, и обе живут ДОЛЬШЕ, чем повод, по
/// которому их зажгли. Здесь собраны те правила `docs/UI-GUIDELINES.md` §9, которые
/// говорят про КОНЕЦ показа, а не про его начало.
///
/// «Возвращать только то, что гасили сами» — половина правила про порендерное
/// гашение, и она держится не только на списке `Muted`: пока превью открыто, вид
/// мог погасить тот же рендерер сам («скрыть объекты»), а `SceneVisibilityManager
/// .Apply` мемоизирован по хэшу вида и на неизменившемся хэше выходит сразу. Общий
/// «включить всё» тогда зажигает спрятанное чем-то другим НАВСЕГДА — до следующей
/// смены вида. Поэтому снятие превью обязано сбросить эту память.
///
/// «Призрак — состояние вида» означает ещё и то, что он не тело: клик, двойной клик,
/// рулетка и пипетка бьют нефильтрованным `Physics.Raycast`, а призрак приходит из
/// фабрики через `ElementRoot.Prepare` с коллайдером и `Rigidbody`. Коллайдер,
/// оставленный на картинке, съедает клик по тому, что за ней, а тень призрака
/// попадает в фотокадр.
///
/// И устаревание: накладка следит не за «позой», а за ТЕМ, ЧТО НАРИСОВАЛА. Радиус
/// гильзы фитинга считается от `BoreSizeIds`, который меняется отдельно от
/// `DimensionsMM`; призрак висит на хозяине, которого `SceneMembership.Leave` гасит,
/// не разрушая компонент. Оба случая проходят мимо проверки «поза та же».</summary>
public class HoverOverlayContractTests
{
    private readonly List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        HoverPreviewGate.HideAll();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        SceneVisibilityManager.Invalidate();
        ElementFactory.ClearPools();
    }

    private PipeElement Pipe()
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 600, "Run", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement Elbow()
    {
        var go = ElementFactory.CreatePipeElbow("Elbow", new Vector3(2f, 0f, 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    private KitchenElement Cap()
    {
        var go = ElementFactory.CreatePipeCap("Seated", new Vector3(4f, 0f, 0f));
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

    private static float SleeveRadiusUnits()
    {
        var filter = HighlightOverlay.PieceObjects[0].GetComponent<MeshFilter>();
        return filter.sharedMesh.bounds.extents.x;
    }

    [Test]
    public void LeavingThePreview_ForgetsTheViewsMemo_SoTheViewCanHideTheObjectAgain()
    {
        var owner = Pipe();
        var replaced = Cap();
        SceneVisibilityManager.Apply();
        Assume.That(EnabledRenderers(replaced), Is.GreaterThan(0),
            "вид по умолчанию показывает объекты — иначе тесту не с чем сравнивать");

        ScenePreview.Hover("remove", () => null, replaced, owner);
        ScenePreview.Leave();

        SceneVisibility.SetRenderersEnabled(replaced, false);
        SceneVisibilityManager.Apply();

        Assert.Greater(EnabledRenderers(replaced), 0,
            "снятие превью зажигает рендереры ОБЩИМ enabled = true, а Apply мемоизирован "
            + "по хэшу вида: не сбросив память, превью навсегда оставляет видимым то, что "
            + "погасил не он, — до следующей смены состояния вида");
    }

    [Test]
    public void TheGhost_IsAPicture_NotABody()
    {
        var owner = Pipe();
        ScenePreview.Hover("cap",
            () => ElementFactory.CreatePipeCap("ghost", owner.transform.position), null, owner);

        var ghost = ScenePreview.Ghost;
        Assert.IsNotNull(ghost, "без призрака этот тест ничего не проверяет");

        var colliders = ghost!.GetComponentsInChildren<Collider>(true);
        Assume.That(colliders.Length, Is.GreaterThan(0),
            "фабрика ставит элементу коллайдер — если перестала, сенсор надо переписать");
        foreach (var collider in colliders)
            Assert.IsFalse(collider.enabled,
                "выделение, фокус по двойному клику, рулетка и пипетка бьют нефильтрованным "
                + "Physics.Raycast: живой коллайдер призрака съедает клик по тому, "
                + "что за ним, и уводит фокус на объект, которого нет в проекте");

        foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off,
                renderer.shadowCastingMode,
                "полупрозрачная картинка не имеет права отбрасывать тень — "
                + "в фотокадр попадёт тень детали, которой нет ни в файле, ни в ведомости");
    }

    [Test]
    public void Sync_DropsTheGhost_WhenItsOwnerLeavesTheProject()
    {
        var owner = Pipe();
        ScenePreview.Hover("cap",
            () => ElementFactory.CreatePipeCap("ghost", owner.transform.position), null, owner);
        Assume.That(ScenePreview.IsShowing, Is.True, "призрак обязан стоять до удаления хозяина");

        SceneMembership.Leave(owner.gameObject, owner);

        ScenePreview.Sync();

        Assert.IsFalse(ScenePreview.IsShowing,
            "SceneMembership.Leave гасит объект и снимает его с учёта, но КОМПОНЕНТ жив — "
            + "значит owner != null, а поза та же. Проверка «уехал ли хозяин» такое "
            + "удаление не видит, и зелёный призрак остаётся висеть на элементе, "
            + "которого больше нет в проекте");
        Assert.IsNull(ScenePreview.Ghost, "и сам объект призрака обязан быть разрушен");
    }

    [Test]
    public void Sync_DropsTheSleeve_WhenItsElementLeavesTheProject()
    {
        var pipe = Pipe();
        PartHighlighter.ShowPipeEnd(pipe, PartEnd.Start);
        Assume.That(HighlightOverlay.PieceCount, Is.GreaterThan(0));

        SceneMembership.Leave(pipe.gameObject, pipe);

        PartHighlighter.Sync();

        Assert.AreEqual(0, HighlightOverlay.PieceCount,
            "накладка и призрак обязаны устаревать по ОДНОМУ контракту: элемент "
            + "разрушен, погашен или снят с учёта — показывать больше нечего");
    }

    [Test]
    public void Sync_RebuildsTheSleeve_WhenTheBoreWidens_ThoughThePoseAndTheBoxDoNot()
    {
        var elbow = Elbow();
        PartHighlighter.ShowFittingMouth(elbow, 0);
        Assume.That(HighlightOverlay.PieceCount, Is.GreaterThan(0),
            "без гильзы сравнивать нечего");

        float before = SleeveRadiusUnits();
        var pose = elbow.transform.position;
        var box = elbow.DimensionsMM;

        Assume.That(elbow.TakeBoreSizes(new string?[] { PipeSpec.Dn50, PipeSpec.Dn50 }), Is.True,
            "проход обязан РАСШИРИТЬСЯ — иначе радиус и не должен меняться");
        Assume.That(elbow.transform.position, Is.EqualTo(pose));
        Assume.That(elbow.DimensionsMM, Is.EqualTo(box),
            "габаритная коробка ещё не пересчитана — ровно тот кадр, в котором прежний "
            + "Sync (поза + DimensionsMM) не видел никаких изменений");

        PartHighlighter.Sync();

        Assert.AreNotEqual(before, SleeveRadiusUnits(),
            "радиус устья считается от BoreSizeIds, а не от габаритов элемента: "
            + "следя за чужой тройкой, накладка оставляет красную гильзу "
            + "на радиусе прежнего прохода");
    }

    [Test]
    public void TheSleeve_OutgrowsThePartsOwnEndCap_InsteadOfLandingOnIt()
    {
        var pipe = Pipe();
        PartHighlighter.ShowPipeEnd(pipe, PartEnd.End);
        Assume.That(HighlightOverlay.PieceCount, Is.GreaterThan(0));

        var piece = HighlightOverlay.PieceObjects[0].transform;
        var mesh = HighlightOverlay.PieceObjects[0].GetComponent<MeshFilter>().sharedMesh;
        var tipWorld = piece.position + piece.up * (mesh.bounds.size.y * 0.5f);
        float tipMM = pipe.transform.InverseTransformPoint(tipWorld).y / AppConstants.MM_TO_UNITS;

        Assert.Greater(tipMM, pipe.LengthMM * 0.5f + HighlightOverlay.LiftMm * 0.5f,
            "крышка гильзы, севшая ровно в плоскость торца трубы и смотрящая туда же, — "
            + "пара копланарных граней, то есть мерцание (z-fighting). Радиус гильза "
            + "поднимает, а по оси обязана выпустить крышку за торец на тот же LiftMm");
    }

    [Test]
    public void PhotoMode_ClosesBothHoverPaints_NotOnlyTheRedOne()
    {
        var pipe = Pipe();
        PartHighlighter.ShowPipeEnd(pipe, PartEnd.Start);
        ScenePreview.Hover("cap",
            () => ElementFactory.CreatePipeCap("ghost", pipe.transform.position), null, pipe);

        Assume.That(HighlightOverlay.PieceCount, Is.GreaterThan(0),
            "красный участок обязан гореть до входа в фоторежим");
        Assume.That(ScenePreview.IsShowing, Is.True, "и призрак обязан стоять");

        PhotoMode.CloseToolsThatDrawOverlays();

        Assert.AreEqual(0, HighlightOverlay.PieceCount, "красный участок гаснет");
        Assert.IsFalse(ScenePreview.IsShowing,
            "фоторежим закрывает инструменты, рисующие накладки, ОДНОЙ дверью "
            + "HoverPreviewGate.HideAll: погасив только красное, он оставляет в кадре "
            + "полупрозрачный зелёный фитинг, которого нет ни в файле, ни в ведомости, "
            + "ни в реестре");
    }
}
