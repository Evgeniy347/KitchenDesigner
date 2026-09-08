using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Кто может ехать за родителем и кто может его везти.
///
/// Раньше ответ давала лестница из десяти `is XxxElement` внутри
/// AttachLinks.CanBeChild, а «почему именно эти» объяснял комментарий. Теперь
/// на вопрос отвечает сам элемент (CanFollowAnAttachParent /
/// CanCarryAttachedParts), а причины — здесь, по одному Assert на тип.</summary>
public class AttachCapabilityTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private T Adopt<T>(GameObject go) where T : KitchenElement
    {
        _spawned.Add(go);
        return go.GetComponent<T>();
    }

    private KitchenElement Board()
        => Adopt<KitchenElement>(ElementFactory.CreatePart(new Vector3Int(600, 18, 400), "Полка", Vector3.zero));

    [Test]
    public void StaticParts_CanRideAParent_AndCarryChildren()
    {
        var board = Board();
        var shelf = Adopt<RadialShelfElement>(
            ElementFactory.CreateRadialShelf(400, 400, 18, 100, "Радиус", Vector3.zero));
        var pillar = Adopt<PillarElement>(
            ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "Опора", Vector3.zero));

        foreach (var e in new KitchenElement[] { board, shelf, pillar })
        {
            Assert.IsTrue(AttachLinks.CanBeChild(e), $"{e.DisplayTypeName}: статичная деталь едет за родителем");
            Assert.IsTrue(AttachLinks.CanBeParent(e), $"{e.DisplayTypeName}: и сама может везти детей");
        }
    }

    [Test]
    public void Facade_IsTheOneThatCarriesButNeverRides()
    {
        var facade = Adopt<FacadeElement>(
            ElementFactory.CreateFacade(new Vector3Int(400, 700, 18), "Фасад", Vector3.zero, 2, 2, 2, 2));

        Assert.IsFalse(AttachLinks.CanBeChild(facade),
            "у фасада своя кинематика — он открывается сам и вторым хозяином трансформа не обзаводится");
        Assert.IsTrue(AttachLinks.CanBeParent(facade),
            "ради этого механика и затевалась: фасад — лицо нестандартного ящика, "
            + "и его открывание обязано везти короб");
    }

    [Test]
    public void ElementsWithTheirOwnKinematicsOrOwner_NeitherRideNorCarry()
    {
        var cases = new (string why, KitchenElement element)[]
        {
            ("ящик выдвигается сам", Adopt<DrawerElement>(ElementFactory.CreateDrawer(
                DrawerType.A, 350, DrawerColor.Anthracite, 400, "Ящик", Vector3.zero))),
            ("посудомойка открывает дверцу сама, а её фасад — вовсе пассажир",
                Adopt<DishwasherElement>(ElementFactory.CreateDishwasher("ПММ", Vector3.zero))),
            ("духовка открывает дверцу сама", Adopt<OvenElement>(
                ElementFactory.CreateOven("Духовка", Vector3.zero))),
            ("окно ведёт стена", Adopt<WindowElement>(ElementFactory.CreateWindow(
                new Vector3Int(800, 1200, 100), "Окно", Vector3.zero))),
            ("дверь ведёт стена", Adopt<DoorElement>(ElementFactory.CreateDoor(
                new Vector3Int(800, 2000, 100), "Дверь", Vector3.zero))),
            ("мойка врезана в деталь и ходит за ней", Adopt<SinkElement>(
                ElementFactory.CreateSink("Мойка", Vector3.zero))),
            ("варочная врезана в деталь и ходит за ней", Adopt<CooktopElement>(
                ElementFactory.CreateCooktop("Варочная", Vector3.zero))),
            ("пол — не часть сборки", Adopt<FloorElement>(ElementFactory.CreateFloor(
                new Vector3Int(3000, 100, 3000), "Пол", Vector3.zero))),
            ("светильник — не часть сборки", Adopt<LightSourceElement>(
                ElementFactory.CreateLightSource("Лампа", Vector3.zero))),
        };

        foreach (var (why, element) in cases)
        {
            Assert.IsFalse(AttachLinks.CanBeChild(element),
                $"{element.GetType().Name}: {why} — два хозяина у одного трансформа дают разъезжающуюся позу");
            Assert.IsFalse(AttachLinks.CanBeParent(element),
                $"{element.GetType().Name}: {why} — и в родители тоже не годится");
        }
    }

    [Test]
    public void PlumbingParts_NeitherRideNorCarry_LinksLiveOnPorts()
    {
        var cases = new (string why, KitchenElement element)[]
        {
            ("труба стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeElement>(ElementFactory.CreatePipe(
                    KitchenDesigner.Core.Plumbing.PipeSpec.DEFAULT_SIZE, 500, "Труба", Vector3.zero))),
            ("отвод стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeElbowElement>(ElementFactory.CreatePipeElbow("Отвод", Vector3.zero))),
            ("муфта стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeCouplingElement>(ElementFactory.CreatePipeCoupling("Муфта", Vector3.zero))),
            ("тройник стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeTeeElement>(ElementFactory.CreatePipeTee("Тройник", Vector3.zero))),
            ("заглушка стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeCapElement>(ElementFactory.CreatePipeCap("Заглушка", Vector3.zero))),
            ("подача стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeSupplyElement>(ElementFactory.CreatePipeSupply("Подача", Vector3.zero))),
            ("обратка стыкуется портами, а не полем «Прикрепить к»",
                Adopt<PipeReturnElement>(ElementFactory.CreatePipeReturn("Обратка", Vector3.zero))),
        };

        foreach (var (why, element) in cases)
        {
            Assert.IsFalse(AttachLinks.CanBeChild(element),
                $"{element.GetType().Name}: {why}");
            Assert.IsFalse(AttachLinks.CanBeParent(element),
                $"{element.GetType().Name}: {why} — и в родители тоже не годится");
        }
    }

    [Test]
    public void WallAndBasePlate_AreNotPartsOfAnAssembly()
    {
        var wall = Adopt<KitchenElement>(
            ElementFactory.CreateWall(new Vector3Int(3000, 2700, 100), "Стена", Vector3.zero));
        var plate = Board();
        plate.gameObject.AddComponent<BasePlate>();

        Assert.IsFalse(AttachLinks.CanBeChild(wall), "стена — не часть сборки");
        Assert.IsFalse(AttachLinks.CanBeParent(wall));
        Assert.IsFalse(AttachLinks.CanBeChild(plate), "подложка — тоже");
        Assert.IsFalse(AttachLinks.CanBeParent(plate));
    }

    [Test]
    public void AttachLinks_AsksNoElementForItsConcreteType()
    {
        string path = Path.Combine(Application.dataPath,
            "Scripts", "Core", "Elements", "AttachLinks.cs");
        Assert.IsTrue(File.Exists(path), "сканер смотрит в несуществующий файл — он бы прошёл, ничего не проверив");

        string source = File.ReadAllText(path);
        Assert.IsTrue(source.Contains("CanFollowAnAttachParent"),
            "сканер читает не тот файл: в AttachLinks обязан быть вопрос о способности, а не о типе");

        var ladder = Regex.Matches(source, @"is\s+(\w+Element)\b")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

        Assert.IsEmpty(ladder,
            "AttachLinks снова спрашивает конкретный тип элемента: " + string.Join(", ", ladder)
            + ". Спрашивать надо способность (CanFollowAnAttachParent / CanCarryAttachedParts) — "
            + "иначе каждый новый тип элемента требует правки этой лестницы, и однажды её забудут");
    }
}
