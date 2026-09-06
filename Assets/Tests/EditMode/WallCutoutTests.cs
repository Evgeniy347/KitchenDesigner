using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Вырез под окно в мешe стены: сквозное отверстие, корректная
/// позиция для смещённого окна и обеих ориентаций стены.</summary>
public class WallCutoutTests : SnapTestBase
{
    private static WallMeshBuilder.WindowCutout Cut(float cx, float cy, float hx, float hy) =>
        new WallMeshBuilder.WindowCutout
        {
            centerNorm = new Vector2(cx, cy),
            halfSizeNorm = new Vector2(hx, hy),
        };

    /// <summary>Ни одна вершина грани face (|coord по нормали| = 0.5) не лежит
    /// строго внутри прямоугольника выреза.</summary>
    private static void AssertFaceHasHole(Mesh mesh, int normalAxis, float cu, float cv, float hu, float hv, int uAxis)
    {
        int vAxis = 1; // высота всегда Y
        bool foundEdge = false;
        foreach (var v in mesh.vertices)
        {
            if (Mathf.Abs(Mathf.Abs(v[normalAxis]) - 0.5f) > 1e-4f) continue; // не на этой паре граней
            float u = v[uAxis], w = v[vAxis];
            bool strictlyInside = u > cu - hu + 1e-4f && u < cu + hu - 1e-4f &&
                                  w > cv - hv + 1e-4f && w < cv + hv - 1e-4f;
            Assert.IsFalse(strictlyInside, $"вершина {v} внутри проёма — дыра не вырезана");
            bool onEdge = (Mathf.Abs(u - (cu - hu)) < 1e-4f || Mathf.Abs(u - (cu + hu)) < 1e-4f) &&
                          w >= cv - hv - 1e-4f && w <= cv + hv + 1e-4f;
            if (onEdge) foundEdge = true;
        }
        Assert.IsTrue(foundEdge, "нет вершин по границе проёма — вырез не построен");
    }

    [Test]
    public void Builder_CutsThroughZ_ForOffCenterWindow()
    {
        var mesh = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout> { Cut(0.2f, -0.1f, 0.1f, 0.2f) });
        // Дыра на обеих гранях ±Z и именно в заданном месте (раньше центр
        // делился на полуширину стены дважды и вырез уезжал к центру).
        AssertFaceHasHole(mesh, normalAxis: 2, cu: 0.2f, cv: -0.1f, hu: 0.1f, hv: 0.2f, uAxis: 0);
    }

    [Test]
    public void Builder_CutsThroughX_WhenThicknessAlongX()
    {
        var mesh = WallMeshBuilder.Build(
            new List<WallMeshBuilder.WindowCutout> { Cut(0.2f, -0.1f, 0.1f, 0.2f) },
            thicknessAlongX: true);
        // Для стены, повёрнутой длиной вдоль Z, дыра — сквозь X, ширина — по Z.
        AssertFaceHasHole(mesh, normalAxis: 0, cu: 0.2f, cv: -0.1f, hu: 0.1f, hv: 0.2f, uAxis: 2);
    }

    [Test]
    public void Builder_BackFace_HasRectangularHole_NotFullWidthGap()
    {
        // Регресс: задняя грань строилась с перевёрнутым диапазоном X, из-за
        // чего вырез растягивался на всю ширину. У корректного меша на грани
        // z=-0.5 должны быть вершины между вырезом и краем стены (простенки).
        var mesh = WallMeshBuilder.Build(new List<WallMeshBuilder.WindowCutout> { Cut(0f, 0f, 0.15f, 0.24f) });

        bool hasBackPier = false;
        foreach (var v in mesh.vertices)
        {
            if (Mathf.Abs(v.z + 0.5f) > 1e-4f) continue; // только задняя грань
            // Вершина на границе выреза (x = ±0.15) в полосе высоты окна.
            if (Mathf.Abs(Mathf.Abs(v.x) - 0.15f) < 1e-4f && Mathf.Abs(v.y) <= 0.24f + 1e-4f)
                hasBackPier = true;
        }
        Assert.IsTrue(hasBackPier, "на задней грани нет простенков по бокам проёма — вырез во всю ширину");
    }

    [Test]
    public void Wall_RebuildMesh_UsesWindowOffsetAlongWall()
    {
        // Стена длиной вдоль X, окно смещено на +0.6 м от центра стены.
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Cut", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Cut", new Vector3(0.6f, 1.2f, -1.5f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh);

        // Нормализованный центр проёма: 0.6/3.0 = 0.2 по ширине,
        // (1.2 − 1.25)/2.5 = −0.02 по высоте; полуразмеры 0.45/3, 0.6/2.5.
        AssertFaceHasHole(mesh!, normalAxis: 2,
            cu: 0.2f, cv: -0.02f, hu: 0.45f / 3f, hv: 0.6f / 2.5f, uAxis: 0);
    }

    /// <summary>Стену с окном сдвинули вдоль её длины: окно остаётся на месте,
