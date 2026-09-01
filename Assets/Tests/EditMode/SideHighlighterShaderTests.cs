using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class SideHighlighterShaderTests
{
    private static readonly Vector3Int ShelfDims = new Vector3Int(800, 18, 400);

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement CreatePart(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        return element;
    }

    [TearDown]
    public void TearDown()
    {
        SideHighlighter.Hide();
        SideHighlighter.MaterialFactory = null;
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void HighlightShader_PrefersOverlayLine_WhichLivesInResourcesAndSurvivesStripping()
    {
        var shader = SideHighlighter.FindHighlightShader();

        Assert.IsNotNull(shader, "без шейдера подсветки стороны не будет вообще");
        Assert.AreEqual(SideHighlighter.PrimaryShaderName, shader!.name,
            "основной шейдер — накладка рулетки: ZTest Always, чтобы подсветка читалась и когда "
            + "торец прижат к соседней детали, и когда сторона смотрит от камеры");
        Assert.IsNotNull(Resources.Load<Shader>(SideHighlighter.PrimaryShaderResourcePath),
            "шейдер обязан лежать в Resources: только так стриппинг его не тронет, "
            + "а Shader.Find не видит шейдер, не использованный ни одним материалом сцены");
    }

    [Test]
    public void FallbackShaders_ExcludeTheBuiltInPipelineOnes_AndKeepTheOneThePartsUse()
    {
        var chain = SideHighlighter.FallbackShaderNames;

        CollectionAssert.Contains(chain, "Universal Render Pipeline/Lit",
            "URP/Unlit вырезается стриппингом, если им не пользуется ни один материал проекта; "
            + "последним запасом обязан быть шейдер самих деталей");
        CollectionAssert.DoesNotContain(chain, "Unlit/Color",
            "регрессия из Player.log: Unlit/Color — шейдер встроенного пайплайна, в URP-сборке его "
            + "нет вовсе, и конструктор материала падал ArgumentNullException на каждое наведение");
        CollectionAssert.DoesNotContain(chain, "Standard",
            "Standard — тоже встроенный пайплайн, в URP-билде его нет");
    }

    [Test]
    public void OverlayLineMaterial_KeepsTheShaderOwnQueue_InsteadOfBeingForcedToTransparent()
    {
        SideHighlighter.MaterialFactory = null;
        var m = SideHighlighter.HighlightMaterial();

        Assert.IsNotNull(m);
        Assume.That(m!.shader.name, Is.EqualTo(SideHighlighter.PrimaryShaderName));
        Assert.AreEqual(m.shader.renderQueue, m.renderQueue,
            "Hidden/OverlayLine уже настроен как надо и держит свою очередь — перебивать её вредно");
        Assert.AreNotEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, m.renderQueue,
            "очередь шейдера накладки строго позже обычной прозрачной");
    }

    [Test]
    public void MakeSeeThrough_SetsBlendStates_BecauseSurfaceFlagAloneLeavesUrpOpaque()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Assert.IsNotNull(shader);
        var m = new Material(shader);
        try
        {
            SideHighlighter.MakeSeeThrough(m);

            Assert.AreEqual(1f, m.GetFloat("_Surface"), 1e-4f);
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.SrcAlpha, m.GetFloat("_SrcBlend"), 1e-4f,
                "у URP мало выставить _Surface: без режимов смешивания материал остаётся непрозрачным, "
                + "и под накладкой не читается текстура детали");
            Assert.AreEqual((float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, m.GetFloat("_DstBlend"), 1e-4f);
            Assert.AreEqual(0f, m.GetFloat("_ZWrite"), 1e-4f);
            Assert.IsTrue(m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, m.renderQueue);
        }
        finally
        {
            Object.DestroyImmediate(m);
        }
    }

    [Test]
    public void QuadMesh_CarriesTheHighlightColourInItsVertices()
    {
        var mesh = SideHighlighter.QuadMesh();

        Assert.AreEqual(4, mesh.vertexCount, "единичный квад в плоскости XY");
        var colors = mesh.colors;
        Assert.AreEqual(4, colors.Length,
            "цвет лежит в ВЕРШИНАХ — именно его берёт Hidden/OverlayLine, материал его не задаёт");
        foreach (var c in colors)
            Assert.AreEqual(KitchenDesigner.Core.UI.UIStyle.EdgeHighlight3D, c,
                "цвет подсветки приходит из UIStyle, а не из локальной константы");
    }

    [Test]
    public void OppositeFace_PairsTheTwoEndsOfEachAxis()
    {
        for (int face = 0; face < Face.BoxFaceCount; face++)
        {
            int opposite = SideHighlighter.OppositeFaceOf(face);
            Assert.AreNotEqual(face, opposite);
            Assert.AreEqual(face, SideHighlighter.OppositeFaceOf(opposite), "пара взаимна");
            Assert.AreEqual(face / 2, opposite / 2,
                "порядок граней — контракт: index/2 = ось, чётный индекс = положительное направление");
        }
    }

    [Test]
    public void Quads_AreLiftedOffTheFace_SoTheyDoNotFightThePartForTheZBuffer()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        SideHighlighter.MaterialFactory = () => material;
        try
        {
            var shelf = CreatePart("LiftShelf", ShelfDims);
            var layout = EdgeBanding.LayoutOf(ShelfDims);
            int faceIndex = layout.FaceIndex(EdgeSide.W1);
            var end = shelf.GetFaces()[faceIndex];

            SideHighlighter.ShowFace(shelf, faceIndex);
            var quad = SideHighlighter.QuadObjects[0];

            float lift = Vector3.Dot(quad.transform.position - end.center, end.normal);
            Assert.AreEqual(SideHighlighter.LiftMm * AppConstants.MM_TO_UNITS, lift, 1e-6f,
                "накладка приподнята над гранью, иначе её съедает z-fighting с самой деталью");
            Assert.Greater(lift, 0f);
        }
        finally
        {
            SideHighlighter.MaterialFactory = null;
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void Bands_ArePressedAgainstTheEndTheyMark_NotCentredOnTheirFace()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        SideHighlighter.MaterialFactory = () => material;
        try
        {
            var shelf = CreatePart("BandShelf", ShelfDims);
            var layout = EdgeBanding.LayoutOf(ShelfDims);
            int faceIndex = layout.FaceIndex(EdgeSide.W1);
            var faces = shelf.GetFaces();
            var end = faces[faceIndex];

            SideHighlighter.ShowEdgeSide(shelf, EdgeSide.W1);
            var quads = SideHighlighter.QuadObjects;
            Assert.AreEqual(5, quads.Count, "торец + 4 полосы");

            for (int i = 1; i < quads.Count; i++)
            {
                float towardsEnd = Vector3.Dot(
                    quads[i].transform.position - shelf.transform.position, end.normal);
                Assert.Greater(towardsEnd, 0f,
                    "полоса прижата к тому краю соседней грани, который граничит с торцом: "
                    + "кайма посреди пласти не показывает, ГДЕ сторона");
            }
        }
        finally
        {
            SideHighlighter.MaterialFactory = null;
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void BandDepth_TakesThePercent_UntilItHitsTheCap()
    {
        float small = 0.12f;
        Assert.AreEqual(small * SideHighlighter.BandFraction, SideHighlighter.BandDepthOn(small), 1e-6f,
            "на мелкой детали работает процент от размера грани");

        float large = 0.8f;
        Assert.AreEqual(SideHighlighter.BandMaxMm * AppConstants.MM_TO_UNITS,
            SideHighlighter.BandDepthOn(large), 1e-6f,
            "на крупной детали 20 % — это пол-пласти: кайма перестала бы читаться как кайма");
    }
}
