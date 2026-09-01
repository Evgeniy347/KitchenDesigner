using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using KitchenDesigner.Core;

public class PhotoShadowCastersTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        PhotoShadowCasters.Restore();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private MeshRenderer SpawnRenderer(string name, int renderQueue)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        var r = go.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        r.sharedMaterial = new Material(shader) { renderQueue = renderQueue };
        return r;
    }

    [Test]
    public void Glass_LetsLightThrough_WhileOpaqueBlocksIt()
    {
        Assert.AreEqual(ShadowCastingMode.Off,
            PhotoShadowCasters.ShadowModeFor(SpawnRenderer("Glass", (int)RenderQueue.Transparent).sharedMaterial),
            "стекло не должно отбрасывать тень: иначе окно перестаёт пускать солнце в комнату");

        Assert.AreEqual(ShadowCastingMode.On,
            PhotoShadowCasters.ShadowModeFor(SpawnRenderer("Board", (int)RenderQueue.Geometry).sharedMaterial),
            "непрозрачная деталь перекрывает свет — на этом держится реализм закрытой двери");
    }

    [Test]
    public void IsTransparent_ReadsTheRenderQueue_NotTheMaterialName()
    {
        var opaque = SpawnRenderer("Opaque", (int)RenderQueue.Geometry).sharedMaterial;
        Assert.IsFalse(PhotoShadowCasters.IsTransparent(opaque));

        opaque.renderQueue = (int)RenderQueue.Transparent;
        Assert.IsTrue(PhotoShadowCasters.IsTransparent(opaque),
            "признак прозрачности — очередь отрисовки: стекло собирается через ElementHighlighter.MakeTransparent");

        Assert.IsFalse(PhotoShadowCasters.IsTransparent(null));
    }

    [Test]
    public void Restore_PutsBackTheWorkingModeItFoundBefore()
    {
        var glass = SpawnRenderer("GlassRestore", (int)RenderQueue.Transparent);
        var board = SpawnRenderer("BoardRestore", (int)RenderQueue.Geometry);
        glass.shadowCastingMode = ShadowCastingMode.On;
        board.shadowCastingMode = ShadowCastingMode.Off;

        PhotoShadowCasters.Enable();
        Assert.AreEqual(ShadowCastingMode.Off, glass.shadowCastingMode);
        Assert.AreEqual(ShadowCastingMode.On, board.shadowCastingMode);

        PhotoShadowCasters.Restore();

        Assert.AreEqual(ShadowCastingMode.On, glass.shadowCastingMode,
            "в рабочем режиме тени створок выключены ради производительности — выход обязан вернуть прежние значения");
        Assert.AreEqual(ShadowCastingMode.Off, board.shadowCastingMode);
    }

    [Test]
    public void Enable_Twice_StillRestoresTheOriginalMode()
    {
        var board = SpawnRenderer("BoardTwice", (int)RenderQueue.Geometry);
        board.shadowCastingMode = ShadowCastingMode.Off;

        PhotoShadowCasters.Enable();
        PhotoShadowCasters.Enable();
        PhotoShadowCasters.Restore();

        Assert.AreEqual(ShadowCastingMode.Off, board.shadowCastingMode,
            "повторный вход не должен запомнить уже изменённое значение как «прежнее»");
    }
}
