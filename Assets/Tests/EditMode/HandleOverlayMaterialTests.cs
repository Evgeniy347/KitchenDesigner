using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

/// <summary>
/// Ручки трансформации обязаны рисоваться поверх всего: стрелка может оказаться
/// внутри соседней детали, и невидимую ручку нечем ухватить. Держится это ровно
/// одним фактом — материал ручки несёт Hidden/KD/HandleOverlay с ZTest Always.
///
/// Проверять это нужно именно тестом, потому что отказ БЕСШУМНЫЙ: материал
/// создаётся, стрелка рисуется, цвет правильный — меняется только глубина, и
/// в логе не появляется ни строчки. Раньше FindShader() при ненайденном
/// overlay-шейдере молча подставлял URP/Unlit, URP/Lit или Sprites/Default —
/// все три с честной проверкой глубины, то есть ровно то состояние, на которое
/// жалуется пользователь. Запасной цепочки больше нет, и
/// FindShader_HasNoSilentFallbackToADepthTestedShader сторожит её отсутствие.
/// </summary>
public class HandleOverlayMaterialTests
{
    private const int OverlayQueue = 4000;

    private static readonly int[] Axes = { 0, 1, 2 };

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private static string SourcePath(string relative) =>
        Path.Combine(Application.dataPath, relative);

    [Test]
    public void OverlayShader_IsFoundByName_AndAlsoLivesInResources()
    {
        var byName = Shader.Find(HandleMaterials.ShaderName);
        var fromResources = Resources.Load<Shader>(HandleMaterials.ShaderResourcePath);

        Assert.IsNotNull(byName,
            "без overlay-шейдера ручек не видно вовсе — это отказ, а не мелочь");
        Assert.IsNotNull(fromResources,
            "шейдер обязан лежать в Resources: Shader.Find не видит шейдер, "
            + "который не использован ни одним материалом сцены, а сборка кладёт "
            + "в билд всё содержимое Resources целиком");
        Assert.AreEqual(HandleMaterials.ShaderName, HandleMaterials.FindShader()!.name,
            "поиск обязан возвращать именно overlay-шейдер, а не что-то похожее");
    }

    [Test]
    public void OverlayShaderSource_DeclaresZTestAlways_ZWriteOff_AndTheOverlayQueue()
    {
        string path = SourcePath("Resources/Shaders/HandleOverlay.shader");
        Assert.IsTrue(File.Exists(path),
            "скан по несуществующему пути зелен и не проверяет ничего — "
            + "сперва убеждаемся, что файл там, где мы его ищем");
        string source = File.ReadAllText(path);

        StringAssert.Contains("ZTest Always", source,
            "именно ZTest Always делает ручку видимой внутри чужой геометрии; "
            + "из C# состояние прохода не прочитать, поэтому сторожим исходник");
        StringAssert.Contains("ZWrite Off", source,
            "ручка не должна писать глубину: иначе она сама начнёт заслонять сцену");
        StringAssert.Contains("\"Queue\" = \"Overlay\"", source,
            "очередь Overlay рисует ручки после всей непрозрачной геометрии");
    }

    [Test]
    public void FindShader_HasNoSilentFallbackToADepthTestedShader()
    {
        string path = SourcePath("Scripts/Core/Snap/Handles/HandleMaterials.cs");
        Assert.IsTrue(File.Exists(path), "скан по несуществующему пути ничего не проверяет");
        string source = File.ReadAllText(path);

        Assert.IsFalse(source.Contains("Shader.Find(\"Universal Render Pipeline"),
            "URP/Unlit и URP/Lit делают ЧЕСТНУЮ проверку глубины: подставить их "
            + "вместо накладки — значит тихо выключить инструмент, а не подстраховаться");
        Assert.IsFalse(source.Contains("Sprites/Default"),
            "та же беда: запасной шейдер без ZTest Always — это отказ без единой "
            + "строчки в логе");
        StringAssert.Contains("ShaderMissingMessage", source,
            "ненайденный шейдер обязан жаловаться вслух — единственный оставшийся "
            + "исход, кроме правильного");
    }

    [Test]
    public void HandleMaterial_ForEveryAxis_CarriesTheOverlayShaderAndItsQueue()
    {
        foreach (int axis in Axes)
        {
            var m = HandleMaterials.For(HandleMaterials.ForAxis(axis));

            Assert.IsNotNull(m, $"ось {axis}: без материала стрелки не будет");
            Assert.AreEqual(HandleMaterials.ShaderName, m!.shader.name,
                $"ось {axis}: подменённый шейдер — это ручка с обычной проверкой "
                + "глубины, тонущая в соседней детали");
            Assert.AreEqual(OverlayQueue, m.renderQueue,
                $"ось {axis}: очередь Overlay задана шейдером, перебивать её нельзя");
        }
    }

    [Test]
    public void HandleMaterial_ThreeAxes_ThreeDistinctColours_OnOneSharedShader()
    {
        var x = HandleMaterials.For(HandleMaterials.ForAxis(0));
        var y = HandleMaterials.For(HandleMaterials.ForAxis(1));
        var z = HandleMaterials.For(HandleMaterials.ForAxis(2));

        Assert.AreNotSame(x, y, "оси различаются цветом, значит и материалом");
        Assert.AreNotSame(y, z, "иначе кэш по цвету склеил бы две оси в одну");
        var green = y!.GetColor("_BaseColor");
        Assert.AreEqual(HandleMaterials.AxisY.r, green.r, 1e-3f,
            "цвет живёт в _BaseColor: у накладки нет _Color, и Material.color в неё не попадает");
        Assert.AreEqual(HandleMaterials.AxisY.g, green.g, 1e-3f);
        Assert.AreEqual(HandleMaterials.AxisY.b, green.b, 1e-3f);
    }

    [Test]
    public void Arrow_BothItsPieces_CarryTheOverlayMaterial()
    {
        var root = new GameObject("Arrow");
        _spawned.Add(root);
        var material = HandleMaterials.For(HandleMaterials.ForAxis(1));

        HandleVisual.BuildArrow(root.transform, material, HandleMetrics.Resize,
            HandleShaft.Cylinder, HandleTip.Cone);

        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        Assert.AreEqual(2, renderers.Length,
            "стрелка — ДВА рендерера, ствол и наконечник: накладку получить обязаны оба, "
            + "иначе половина ручки утонет, а половина останется поверх");
        foreach (var r in renderers)
        {
            Assert.IsNotNull(r.sharedMaterial, $"{r.gameObject.name}: материал не назначен");
            Assert.AreEqual(HandleMaterials.ShaderName, r.sharedMaterial.shader.name,
                $"{r.gameObject.name}: рисуется не накладкой");
        }
    }
}
