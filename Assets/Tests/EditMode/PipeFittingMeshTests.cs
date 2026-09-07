using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Меш фитинга и его габаритный ящик обязаны описывать ОДНУ форму
/// (CONVENTIONS.md → «A mesh and its metadata must describe the SAME shape»).
///
/// Оба дефекта, ради которых набор написан, кадр показывал, а ни один тест не
/// ловил. Первый: у всех труб фитинга торцы строились с ВЫВЕРНУТОЙ намоткой —
/// вершины лежали правильно, нормали смотрели наружу, а треугольник был закручен
/// внутрь, поэтому крышки отсекались как задние грани. На iso_pipe_cap_dn20.png в
/// заглушку было видно насквозь, а тройник читался цилиндром с плёнкой сбоку.
/// Проверка по вершинам этого не видит вовсе: вершины-то на месте. Видит её
/// сравнение намотки с ЗАЯВЛЕННОЙ нормалью — единственный источник, который в
/// меше говорит, какая сторона наружная.
///
/// Второй: ящик считался как 2·max(|min|,|max|) от геометрии, то есть по
/// построению не мог оказаться меньше её — и точно так же не мог сообщить, что
/// он БОЛЬШЕ. А по этому ящику считаются пересечения (COL-01, PIP-03), и лишний
/// объём — это деталь, которая «пересекает» столешницу, не касаясь её.
///
/// Ступица остаётся привязанной к НОМИНАЛЬНОЙ раме, а не к выведенному проходу:
/// устья фитинга — его монтажный контракт (PipeFittingBoreTests), и поедь они за
/// диаметром, стык, из которого диаметр выведен, разошёлся бы. Поэтому здесь
/// проверяется номинальный случай, где ящик обязан сойтись с мешем ВПЛОТНУЮ.</summary>
public class PipeFittingMeshTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private static readonly PipeNodeKind[] Fittings =
    {
        PipeNodeKind.Elbow, PipeNodeKind.Coupling, PipeNodeKind.Tee,
        PipeNodeKind.Cap, PipeNodeKind.Supply, PipeNodeKind.Return,
    };

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private PipeFittingElement Spawn(PipeNodeKind kind)
    {
        var go = kind switch
        {
            PipeNodeKind.Elbow => ElementFactory.CreatePipeElbow("MeshElbow", Vector3.zero),
            PipeNodeKind.Coupling => ElementFactory.CreatePipeCoupling("MeshCoupling", Vector3.zero),
            PipeNodeKind.Tee => ElementFactory.CreatePipeTee("MeshTee", Vector3.zero),
            PipeNodeKind.Cap => ElementFactory.CreatePipeCap("MeshCap", Vector3.zero),
            PipeNodeKind.Supply => ElementFactory.CreatePipeSupply("MeshSupply", Vector3.zero),
            _ => ElementFactory.CreatePipeReturn("MeshReturn", Vector3.zero),
        };
        _spawned.Add(go);
        var fitting = go.GetComponent<PipeFittingElement>();
        Assert.IsNotNull(fitting, kind + ": фабрика собрала не фитинг");
        return fitting!;
    }

    private static Mesh MeshOf(PipeFittingElement fitting)
    {
        var filter = fitting.GetComponent<MeshFilter>();
        Assert.IsNotNull(filter, fitting.NodeKind + ": у фитинга нет MeshFilter");
        var mesh = filter!.sharedMesh;
        Assert.IsNotNull(mesh, fitting.NodeKind + ": MeshFilter пуст");
        Assert.Greater(mesh!.vertexCount, 0, fitting.NodeKind + ": меш без вершин");
        return mesh!;
    }

    /// <summary>Наибольшее отклонение вписанного многоугольника от окружности,
    /// которую он изображает. Меш кругов не умеет, поэтому его габарит МЕНЬШЕ
    /// объявленного ровно на эту стрелку — и допуск обязан быть ею, а не круглым
    /// числом: заданный «на глаз» допуск в миллиметр скрыл бы вдвое раздутый
    /// ящик у ДУ 15, где стрелка втрое меньше.</summary>
    private static float SagittaMM(PipeNodeKind kind)
    {
        float worst = 0f;
        foreach (var part in PipeFittingLayout.PartsMM(kind, PipeSpec.DEFAULT_SIZE, null))
        {
            float radius = Mathf.Max(part.FromRadiusMM, part.ToRadiusMM);
            worst = Mathf.Max(worst,
                PipeTessellation.SagittaMM(radius, PipeTessellation.RadialSegmentsFor(radius)));
        }
        return worst;
    }

    private const float RoundingMM = 0.5f;

    [Test]
    public void EveryFittingMesh_FillsTheBoxItDeclares_AndNeverLeavesIt()
    {
        foreach (var kind in Fittings)
        {
            var fitting = Spawn(kind);
            var mesh = MeshOf(fitting);
            var box = mesh.bounds;
            float toMM = 1f / AppConstants.MM_TO_UNITS;
            var half = new Vector3(box.extents.x, box.extents.y, box.extents.z) * toMM;
            var centre = box.center * toMM;
            var declared = fitting.DerivedDimensionsMM;
            float slack = SagittaMM(kind) + RoundingMM;

            var declaredHalf = new Vector3(declared.x, declared.y, declared.z) * 0.5f;
            string what = kind + " (ящик " + declared + ", меш " + (half * 2f) + ")";

            for (int axis = 0; axis < 3; axis++)
            {
                Assert.LessOrEqual(half[axis], declaredHalf[axis] + RoundingMM,
                    "меш вылез за собственный ящик по оси " + axis + ": валидация судит "
                    + "пересечения по ящику и этой части детали просто не увидит — " + what);
                Assert.GreaterOrEqual(half[axis], declaredHalf[axis] - slack,
                    "ящик больше меша по оси " + axis + ": деталь занимает объём, "
                    + "которого у неё нет, и «пересечёт» соседа, не коснувшись его — " + what);
                Assert.AreEqual(0f, centre[axis], slack,
                    "меш смещён относительно точки элемента по оси " + axis + ", а ящик "
                    + "KitchenElement центрирован на ней — " + what);
            }

            Teardown();
        }
    }

    /// <summary>Треугольник, закрученный против собственной нормали, рисуется
    /// только изнутри: снаружи его отсекает back-face culling. Именно так пропали
    /// торцы у всех шести фитингов сразу — форма была, а видно её не было.</summary>
    [Test]
    public void EveryFittingMesh_WindsItsFacesOutward_SoItsEndsAreSolid()
    {
        var report = new List<string>();

        foreach (var kind in Fittings)
        {
            var mesh = MeshOf(Spawn(kind));
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;

            Assert.AreEqual(vertices.Length, normals.Length,
                kind + ": у меша нет нормалей — сравнивать намотку не с чем");

            int inverted = 0;
            int flat = 0;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var face = Vector3.Cross(vertices[triangles[i + 1]] - a,
                    vertices[triangles[i + 2]] - a);
                if (face.sqrMagnitude <= Tolerance.EpsilonSqr)
                {
                    flat++;
                    continue;
                }

                if (Vector3.Dot(face.normalized, normals[triangles[i]]) < 0f) inverted++;
            }

            if (inverted > 0)
                report.Add(kind + ": " + inverted + " из " + (triangles.Length / 3)
                    + " треугольников закручены внутрь (вырожденных " + flat + ")");

            Teardown();
        }

        CollectionAssert.IsEmpty(report,
            "намотка разошлась с нормалями: " + string.Join("; ", report)
            + ". Такой треугольник виден только изнутри — на изометрическом кадре "
            + "деталь стоит с дырой на месте торца");
    }

    /// <summary>Подача и обратка — одна железка, и разводить их формой корпуса
    /// значит врать чертежом. Различает их МАРКИРОВКА: стрелка потока на теле и
    /// цвет. Стрелка обязательна отдельно от цвета (LEAD-AGENT.md §2): смысл,
    /// который несёт только цвет, исчезает на чёрно-белой печати и у дальтоника.
    ///
    /// До этой правки два кадра, iso_pipe_supply_dn20.png и
    /// iso_pipe_return_dn20.png, совпадали побайтово.</summary>
    [Test]
    public void SupplyAndReturn_ShareOneBody_AndDifferByTheMarkAndTheColourBoth()
    {
        var supply = Spawn(PipeNodeKind.Supply);
        var back = Spawn(PipeNodeKind.Return);

        Assert.AreEqual(supply.DerivedDimensionsMM, back.DerivedDimensionsMM,
            "корпус подачи и обратки обязан совпадать — это одна деталь");

        float supplyMark = MarkedMassY(MeshOf(supply));
        float returnMark = MarkedMassY(MeshOf(back));
        Assert.Greater(supplyMark, returnMark,
            "стрелки подачи и обратки смотрят в одну сторону: по силуэту детали "
            + "неразличимы, а на контуре отопления они не взаимозаменяемы");

        var warm = supply.FactoryMaterial;
        var cold = back.FactoryMaterial;
        Assert.AreNotSame(warm, cold, "подача и обратка красятся одним материалом");
        Assert.Greater(warm.color.r, warm.color.b, "подача обязана быть тёплой");
        Assert.Greater(cold.color.b, cold.color.r, "обратка обязана быть холодной");
    }

    /// <summary>Средняя ордината всех вершин. Корпус у подачи и обратки один и
    /// тот же, поэтому в этой сумме он сокращается, а остаётся ровно перекос,
    /// который вносит наконечник стрелки: у подачи он выше середины тела, у
    /// обратки ниже. Число само по себе ничего не значит — значение имеет ЗНАК
    /// разности между двумя деталями, и именно он и сравнивается.</summary>
    private static float MarkedMassY(Mesh mesh)
    {
        var vertices = mesh.vertices;
        float sum = 0f;
        foreach (var v in vertices) sum += v.y;
        return sum / vertices.Length;
    }
}