/// значит вырез обязан переехать в новое относительное положение. Регресс:
/// меш не перестраивался и дыра уезжала вместе со стеной — «окно отдельно,
/// дыра отдельно».</summary>
    [Test]
    public void Wall_MovedAlongLength_RebuildsCutoutAtWindowPosition()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Move", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Move", new Vector3(0.6f, 1.25f, -1.5f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var wall = wallGo.GetComponent<Wall>()!;
        wallGo.transform.position += new Vector3(0.5f, 0f, 0f);
        wall.SyncOpeningsIfChanged();

        // Окно осталось на x = 0.6, центр стены уехал на x = 0.5 →
        // смещение вдоль стены 0.1 м из 3.0 м.
        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        AssertFaceHasHole(mesh!, normalAxis: 2,
            cu: 0.1f / 3f, cv: 0f, hu: 0.45f / 3f, hv: 0.6f / 2.5f, uAxis: 0);
    }

    /// <summary>Стену с окном растянули по длине: нормализованные координаты
    /// выреза считаются от новой ширины, иначе дыра «разъезжается» с окном.</summary>
    [Test]
    public void Wall_ResizedAlongLength_RebuildsCutoutAtWindowPosition()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Resize", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Resize", new Vector3(0.6f, 1.25f, -1.5f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var wallEl = wallGo.GetComponent<KitchenElement>()!;
        var wall = wallGo.GetComponent<Wall>()!;
        wallEl.DimensionsMM = new Vector3Int(4000, 2500, 100);
        wall.SyncOpeningsIfChanged();

        // Центр стены не двигался, окно на 0.6 м от него, ширина теперь 4.0 м.
        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        AssertFaceHasHole(mesh!, normalAxis: 2,
            cu: 0.6f / 4f, cv: 0f, hu: 0.45f / 4f, hv: 0.6f / 2.5f, uAxis: 0);
    }

    /// <summary>Опускание МЕНЯЕТ геометрию, и это не оптимизируемая деталь.
    /// Вырезы заданы в нормированных координатах ПОЛНОЙ стены, а опускание сжимает
    /// меш по Y — вместе со стеной сжимается перемычка над дверью и садится поперёк
    /// прохода порогом (см. DoorThresholdTests.Wall_Lowered_KeepsNoLintelAcrossADoorOpening).
    /// Значит на ПЕРЕХОДЕ меш обязан пересобраться.
    ///
    /// Раньше здесь стоял обратный запрет — «лишний Rebuild каждый кадр не нужен» —
    /// и он закреплял сам дефект. Настоящая забота того теста была про КАЖДЫЙ КАДР:
    /// WallManager.LateUpdate зовёт SetLowered на каждом кадре, и пересборка там
    /// действительно недопустима. Она сохранена вторым ассертом.</summary>
    [Test]
    public void Wall_Lowered_RebuildsOnTheTransition_ButNotOnEveryCall()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall_Low", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGo);
        var winGo = ElementFactory.CreateWindow(
            new Vector3Int(900, 1200, 100), "Win_Low", new Vector3(0.6f, 1.25f, -1.5f));
        _spawned.Add(winGo);
        winGo.GetComponent<WindowElement>()!.SnapToWall();

        var wall = wallGo.GetComponent<Wall>()!;
        wall.SyncOpeningsIfChanged();
        var before = wallGo.GetComponent<MeshFilter>()!.sharedMesh;

        wall.SetLowered(true, 0.1f);
        var afterTransition = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.AreNotSame(before, afterTransition,
            "переход в опущенное состояние обязан пересобрать меш: у опущенной стены "
            + "другая геометрия проёмов, а не та же самая пониже");

        wall.SetLowered(true, 0.1f);
        wall.SetLowered(true, 0.1f);
        Assert.AreSame(afterTransition, wallGo.GetComponent<MeshFilter>()!.sharedMesh,
            "повторный вызов в том же состоянии пересобирать не имеет права — "
            + "WallManager зовёт SetLowered каждый кадр");
    }

    // Зазор между окнами (= Tolerance.EpsilonUnits) меньше порога MinCellNorm —
    // ячейка должна быть скипнута, иначе появится вырожденный квад (полоса).
    private const float CloseGap = Tolerance.EpsilonUnits;

    /// <summary>Два выреза с очень близкими границами по Y не должны порождать
    /// вырожденные треугольники (регресс: тонкая полоса между окнами).</summary>
    [Test]
    public void Builder_TwoCloseWindows_NoDegenerateTriangles()
    {
        // Окно 1: Y ∈ [CloseGap, 0.4+CloseGap], окно 2: Y ∈ [−0.4−CloseGap, −CloseGap].
        var cutouts = new List<WallMeshBuilder.WindowCutout>
        {
            Cut(0f,  0.2f + CloseGap * 0.5f, 0.1f, 0.2f),
            Cut(0f, -0.2f - CloseGap * 0.5f, 0.1f, 0.2f),
        };
        var mesh = WallMeshBuilder.Build(cutouts);

        var verts = mesh.vertices;
        var tris = mesh.triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            float ab = (a - b).magnitude;
            float bc = (b - c).magnitude;
            float ca = (c - a).magnitude;
            Assert.GreaterOrEqual(ab, WallMeshBuilder.MinCellNorm,
                $"вырожденное ребро AB={ab} в треугольнике {t/3}: {a}→{b}");
            Assert.GreaterOrEqual(bc, WallMeshBuilder.MinCellNorm,
                $"вырожденное ребро BC={bc} в треугольнике {t/3}: {b}→{c}");
            Assert.GreaterOrEqual(ca, WallMeshBuilder.MinCellNorm,
                $"вырожденное ребро CA={ca} в треугольнике {t/3}: {c}→{a}");
        }
    }

    /// <summary>Стена с двумя окнами, зарегистрированными через SnapToWall:
    /// меш не должен содержать вырожденных треугольников.</summary>
    [Test]
    public void Wall_TwoWindows_OnSameWall_NoDegenerateTriangles()
    {
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(3000, 2500, 100), "Wall2W", new Vector3(0f, 1.25f, -1.5f));
        _spawned.Add(wallGo);

        // Два окна разносятся по высоте: нижнее (y=0.6, пролёт [0.1, 1.1]),
        // верхнее (y=1.9, пролёт [1.4, 2.4]). Зазор 300 мм — без пересечений.
        var win1Go = ElementFactory.CreateWindow(
            new Vector3Int(900, 1000, 100), "Win2W_1", new Vector3(0f, 1.9f, -1.5f));
        _spawned.Add(win1Go);
        var win2Go = ElementFactory.CreateWindow(
            new Vector3Int(900, 1000, 100), "Win2W_2", new Vector3(0f, 0.6f, -1.5f));
        _spawned.Add(win2Go);

        win1Go.GetComponent<WindowElement>()!.SnapToWall();
        win2Go.GetComponent<WindowElement>()!.SnapToWall();

        var mesh = wallGo.GetComponent<MeshFilter>()!.sharedMesh;
        Assert.IsNotNull(mesh);

        var verts = mesh.vertices;
        var tris = mesh.triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            var a = verts[tris[t]];
            var b = verts[tris[t + 1]];
            var c = verts[tris[t + 2]];
            float ab = (a - b).magnitude;
            float bc = (b - c).magnitude;
            float ca = (c - a).magnitude;
            Assert.GreaterOrEqual(ab, WallMeshBuilder.MinCellNorm, $"AB={ab} tri {t/3}");
            Assert.GreaterOrEqual(bc, WallMeshBuilder.MinCellNorm, $"BC={bc} tri {t/3}");
            Assert.GreaterOrEqual(ca, WallMeshBuilder.MinCellNorm, $"CA={ca} tri {t/3}");
        }
    }

    /// <summary>Два окна бок о бок «почти на одной высоте» (края по Y отличаются
    /// меньше чем на MinCellNorm). Регресс: волосковая ячейка у края проёма
    /// отбрасывалась вместе с гранями и откосом — сквозная щель со светом
    /// сверху и снизу окна. После схлопывания близких краёв все Y-координаты
    /// меша либо совпадают, либо отстоят не меньше чем на MinCellNorm.</summary>
    [Test]
    public void Builder_TwoWindowsNearSameHeight_NoThinUncappedBand()
    {
        float delta = WallMeshBuilder.MinCellNorm * 0.3f; // заведомо ниже порога
        var cutouts = new List<WallMeshBuilder.WindowCutout>
        {
            Cut(-0.25f, 0f,    0.1f, 0.2f),  // X ∈ [-0.35,-0.15], Y ∈ [-0.2, 0.2]
            Cut( 0.25f, delta, 0.1f, 0.2f),  // X ∈ [ 0.15, 0.35], Y ∈ [-0.2+δ, 0.2+δ]
        };
        var mesh = WallMeshBuilder.Build(cutouts);

        var ys = new List<float>();
        foreach (var v in mesh.vertices) ys.Add(v.y);
        ys.Sort();

        // Соседние РАЗЛИЧНЫЕ Y не должны отстоять меньше чем на MinCellNorm:
        // близкие края окон обязаны слиться, иначе между ними остаётся
        // незакрытая полоска (буг: -0.2 и -0.2+δ существуют одновременно).
        const float same = 1e-6f;
        for (int i = 1; i < ys.Count; i++)
        {
            float gap = ys[i] - ys[i - 1];
            Assert.IsTrue(gap < same || gap >= WallMeshBuilder.MinCellNorm - same,
                $"почти-дубль Y: {ys[i - 1]} и {ys[i]} (зазор {gap}) — волосковая ячейка у края проёма");
        }
    }

    /// <summary>Два пересекающихся окна: reveal-квады должны быть только на
    /// внешних границах объединённого проёма, а не внутри overlap-региона.
    /// Регресс: артефактная полоса на границе overlap/single-window.</summary>
    [Test]
    public void Builder_OverlappingWindows_NoInternalRevealQuads()
    {
        // Оба окна помещаются в unit cube [-0.5, 0.5]. Пересекаются по Y.
        // Окно 1: X ∈ [-0.40, -0.10], Y ∈ [-0.30, 0.10]
        // Окно 2: X ∈ [-0.10,  0.20], Y ∈ [-0.10, 0.30]
        // Overlap по Y: [-0.10, 0.10]. Внешние границы: y = -0.30 и y = 0.30.
        var cutouts = new List<WallMeshBuilder.WindowCutout>
        {
            Cut(-0.25f, -0.10f, 0.15f, 0.20f),  // X ∈ [-0.40, -0.10], Y ∈ [-0.30, 0.10]
            Cut( 0.05f,  0.10f, 0.15f, 0.20f),  // X ∈ [-0.10,  0.20], Y ∈ [-0.10, 0.30]
        };
        var mesh = WallMeshBuilder.Build(cutouts);

        var verts = mesh.vertices;

        // Негативный assert: нет вершин на front/back face строго внутри
        // overlap-региона (y ∈ (-0.10, 0.10)).
        foreach (var v in verts)
        {
            if (Mathf.Abs(Mathf.Abs(v.z) - 0.5f) > 1e-3f) continue;
            float eps = 1e-3f;
            bool insideOverlap = v.y > -0.10f + eps && v.y < 0.10f - eps;
            Assert.IsFalse(insideOverlap,
                $"вершина {v} на front/back face внутри overlap-региона — reveal на внутреннем ребре");
        }

        // Позитивный assert: reveal-квады на внешних границах (y = -0.30 и y = 0.30)
        // должны существовать.
        bool hasBottomReveal = false, hasTopReveal = false;
        foreach (var v in verts)
        {
            if (Mathf.Abs(Mathf.Abs(v.z) - 0.5f) > 1e-3f) continue;
            if (Mathf.Abs(v.y - (-0.30f)) < 1e-3f) hasBottomReveal = true;
            if (Mathf.Abs(v.y - 0.30f) < 1e-3f) hasTopReveal = true;
        }
        Assert.IsTrue(hasBottomReveal, "нет reveal-квада на нижней границе (y=-0.30)");
        Assert.IsTrue(hasTopReveal, "нет reveal-квада на верхней границе (y=0.30)");
    }
}
