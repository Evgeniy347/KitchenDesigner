using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class BoxWireframeTests
{
    [Test]
    public void Corners_AreEightUnitCubeCorners()
    {
        Assert.AreEqual(8, BoxWireframe.Corners.Length);
        foreach (var c in BoxWireframe.Corners)
        {
            Assert.AreEqual(0.5f, Mathf.Abs(c.x), 1e-6f, "угол по X = ±0.5");
            Assert.AreEqual(0.5f, Mathf.Abs(c.y), 1e-6f, "угол по Y = ±0.5");
            Assert.AreEqual(0.5f, Mathf.Abs(c.z), 1e-6f, "угол по Z = ±0.5");
        }
    }

    [Test]
    public void EdgeIndices_Describe12Edges_InRange()
    {
        Assert.AreEqual(BoxWireframe.EdgeCount * 2, BoxWireframe.EdgeIndices.Length, "12 рёбер * 2 индекса");
        foreach (var i in BoxWireframe.EdgeIndices)
            Assert.IsTrue(i >= 0 && i < 8, "индекс ссылается на существующий угол");
    }

    [Test]
    public void EdgeLengths_AreAllUnit()
    {
        var c = BoxWireframe.Corners;
        var e = BoxWireframe.EdgeIndices;
        for (int k = 0; k < e.Length; k += 2)
        {
            float len = Vector3.Distance(c[e[k]], c[e[k + 1]]);
            Assert.AreEqual(1f, len, 1e-6f, "ребро единичного куба = 1");
        }
    }

    [Test]
    public void WorldCorners_Identity_MatchLocal()
    {
        var into = new Vector3[8];
        BoxWireframe.WorldCorners(Matrix4x4.identity, into);
        for (int i = 0; i < 8; i++)
            Assert.Less(Vector3.Distance(into[i], BoxWireframe.Corners[i]), 1e-6f);
    }

    [Test]
    public void WorldCorners_ScaledAndTranslated_MapsBox()
    {
        // деталь 600×360×18 мм, смещённая и повёрнутая на 90° по Y.
        var pos = new Vector3(1f, 0.5f, -2f);
        var rot = ManagedRotation.Euler(0f, 90f, 0f);
        var scale = new Vector3(0.6f, 0.36f, 0.018f);
        var m = Matrix4x4.TRS(pos, rot, scale);

        var into = new Vector3[8];
        BoxWireframe.WorldCorners(m, into);

        // Габарит по мировым углам совпадает с ожидаемым после поворота (оси X↔Z).
        var min = into[0]; var max = into[0];
        foreach (var p in into) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        Assert.AreEqual(0.018f, max.x - min.x, 1e-4f, "после поворота ширина короба по X = толщина");
        Assert.AreEqual(0.36f, max.y - min.y, 1e-4f, "высота по Y сохраняется");
        Assert.AreEqual(0.6f, max.z - min.z, 1e-4f, "после поворота глубина по Z = ширина");
        // Центр короба = позиция детали.
        Assert.Less(Vector3.Distance((min + max) * 0.5f, pos), 1e-4f);
    }
}

