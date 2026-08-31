using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class RadialShelfTests
{
    private GameObject? _go;

    [TearDown]
    public void Teardown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        PartRegistry.Clear();
        MaterialCatalog.Reset();
    }

    [Test]
    public void CreateRadialShelf_HasDimensionsAndCornerRadius()
    {
        var shelf = CreateShelf("R1", 600, 400, 18, 200, Vector3.zero);

        Assert.AreEqual(new Vector3Int(600, 18, 400), shelf.DimensionsMM);
        Assert.AreEqual(200, shelf.CornerRadius);
        Assert.IsTrue(shelf.GetComponent<MeshCollider>() != null);
    }

    [Test]
    public void CornerRadiusSetter_ClampsToMinOfWidthAndDepth()
    {
        var shelf = CreateShelf("R2", 600, 400, 18, 200, Vector3.zero);

        shelf.CornerRadius = 1000;
        Assert.AreEqual(400, shelf.CornerRadius, "clamped to min(width, depth)");

        shelf.CornerRadius = 0;
        Assert.AreEqual(1, shelf.CornerRadius, "clamped to 1");

        shelf.CornerRadius = 150;
        Assert.AreEqual(150, shelf.CornerRadius);
    }

    [Test]
    public void ApplyDimensions_ClampsCornerRadius_WhenBoardShrinks()
    {
        var shelf = CreateShelf("R3", 600, 400, 18, 300, Vector3.zero);
        Assert.AreEqual(300, shelf.CornerRadius);

        shelf.DimensionsMM = new Vector3Int(600, 18, 150);
        Assert.AreEqual(150, shelf.CornerRadius, "corner radius follows the shrunken depth");
    }

    [Test]
    public void BuildMesh_BoundsMatchDimensions()
    {
        var shelf = CreateShelf("R4", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;

        Assert.IsNotNull(mesh);
        Assert.Greater(mesh.vertexCount, 24, "mesh should be procedural, not default cube");
        Assert.AreEqual(600f * AppConstants.MM_TO_UNITS, mesh.bounds.size.x, 1e-4f);
        Assert.AreEqual(18f * AppConstants.MM_TO_UNITS, mesh.bounds.size.y, 1e-4f);
        Assert.AreEqual(400f * AppConstants.MM_TO_UNITS, mesh.bounds.size.z, 1e-4f);
    }

    [Test]
    public void ApplyDimensions_LocalScaleIsUnity()
    {
        var shelf = CreateShelf("R_SCALE", 600, 400, 18, 200, Vector3.zero);
        var ls = shelf.transform.localScale;
        Assert.AreEqual(1f, ls.x, 1e-6f, "localScale.x must be 1");
        Assert.AreEqual(1f, ls.y, 1e-6f, "localScale.y must be 1");
        Assert.AreEqual(1f, ls.z, 1e-6f, "localScale.z must be 1");
    }

    [Test]
    public void EffectiveScale_ReturnsWorldUnitSize()
    {
        var shelf = CreateShelf("R_ESCALE", 500, 350, 25, 200, Vector3.zero);
        // EffectiveScale is protected; verify indirectly via GetVertices bounding box.
        var verts = shelf.GetVertices();
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in verts)
        {
            if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
            if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
        }
        Assert.AreEqual(500f * AppConstants.MM_TO_UNITS, maxX - minX, 1e-4f, "X extent should match width in world units");
        Assert.AreEqual(25f * AppConstants.MM_TO_UNITS, maxY - minY, 1e-4f, "Y extent should match thickness in world units");
        Assert.AreEqual(350f * AppConstants.MM_TO_UNITS, maxZ - minZ, 1e-4f, "Z extent should match depth in world units");
    }

    [Test]
    public void BuildMesh_VertexCount_MatchesExpectedTopology()
    {
        // Контур RoundedRectProfile: 3 острых угла + дуга из 17 точек = 20 точек.
        // крышки (x2): 1 центр + 20 по контуру = 21 → 42
        // боковины: 20 сегментов * 4 (свои вершины у каждого — так держатся
        // жёсткие рёбра на прямых углах) = 80
        // итого = 122
        var shelf = CreateShelf("R_VTX", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(122, mesh.vertexCount, "топология полки после перехода на общий "
            + "экструдер: у каждого сегмента боковины свои вершины");
    }

    [Test]
    public void BuildMesh_TriangleCount_MatchesExpectedTopology()
    {
        // крышки (x2): веер из 20 треугольников = 40
        // боковины: 20 сегментов * 2 = 40
        // итого = 80
        var shelf = CreateShelf("R_TRIS", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(80, mesh.triangles.Length / 3, "число треугольников полки");
    }

    [Test]
    public void BuildMesh_IsCenteredOnPivot()
    {
        // Контур/прилипание строятся как AABB «позиция ± габарит/2» — меш
        // обязан быть центрирован, иначе деталь вылезает из контура.
        var shelf = CreateShelf("R_CTR", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;

        Assert.AreEqual(0f, mesh.bounds.center.x, 1e-5f, "bounds center X");
        Assert.AreEqual(0f, mesh.bounds.center.y, 1e-5f, "bounds center Y");
        Assert.AreEqual(0f, mesh.bounds.center.z, 1e-5f, "bounds center Z");
    }

    [Test]
    public void BuildMesh_ArcIsAtRoundedCorner()
    {
        // Доска 600×400, R=200, меш центрирован: скруглён угол (+0.3, +0.2),
        // дуга от (0.3, 0.0) до (0.1, 0.2) вокруг центра (0.1, 0.0). Проверка
        // идёт по КООРДИНАТАМ, а не по индексам вершин: раскладка меша — дело
        // общего экструдера, и тест не вправе её замораживать.
        var shelf = CreateShelf("R_ARC", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        var verts = mesh.vertices;
        var centre = new Vector2(0.1f, 0f);

        foreach (var v in verts)
        {
            if (v.x < centre.x || v.z < centre.y) continue;
            float distance = new Vector2(v.x - centre.x, v.z - centre.y).magnitude;
            Assert.LessOrEqual(distance, 0.2f + 1e-5f,
                "ни одна вершина не вправе выйти за дугу радиуса R вокруг (W/2−R, D/2−R): "
                + "именно это и значит «угол срезан»");
        }

        Assert.IsTrue(HasVertexAtXZ(verts, 0.3f, 0f), "дуга начинается в (+W/2, D/2−R)");
        Assert.IsTrue(HasVertexAtXZ(verts, 0.1f, 0.2f), "дуга кончается в (W/2−R, +D/2)");
        Assert.IsTrue(HasVertexAtXZ(verts,
                centre.x + 0.2f * Mathf.Cos(Mathf.PI * 0.25f),
                centre.y + 0.2f * Mathf.Sin(Mathf.PI * 0.25f)),
            "середина дуги (45°) лежит на радиусе R — без неё «срезан» мог бы означать "
            + "просто хорду");
    }

    [Test]
    public void BuildMesh_ThreeStraightCornersPresent()
    {
        var shelf = CreateShelf("R_CRN", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        var verts = mesh.vertices;

        Assert.IsTrue(HasVertexAtXZ(verts, -0.3f, -0.2f), "corner (−W/2,−D/2) must be square");
        Assert.IsTrue(HasVertexAtXZ(verts, 0.3f, -0.2f), "corner (+W/2,−D/2) must be square");
        Assert.IsTrue(HasVertexAtXZ(verts, -0.3f, 0.2f), "corner (−W/2,+D/2) must be square");
        Assert.IsFalse(HasVertexAtXZ(verts, 0.3f, 0.2f), "corner (+W/2,+D/2) must be rounded away");
    }

    [Test]
    public void BuildMesh_NormalsPointOutward()
    {
        var shelf = CreateShelf("R_NORM", 600, 400, 18, 200, Vector3.zero);
        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        var normals = mesh.normals;
        var verts = mesh.vertices;
        int caps = 0, sides = 0;

        // Крышку от боковины отличаем по самой нормали, а не по индексу: раскладка
        // вершин принадлежит общему экструдеру. Наружу боковина смотрит тогда,
        // когда её нормаль лежит по ту же сторону, что и сама вершина от оси
        // детали — контур выпуклый и охватывает начало координат.
        for (int i = 0; i < normals.Length; i++)
        {
            var n = normals[i];
            if (Mathf.Abs(n.y) > 0.5f)
            {
                caps++;
                Assert.AreEqual(Mathf.Sign(verts[i].y), n.y, 1e-4f,
                    "нормаль крышки смотрит от плоскости детали: верхняя вверх, нижняя вниз");
                continue;
            }

            sides++;
            float lenXZ = Mathf.Sqrt(n.x * n.x + n.z * n.z);
            Assert.AreEqual(1f, lenXZ, 1e-4f, "нормаль боковины единичная и лежит в плоскости XZ");
            Assert.AreEqual(0f, n.y, 1e-4f, "у нормали боковины нет вертикальной составляющей");
            Assert.Greater(n.x * verts[i].x + n.z * verts[i].z, 0f,
                "нормаль боковины смотрит НАРУЖУ: при обратном обходе контура вся "
                + "боковая поверхность вывернулась бы внутрь, и деталь стала бы прозрачной");
        }

        Assert.AreEqual(2 * 21, caps, "две крышки по 21 вершине (центр веера + контур)");
        Assert.AreEqual(20 * 4, sides, "20 сегментов боковины по 4 вершины");
    }

    [Test]
    public void BuildMesh_CornerRadiusChange_RebuildsMesh()
    {
        var shelf = CreateShelf("R_CHG", 600, 400, 18, 200, Vector3.zero);
        var before = shelf.GetComponent<MeshFilter>().sharedMesh;
        Assert.IsFalse(HasVertexAtXZ(before.vertices, 0.3f, 0.2f));

        shelf.CornerRadius = 100;
        var after = shelf.GetComponent<MeshFilter>().sharedMesh;

        // Дуга сместилась к углу: центр дуги теперь (W/2−R, D/2−R) = (0.2, 0.1),
        // значит она начинается в (0.3, 0.1) и кончается в (0.2, 0.2).
        Assert.IsTrue(HasVertexAtXZ(after.vertices, 0.3f, 0.1f), "новая дуга начинается ниже");
        Assert.IsTrue(HasVertexAtXZ(after.vertices, 0.2f, 0.2f), "и кончается правее");
        Assert.IsFalse(HasVertexAtXZ(after.vertices, 0.3f, 0f),
            "начало прежней дуги (R=200) обязано исчезнуть — иначе меш не перестроился");
        Assert.AreEqual(Vector3.one.x, shelf.transform.localScale.x, 1e-6f);
        Assert.AreEqual(Vector3.one.y, shelf.transform.localScale.y, 1e-6f);
        Assert.AreEqual(Vector3.one.z, shelf.transform.localScale.z, 1e-6f);
    }

    [Test]
    public void ElementData_FromElement_PreservesCornerRadius()
    {
        var shelf = CreateShelf("R5", 600, 400, 25, 180, new Vector3(1f, 0.5f, -2f));
        var ed = ElementCapture.FromElement(shelf);

        Assert.IsTrue(ed.isRadialShelf);
        Assert.AreEqual(180, ed.cornerRadius);
        Assert.AreEqual(new Vector3Int(600, 25, 400), ed.Dimensions);
    }

    [Test]
    public void SaveLoad_RoundTrip_KeepsCornerRadius()
    {
        var shelf = CreateShelf("R6", 550, 350, 18, 250, new Vector3(0.5f, 0.1f, -1f));
        shelf.Movable = false;
        shelf.GroupId = 7;
        shelf.MaterialId = "oak";

        var ed = ElementCapture.FromElement(shelf);
        var json = JsonUtility.ToJson(ed);
        var restored = JsonUtility.FromJson<ElementData>(json);

        Assert.IsTrue(restored.isRadialShelf);
        Assert.AreEqual(250, restored.cornerRadius);
        Assert.AreEqual(new Vector3Int(550, 18, 350), restored.Dimensions);
        Assert.AreEqual("R6", restored.name);
        Assert.AreEqual(false, restored.movable);
        Assert.AreEqual(7, restored.groupId);
        Assert.AreEqual("oak", restored.materialId);
    }

    [Test]
    public void FromJson_WithoutCornerRadius_UsesDefault()
    {
        // Файл без поля cornerRadius (старый формат) — JsonUtility оставляет
        // дефолт инициализатора.
        var restored = JsonUtility.FromJson<ElementData>(
            "{\"name\":\"Old\",\"dimensionsMM\":[400,18,400],\"isRadialShelf\":true}");
        Assert.AreEqual(AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, restored.cornerRadius);
    }

    [Test]
    public void RestoreScene_CreatesRadialShelfWithCornerRadius()
    {
        var data = new ProjectData(new[]
        {
            new ElementData
            {
                name = "RSaved",
                dimensionsMM = new[] { 600, 18, 400 },
                position = new[] { 0f, 0f, 0f },
                rotation = new[] { 0f, 0f, 0f, 1f },
                isRadialShelf = true,
                cornerRadius = 200,
                materialId = "default"
            }
        });

        var created = SaveLoadManager.RestoreScene(data);
        Assert.AreEqual(1, created.Count);

        var shelf = created[0].GetComponent<RadialShelfElement>();
        Assert.IsNotNull(shelf);
        Assert.AreEqual(200, shelf.CornerRadius);
        Assert.AreEqual(new Vector3Int(600, 18, 400), shelf.DimensionsMM);

        foreach (var go in created)
            if (go != null) Object.DestroyImmediate(go);
    }

    [Test]
    public void Convert_PartToRadialShelf_PreservesNameAndDimensions()
    {
        var go = new GameObject("Part1");
        _go = go;
        go.transform.position = Vector3.zero;
        var part = go.AddComponent<KitchenElement>();
        part.PartName = "Part1";
        part.DimensionsMM = new Vector3Int(300, 18, 200);
        PartRegistry.Register(part);

        var converted = ElementConverter.Convert(part, ElementConverter.TargetType.RadialShelf);

        Assert.IsNotNull(converted as RadialShelfElement);
        Assert.AreEqual("Part1", converted.PartName);
        Assert.AreEqual(new Vector3Int(300, 18, 200), converted.DimensionsMM, "dimensions are preserved as-is");
        Assert.AreEqual(200, (converted as RadialShelfElement)!.CornerRadius, "default corner radius fits min(300, 200)");
    }

    [Test]
    public void Convert_RadialShelfToPart_PreservesNameAndDimensions()
    {
        var shelf = CreateShelf("RS", 600, 400, 18, 200, Vector3.zero);
        var converted = ElementConverter.Convert(shelf, ElementConverter.TargetType.Part);

        Assert.IsNull(converted as RadialShelfElement);
        Assert.AreEqual("RS", converted.PartName);
        Assert.AreEqual(new Vector3Int(600, 18, 400), converted.DimensionsMM);

        // После конвертации в Part меш должен быть коробкой, а не радиальным:
        // 6 граней по 4 вершины (см. GrooveMesh).
        var mf = converted.GetComponent<MeshFilter>();
        Assert.IsNotNull(mf, "MeshFilter is present");
        Assert.IsNotNull(mf!.sharedMesh, "sharedMesh is assigned");
        Assert.AreEqual(24, mf.sharedMesh.vertexCount, "mesh is a box, not radial");

        // Коллайдер — BoxCollider, а не MeshCollider от радиальной полки.
        Assert.IsNull(converted.GetComponent<MeshCollider>(), "MeshCollider is removed");
        Assert.IsNotNull(converted.GetComponent<BoxCollider>(), "BoxCollider is present");
    }

    // ── Физ. масштаб декора ────────────────────────────────────────────────
    //
    // _BaseMap_ST у полки строится по DecorSurfaceMM, а она у полки — (Ш, Г):
    // полка лежит горизонтально, и вторая ось её развёртки — глубина, а не
    // толщина (см. MaterialManager.ComputeTileST). Значит UV любого куска меша
    // обязаны мериться этими же двумя размерами, иначе декор на нём
    // растягивается. Меряем так же, как у детали: протяжённость UV × ST × плитка
    // даёт физический размер куска декора, легшего на грань.

    private const int Tile = 800;

    private static Vector2 DecorSpanMM(Mesh mesh, Vector2Int surface, System.Func<Vector3, bool> pick)
    {
        var normals = mesh.normals;
        var uv = mesh.uv;
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;
        int found = 0;
        for (int i = 0; i < normals.Length; i++)
        {
            if (!pick(normals[i])) continue;
            found++;
            minU = Mathf.Min(minU, uv[i].x); maxU = Mathf.Max(maxU, uv[i].x);
            minV = Mathf.Min(minV, uv[i].y); maxV = Mathf.Max(maxV, uv[i].y);
        }
        Assert.Greater(found, 0, "нужные вершины в меше не найдены");

        var st = MaterialManager.ComputeTileST(surface, Tile, Tile);
        return new Vector2((maxU - minU) * st.x * Tile, (maxV - minV) * st.y * Tile);
    }

    private static System.Func<Vector3, bool> Facing(Vector3 normal)
        => n => Vector3.Dot(n, normal) > 0.999f;

    [Test]
    public void Decor_TopFace_MatchesWidthAndDepth()
    {
        var shelf = CreateShelf("RD1", 600, 400, 18, 200, Vector3.zero);
        _go = shelf.gameObject;

        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        var mm = DecorSpanMM(mesh, shelf.DecorSurfaceMM, Facing(Vector3.up));

        Assert.AreEqual(600f, mm.x, 0.5f, "пласть: ширина");
        Assert.AreEqual(400f, mm.y, 0.5f, "пласть: глубина");
    }

    [Test]
    public void Decor_FlatSides_MatchWallLengthAndThickness()
    {
        var shelf = CreateShelf("RD2", 600, 400, 18, 200, Vector3.zero);
        _go = shelf.gameObject;

        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        var surface = shelf.DecorSurfaceMM;

        // Торец x=0 идёт на всю глубину, торец z=0 — на всю ширину. Оба
        // получают ОДИН и тот же ST, значит длину обязаны нести UV.
        var left = DecorSpanMM(mesh, surface, Facing(-Vector3.right));
        Assert.AreEqual(400f, left.x, 0.5f, "торец x=0: длина равна глубине");
        Assert.AreEqual(18f, left.y, 0.5f, "торец x=0: толщина");

        var front = DecorSpanMM(mesh, surface, Facing(-Vector3.forward));
        Assert.AreEqual(600f, front.x, 0.5f, "торец z=0: длина равна ширине");
    }

    [Test]
    public void Decor_CurvedSide_MatchesArcLength()
    {
        var shelf = CreateShelf("RD3", 600, 400, 18, 200, Vector3.zero);
        _go = shelf.gameObject;

        var mesh = shelf.GetComponent<MeshFilter>().sharedMesh;
        // Только внутренние вершины дуги: у плоских торцов нормаль строго по оси,
        // и на концах дуга с ними совпадает.
        var mm = DecorSpanMM(mesh, shelf.DecorSurfaceMM,
            n => Mathf.Abs(n.y) < 0.01f && n.x > 0.01f && n.z > 0.01f);

        // Общий экструдер сглаживает стык дуги с прямым торцом (угол между их
        // нормалями 2,8° — меньше порога жёсткого ребра), поэтому крайние точки
        // дуги тоже попадают в выборку, и меряется ВСЯ дуга. Раньше их нормали
        // совпадали с осями, и тест не досчитывал двух сегментов из шестнадцати.
        float arc = Mathf.PI * 0.5f * 200f;
        Assert.AreEqual(arc, mm.x, 1f, "дуга: декор ложится по её длине, а не по ширине полки");
        Assert.AreEqual(18f, mm.y, 0.5f, "дуга: толщина");
    }

    [Test]
    public void Decor_Tiling_RecomputedOnResize()
    {
        var shelf = CreateShelf("RD4", 600, 400, 18, 200, Vector3.zero);
        _go = shelf.gameObject;
        var def = MaterialCatalog.Get("oak");
        MaterialManager.Apply(shelf, def);

        shelf.DimensionsMM = new Vector3Int(1200, 18, 400);

        var mpb = new MaterialPropertyBlock();
        shelf.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
        var tile = MaterialManager.TileMM(def);
        Assert.AreEqual(1200f / tile.x, mpb.GetVector("_BaseMap_ST").x, 1e-3f,
            "после ресайза «вырез» декора обязан пересчитаться");
    }

    private static bool HasVertexAtXZ(Vector3[] verts, float x, float z)
    {
        foreach (var v in verts)
            if (Mathf.Abs(v.x - x) < 1e-5f && Mathf.Abs(v.z - z) < 1e-5f)
                return true;
        return false;
    }

    private static RadialShelfElement CreateShelf(string name, int width, int depth, int thickness,
        int cornerRadius, Vector3 pos)
    {
        var go = ElementFactory.CreateRadialShelf(width, depth, thickness, cornerRadius, name, pos);
        return go.GetComponent<RadialShelfElement>();
    }
}
