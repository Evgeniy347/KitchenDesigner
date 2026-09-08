using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Что осталось СЦЕНОВЫМ у ДВП/ХДФ после выноса модели в PanelBody.
///
/// Геометрия зазоров уехала в PanelBodyTests и считается под dotnet. Здесь —
/// то, что без сцены не проверяется:
/// 1) оболочка не разошлась с моделью (зеркало: компонент и его Body дают
///    одни и те же вершины, грани и признаки) — в покое и во время attach-ride,
///    когда transform несёт анимированную позу, а не логическую (см.
///    AttachRider.cs); Body обязан идти через тот же парный API
///    ValidationPositionAt/ValidationRotation, что и GetVerticesAt/GetFacesAt,
///    а не читать transform напрямую — первая версия делала именно это и молча
///    расходилась со сценой на ridden-элементе, потому что мирорный тест выше
///    не ставил панель в ridden-состояние;
/// 2) способность «иметь паз» вычисляется базовым классом через тип и
///    GetComponent, то есть по сцене, а не по данным панели;
/// 3) круговой рейс через ElementData: JsonUtility под CoreCLR не компилируется
///    вовсе (он в UnityEngine.JSONSerializeModule), поэтому остаётся здесь.</summary>
public class PanelElementTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private PanelElement MakePanel(Vector3Int dims, int gap = PanelElement.DEFAULT_GAP_MM)
    {
        var go = new GameObject("ДВП");
        _spawned.Add(go);
        var panel = go.AddComponent<PanelElement>();
        panel.PartName = "ДВП";
        panel.DimensionsMM = dims;
        panel.SetUniformGap(gap);
        return panel;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    /// <summary>Шов держится, только пока компонент и модель дают одно и то же.
    /// Оболочка берёт масштаб из transform.localScale, модель — из миллиметров;
    /// эти два источника обязаны совпадать, иначе быстрые тесты меряют не то,
    /// что рисует приложение.</summary>
    [Test]
    public void Panel_MirrorsItsBody_InVerticesAndFaces()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 2);
        panel.transform.SetPositionAndRotation(
            new Vector3(1.5665f, 1.81f, -2.566f), ManagedRotation.Euler(0f, 90f, 0f));

        var body = panel.Body;

        var fromScene = panel.GetVertices();
        var fromModel = body.Vertices();
        Assert.AreEqual(fromScene.Length, fromModel.Length);
        for (int i = 0; i < fromScene.Length; i++)
            Assert.AreEqual(0f, Vector3.Distance(fromScene[i], fromModel[i]), 1e-5f,
                $"вершина {i}: компонент и модель разошлись");

        var sceneFaces = panel.GetFaces();
        var modelFaces = body.Faces();
        Assert.AreEqual(sceneFaces.Length, modelFaces.Length);
        for (int i = 0; i < sceneFaces.Length; i++)
        {
            Assert.AreEqual(0f, Vector3.Distance(sceneFaces[i].center, modelFaces[i].center), 1e-5f,
                $"центр грани {i}");
            Assert.AreEqual(0f, Vector2.Distance(sceneFaces[i].size, modelFaces[i].size), 1e-5f,
                $"размер грани {i}");
        }
    }

    /// <summary>«Riding» means transform.position/rotation carry the ANIMATED pose
    /// while GetVerticesAt/GetFacesAt read the logical rest pose instead (see
    /// AttachRider.cs and KitchenElement.ValidationPositionAt/ValidationRotation).
    /// Body must resolve through that same pair, not through raw transform — else the
    /// mirror above only holds because it never rides.</summary>
    [Test]
    public void Panel_WhileAttachRidden_StillMirrorsGetVerticesAt_NotRawTransform()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 2);
        var restPos = new Vector3(1.5665f, 1.81f, -2.566f);
        var restRot = ManagedRotation.Euler(0f, 90f, 0f);
        panel.transform.SetPositionAndRotation(restPos, restRot);

        panel.BeginAttachRide(restPos, restRot);
        panel.transform.SetPositionAndRotation(
            restPos + new Vector3(0.4f, 0f, 0f), Quaternion.identity);

        var fromScene = panel.GetVertices();
        var fromModel = panel.Body.Vertices();
        Assert.AreEqual(fromScene.Length, fromModel.Length);
        for (int i = 0; i < fromScene.Length; i++)
            Assert.AreEqual(0f, Vector3.Distance(fromScene[i], fromModel[i]), 1e-5f,
                $"вершина {i}: во время attach-ride Body обязан читать rest-позу, а не transform");
    }

    [Test]
    public void Panel_MirrorsItsBody_InGapsAndTraits()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 1);

        Assert.AreEqual(PanelBody.DEFAULT_GAP_MM, PanelElement.DEFAULT_GAP_MM);
        Assert.AreEqual(PanelBody.DISPLAY_TYPE_NAME, panel.DisplayTypeName);
        Assert.AreEqual(PanelBody.CUTOUT_ROLE, panel.CutoutRole);
        Assert.AreEqual(PanelBody.SUPPORTS_GAPS, panel.SupportsGaps);
        Assert.AreEqual(panel.GapMM, panel.Body.GapMM);
        Assert.AreEqual(panel.DimensionsMM, panel.Body.DimensionsMM);
    }

    [Test]
    public void Panel_DoesNotSupportGrooves()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3));
        Assert.IsFalse(panel.SupportsGrooves, "ДВП вставляется В паз, своих пазов не имеет");
        Assert.IsFalse(panel.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)));
    }

    [Test]
    public void Panel_RoundTripsThroughElementData()
    {
        var panel = MakePanel(new Vector3Int(383, 376, 3), gap: 1);

        var data = ElementCapture.FromElement(panel);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));

        Assert.IsTrue(restored.isPanel);
        Assert.IsFalse(restored.isFacade, "ДВП не должна грузиться как фасад");
        Assert.AreEqual(1, restored.gapLeft);
        Assert.AreEqual(1, restored.gapRight);
        Assert.AreEqual(1, restored.gapTop);
        Assert.AreEqual(1, restored.gapBottom);
        Assert.AreEqual(new Vector3Int(383, 376, 3), restored.Dimensions);
    }

}