public class ElementOutlineTests
{
    private readonly System.Collections.Generic.List<GameObject> _spawned =
        new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var stray in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (stray != null && stray.name == ElementOutline.OutlineRootName)
                Object.DestroyImmediate(stray.gameObject);
        PartRegistry.Clear();
    }

    private ElementOutline ShownOutlineOnARectangularPart(out Transform part)
    {
        var go = ElementFactory.CreatePart(
            new Vector3Int(600, 360, 18), "OutlinePart", new Vector3(1f, 0.5f, -2f));
        _spawned.Add(go);
        go.transform.rotation = ManagedRotation.Euler(0f, 30f, 0f);
        part = go.transform;

        var outline = ElementOutline.Ensure(go.GetComponent<KitchenElement>()!);
        Assert.IsNotNull(outline);
        outline!.Show(false);
        return outline;
    }

    [Test]
    public void EdgeBars_KeepTheirThickness_UnderANonUniformlyScaledPart()
    {
        var outline = ShownOutlineOnARectangularPart(out var part);

        Assert.IsNotNull(outline.Root);
        Assert.IsNull(outline.Root!.parent,
            "короб контура живёт в мировых координатах: став ребёнком детали, он унаследовал бы её масштаб");
        Assert.AreNotEqual(part.lossyScale.x, part.lossyScale.z,
            "деталь взята неравномерная — иначе наследование масштаба было бы незаметно");

        foreach (Transform seg in outline.Root!)
        {
            Assert.AreEqual(ElementOutline.EdgeThicknessMeters, seg.lossyScale.x, 1e-6f,
                "ребро одинаково толстое по любой оси — иначе контур на тонкой детали пропадает");
            Assert.AreEqual(ElementOutline.EdgeThicknessMeters, seg.lossyScale.y, 1e-6f);
        }
    }

    [Test]
    public void EdgeBars_SpanEveryEdgeOfThePartBox_AlongTheirLocalZ()
    {
        var outline = ShownOutlineOnARectangularPart(out var part);
        var expected = new[] { part.lossyScale.x, part.lossyScale.y, part.lossyScale.z };

        int[] found = { 0, 0, 0 };
        foreach (Transform seg in outline.Root!)
        {
            int match = -1;
            for (int i = 0; i < expected.Length; i++)
                if (Mathf.Abs(seg.localScale.z - expected[i]) < 1e-4f) match = i;

            Assert.GreaterOrEqual(match, 0,
                "длина бруска обязана совпасть с одним из трёх габаритов детали, а не с чем-то своим: "
                + seg.localScale.z);
            found[match]++;
        }

        foreach (int count in found)
            Assert.AreEqual(4, count, "у бокса по четыре ребра каждого габарита");
    }

    [Test]
    public void EdgeBars_CarryNoCollider_SoAClickStillHitsThePart()
    {
        var outline = ShownOutlineOnARectangularPart(out _);

        foreach (Transform seg in outline.Root!)
            Assert.IsNull(seg.GetComponent<Collider>(),
                "примитив-куб приносит BoxCollider: оставленный, он перехватывал бы клики вместо детали");
    }

    [Test]
    public void UnlitChain_StartsWithAShaderThatSurvivesTheBuild_NotWithUrpUnlit()
    {
        var chain = ElementOutline.UnlitShaderChain;

        Assert.AreEqual(ElementOutline.PrimaryShaderName, chain[0],
            "контур не должен зависеть от освещения, и URP/Unlit этого НЕ обеспечивает: "
            + "им не пользуется ни один материал проекта, поэтому в собранном плеере его "
            + "нет вовсе (проверено grep-ом по дереву сборки), Shader.Find возвращает там "
            + "null, и контур молча уезжал на URP/Lit — то есть темнел вместе с комнатой");
        Assert.IsNotNull(Resources.Load<Shader>(ElementOutline.PrimaryShaderResourcePath),
            "первое звено обязано лежать в Resources: только так стриппинг его не тронет, "
            + "иначе мы поменяли одно вырезаемое звено на другое");
        CollectionAssert.Contains(chain, "Universal Render Pipeline/Lit",
            "последним запасом остаётся шейдер самих деталей — его вырезать нельзя");
        Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Lit"),
            "запасной шейдер обязан существовать в проекте, иначе запас фиктивный");
    }

    [Test]
    public void UnlitOutline_ResolvesToTheUnlitShader_NotToTheLitFallback()
    {
        var shader = ElementOutline.FindUnlitShader();
        Assert.IsNotNull(shader,
            "контур не имеет права исчезнуть из-за отсутствующего шейдера");
        Assert.AreEqual(ElementOutline.PrimaryShaderName, shader!.name,
            "разрешаться обязано ПЕРВОЕ звено: запас существует на случай беды, "
            + "а не как обычный путь — именно молчаливый уход на запас и был дефектом");

        var m = ElementOutline.MakeUnlit(Color.black);
        Assert.IsNotNull(m, "материал контура обязан создаваться");
        Assert.AreEqual(ElementOutline.PrimaryShaderName, m!.shader.name,
            "этот материал получают все двенадцать рёбер контура");
        Object.DestroyImmediate(m);
    }
}
