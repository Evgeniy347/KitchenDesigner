using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class FacadeElementTests : ElementTestBase
{
    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    /// <summary>НЕ базовый MakePrimitiveFacade: тот оставляет зазоры нулевыми, а
    /// здесь ими управляет каждый тест — на них считается видимая грань дверцы.
    /// Имя разное намеренно: одинаковое скрывало бы разницу вместо того, чтобы
    /// её показать.</summary>
    private FacadeElement MakeFacade(string name, Vector3Int dims, Vector3 pos,
        int gapL = 2, int gapR = 2, int gapT = 2, int gapB = 2)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var f = go.AddComponent<FacadeElement>();
        f.PartName = name;
        f.DimensionsMM = dims;
        f.GapLeft = gapL;
        f.GapRight = gapR;
        f.GapTop = gapT;
        f.GapBottom = gapB;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    /// <summary>Открытая дверца строит габарит от замороженной закрытой позы, и
    /// запись в transform.position её не двигает: коробка растёт симметрично, грань
    /// уходит на половину дельты, снэп мажет, а при закрытии деталь прыгает обратно.
    /// Поэтому пока дверца открыта, двигать и растягивать её запрещено.</summary>
    [Test]
    public void Facade_OpenDoor_IsNotTransformable()
    {
        var f = MakeFacade("Дверца", new Vector3Int(400, 700, 18), Vector3.zero);
        Assert.IsTrue(f.Movable, "фасад по умолчанию подвижен");
        Assert.IsTrue(f.Transformable, "закрытая дверца двигается и растягивается");

        f.SetOpen(true);
        Assert.IsFalse(f.PoseFollowsTransform, "у открытой дверцы поза не идёт за трансформом");
        Assert.IsFalse(f.Transformable, "открытую дверцу двигать и растягивать нельзя");

        f.SetOpen(false);
        Assert.IsTrue(f.Transformable, "закрыли — снова можно");
    }

    /// <summary>Запрет перемещения по-прежнему запрещает и ресайз: Transformable
    /// не должен «оживлять» заблокированную деталь.</summary>
    [Test]
    public void Facade_Immovable_IsNotTransformable()
    {
        var f = MakeFacade("Дверца", new Vector3Int(400, 700, 18), Vector3.zero);
        f.Movable = false;
        Assert.IsFalse(f.Transformable);
    }

    [Test]
    public void Facade_DefaultGap_IsTwoOnAllSides()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero);
        Assert.AreEqual(2, f.GapLeft);
        Assert.AreEqual(2, f.GapRight);
        Assert.AreEqual(2, f.GapTop);
        Assert.AreEqual(2, f.GapBottom);
    }

    [Test]
    public void Facade_Gap_CanSetIndividualSides()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 1, 3, 5, 7);
        Assert.AreEqual(1, f.GapLeft);
        Assert.AreEqual(3, f.GapRight);
        Assert.AreEqual(5, f.GapTop);
        Assert.AreEqual(7, f.GapBottom);
    }

    [Test]
    public void Facade_Gap_ClampedToZero()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, -1, -1, -1, -1);
        Assert.AreEqual(0, f.GapLeft);
        Assert.AreEqual(0, f.GapRight);
        Assert.AreEqual(0, f.GapTop);
        Assert.AreEqual(0, f.GapBottom);
    }

    /// <summary>Оболочка не разошлась с моделью: компонент и его Body дают одни
    /// и те же вершины, грани и признаки. Геометрия зазоров (X/Y растут, Z нет)
    /// уехала в FacadeBodyTests и считается под dotnet — здесь только зеркало.</summary>
    [Test]
    public void Facade_MirrorsItsBody_AtRest_InVerticesAndFaces()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), new Vector3(0.7f, 1.2f, -0.3f), 1, 3, 0, 5);

        var body = f.Body;
        var fromScene = f.GetVertices();
        var fromModel = body.Vertices();
        Assert.AreEqual(fromScene.Length, fromModel.Length);
        for (int i = 0; i < fromScene.Length; i++)
            Assert.AreEqual(0f, Vector3.Distance(fromScene[i], fromModel[i]), 1e-5f,
                $"вершина {i}: компонент и модель разошлись");

        var sceneFaces = f.GetFaces();
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

    /// <summary>Пока дверца открыта, transform несёт АНИМИРОВАННУЮ позу открытия
    /// (см. ApplyDoor/FacadeDoor.Pose), а логическая поза детали — закрытая,
    /// в _closedPos/_closedRot. Body обязан идти через тот же
    /// ValidationPositionAt/ValidationRotation, что и GetVerticesAt/GetFacesAt —
    /// иначе зеркальный тест выше проходит только потому, что дверца не открыта.</summary>
    [Test]
    public void Facade_WhileDoorOpen_StillMirrorsGetVerticesAt_NotAnimatedTransform()
    {
        var f = MakeFacade("F", new Vector3Int(400, 700, 18), new Vector3(0.5f, 0.9f, -0.2f));
        f.SetOpen(true);
        f.StepDoor(10f);
        Assert.IsFalse(f.IsDoorClosed, "дверца должна успеть открыться за 10 секунд анимации");

        var fromScene = f.GetVertices();
        var fromModel = f.Body.Vertices();
        Assert.AreEqual(fromScene.Length, fromModel.Length);
        for (int i = 0; i < fromScene.Length; i++)
            Assert.AreEqual(0f, Vector3.Distance(fromScene[i], fromModel[i]), 1e-5f,
                $"вершина {i}: во время открытия Body обязан читать закрытую позу, а не transform");
    }

    /// <summary>«Пассажир» — фасад, приклеенный к фронту ящика/посудомойки: его
    /// transform ведёт хозяин (см. IsPassenger, DrawerElement/DishwasherElement),
    /// а не собственная анимация двери. Body обязан следовать за этим движением
    /// так же, как GetVerticesAt.</summary>
    [Test]
    public void Facade_WhilePassenger_StillMirrorsGetVerticesAt_FollowsMovingTransform()
    {
        var f = MakeFacade("F", new Vector3Int(400, 700, 18), Vector3.zero);
        f.IsPassenger = true;
        f.transform.SetPositionAndRotation(
            new Vector3(2.1f, 0.4f, -1.1f), Quaternion.Euler(0f, 30f, 0f));

        var fromScene = f.GetVertices();
        var fromModel = f.Body.Vertices();
        Assert.AreEqual(fromScene.Length, fromModel.Length);
        for (int i = 0; i < fromScene.Length; i++)
            Assert.AreEqual(0f, Vector3.Distance(fromScene[i], fromModel[i]), 1e-5f,
                $"вершина {i}: пассажир обязан следовать за transform хозяина");
    }

    /// <summary>Фасад не умеет быть ребёнком в общем attach-ride (AttachRider.cs):
    /// CanFollowAnAttachParent=false исключает его из AttachLinks.CanBeChild, а
    /// значит AttachRider никогда не вызовет на нём BeginAttachRide. Собственная
    /// «езда» у фасада — открытие двери и режим пассажира, оба уже покрыты
    /// зеркальными тестами выше через facade-специфичный
    /// ValidationPositionAt/ValidationRotation.</summary>
    [Test]
    public void Facade_CannotBeChild_SoNeverEntersGenericAttachRide()
    {
        var f = MakeFacade("F", new Vector3Int(400, 700, 18), Vector3.zero);
        Assert.IsFalse(f.CanFollowAnAttachParent);
        Assert.IsFalse(AttachLinks.CanBeChild(f));
    }

    [Test]
    public void Facade_MirrorsItsBody_InGapsAndTraits()
    {
        var f = MakeFacade("F", new Vector3Int(400, 300, 18), Vector3.zero, 1, 2, 3, 4);

        Assert.AreEqual(FacadeBody.DEFAULT_GAP_MM, FacadeElement.DEFAULT_GAP_MM);
        Assert.AreEqual(FacadeBody.DISPLAY_TYPE_NAME, f.DisplayTypeName);
        Assert.AreEqual(FacadeBody.CUTOUT_ROLE, f.CutoutRole);
        Assert.AreEqual(FacadeBody.SUPPORTS_GAPS, f.SupportsGaps);
        Assert.AreEqual(f.GapMM, f.Body.GapMM);
        Assert.AreEqual(f.DimensionsMM, f.Body.DimensionsMM);
    }

    [Test]
    public void Facade_OverlappingAnother_GapIncluded()
    {
        // Two facades with gap=2 each, physical 400x300x18 placed 3mm apart
        // Effective: 404x304x18. Physical gap=3mm, effective edges overlap by 1mm
        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero, 2, 2, 2, 2);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.003f, 0f, 0f), 2, 2, 2, 2);

        var all = PartRegistry.GetAll();
        var vr = ConstraintValidator.Validate(all);
        Assert.IsTrue(vr.violations.Contains(a), "Effective bounds overlap → violation");
    }

    [Test]
    public void Facade_NoGap_TouchingEdges_NoViolation()
    {
        var a = MakeFacade("A", new Vector3Int(400, 300, 18), Vector3.zero, 0, 0, 0, 0);
        MakeFacade("B", new Vector3Int(400, 300, 18), new Vector3(0.4f, 0f, 0f), 0, 0, 0, 0);

        var all = PartRegistry.GetAll();
        var vr = ConstraintValidator.Validate(all);
        Assert.IsFalse(vr.violations.Contains(a));
    }

    [Test]
    public void Facade_ElementFactory_CreatesWithGaps()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(600, 400, 18), "TestFacade", Vector3.zero, 1, 2, 3, 4);
        _spawned.Add(go);

        var facade = go.GetComponent<FacadeElement>();
        Assert.IsNotNull(facade);
        Assert.AreEqual("TestFacade", facade.PartName);
        Assert.AreEqual(1, facade.GapLeft);
        Assert.AreEqual(2, facade.GapRight);
        Assert.AreEqual(3, facade.GapTop);
        Assert.AreEqual(4, facade.GapBottom);
        Assert.AreEqual(new Vector3Int(600, 400, 18), facade.DimensionsMM);
    }

    [Test]
    public void Facade_PhysicalScale_StaysAtDimensions()
    {
        var f = MakeFacade("F", new Vector3Int(500, 400, 18), Vector3.zero, 4, 4, 4, 4);
        Assert.AreEqual(0.5f, f.transform.localScale.x, 1e-5f);
        Assert.AreEqual(0.4f, f.transform.localScale.y, 1e-5f);
        Assert.AreEqual(0.018f, f.transform.localScale.z, 1e-5f);
    }

    [Test]
    public void Facade_Duplicate_PreservesGaps()
    {
        var src = MakeFacade("Src", new Vector3Int(500, 400, 18), Vector3.zero, 1, 2, 3, 4);
        var dupGo = ElementFactory.Duplicate(src);
        _spawned.Add(dupGo);

        var dup = dupGo.GetComponent<FacadeElement>();
        Assert.IsNotNull(dup);
        Assert.AreEqual(1, dup.GapLeft);
        Assert.AreEqual(2, dup.GapRight);
        Assert.AreEqual(3, dup.GapTop);
        Assert.AreEqual(4, dup.GapBottom);
    }
}
