using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Два сенсора на переиспользование геометрии в
/// <c>ValidationSnapshot.Build</c>: сенсор РАБОТЫ (кадр жеста пересобирает одну
/// деталь, а не всю сцену) и сенсор ЧЕСТНОСТИ (деталь, изменённую любым из
/// способов приложения, снимок отдаёт заново, а не из кэша).
///
/// Второй сенсор дороже первого и важнее его. Замороженный снимок без
/// ЗВУЧАЩЕГО признака «эта деталь не менялась» — это молчаливая ложь
/// пользователю: подсветка показывает вчерашнюю геометрию, и ни один тест
/// производительности этого не видит. Поэтому каждый способ изменить деталь —
/// сдвиг, поворот, размер, зазор, паз, имя, группа, смена позы открывания,
/// перепривязка пары — стоит здесь отдельным тестом, а не пунктом в общем
/// цикле: падение обязано НАЗЫВАТЬ способ.
///
/// Признак построен на ЗНАЧЕНИЯХ (поза, поза покоя, признак
/// <c>PoseFollowsTransform</c>, масштаб, габариты, зазоры, пазы, имя, группа,
/// имя парной детали, наличие соседних компонентов <c>Wall</c> и
/// <c>BasePlate</c> и три числа стены — опущена ли, полная высота, полная
/// позиция), а не на <c>Transform.hasChanged</c> и не на
/// <c>SceneRevision</c>. Это не стилистический выбор: запись в <c>transform</c>
/// тем же значением поднимает <c>hasChanged</c>, <c>SceneChangeTracker.Poll</c>
/// бампит ревизию каждый кадр перетаскивания — кэш с таким ключом обесценивал
/// бы сам себя, и вся работа свелась бы к нулю
/// (<c>agents/TEST-DESIGN.md</c> → «Функция на покадровом пути…»).
/// Обратный вход на это записан отдельным тестом: та же позиция, записанная
/// второй раз, пересборки НЕ стоит.</summary>
public class ValidationSnapshotReuseTests : ElementTestBase
{
    private const int SceneSize = 400;

    private bool _suppressBefore;
    private bool _snapBefore;
    private bool _blockBefore;
    private ElementMover? _mover;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementSnapshotReuse.Clear();
        _suppressBefore = KitchenElement.SuppressVisualRebuild;
        _snapBefore = KitchenSettings.Instance.SnapEnabled;
        _blockBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
        var go = new GameObject("ElementMover");
        _spawned.Add(go);
        _mover = go.AddComponent<ElementMover>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_mover != null) _mover.FinishDragNow();
        if (_mover != null) _mover.RestoreDragMaterial();
        KitchenSettings.Instance.SnapEnabled = _snapBefore;
        KitchenSettings.Instance.BlockOnViolation = _blockBefore;
        KitchenElement.SuppressVisualRebuild = _suppressBefore;
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) UnityEngine.Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementSnapshotReuse.Clear();
    }

    /// <summary>Независимый судья кэша: снимок, собранный С кэшем, против
    /// снимка той же сцены, собранного с нуля. Сравнение — <c>ValidatesTheSameAs</c>,
    /// то есть имя, роль, группа, пара, индексы, высотный промежуток, габариты,
    /// осевая стены, КАЖДАЯ грань (вместе с осями грани), КАЖДОЕ сиденье и
    /// стенка паза и КАЖДАЯ вершина.
    ///
    /// Это ответ на вопрос, на который счётчик пересборок ответить не может.
    /// Ноль пересборок означает «работы не было» и ничего не говорит о том,
    /// правильная ли геометрия лежит в ответе; здесь спрашивается сама
    /// ГЕОМЕТРИЯ.</summary>
    private static void AssertTheCacheAgreesWithAColdBuild(List<KitchenElement> scene, string when)
    {
        var warm = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, warm);

        ElementSnapshotReuse.Clear();
        var cold = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, cold);

        Assert.AreEqual(cold.Count, warm.Count,
            $"{when}: снимок из кэша и снимок с нуля разной длины");
        for (int i = 0; i < cold.Count; i++)
            Assert.IsTrue(warm[i].ValidatesTheSameAs(cold[i]),
                $"{when}: деталь {cold[i].Name} пришла из кэша НЕ такой, какой её строят "
                + "заново — это и есть устаревшая геометрия, по которой валидация "
                + "судит молча");
    }

    private DrawerElement MakePrimitiveDrawer(string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        var d = go.AddComponent<DrawerElement>();
        d.PartName = name;
        PartRegistry.Register(d);
        _spawned.Add(go);
        return d;
    }

    /// <summary>Сцена вокруг подопытной детали: две соседки нужны затем, чтобы
    /// «пересборок ровно одна» означало «кэш живой и всё равно пропустил
    /// изменение», а не «кэша нет».</summary>
    private List<KitchenElement> SceneAround(KitchenElement subject)
    {
        return new List<KitchenElement>
        {
            subject,
            MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18), new Vector3(0.6f, 0.36f, 0f)),
            MakePrimitiveElement("Shelf", new Vector3Int(564, 18, 560), new Vector3(0.3f, 0.4f, 0f)),
        };
    }

    private KitchenElement MakeBoard(string name = "SubjectBoard") =>
        MakePrimitiveElement(name, new Vector3Int(600, 720, 18), new Vector3(0f, 0.36f, 0f));

    private FacadeElement MakeFacade(string name = "SubjectFacade") =>
        MakePrimitiveFacade(name, new Vector3Int(600, 716, 18), new Vector3(0f, 0.36f, 0f));

    private DrawerElement MakeDrawer(string name = "SubjectDrawer") =>
        MakePrimitiveDrawer(name, new Vector3(0f, 0.36f, 0f));

    /// <summary>Стена — через настоящую фабрику: она вешает на объект компонент
    /// <c>Wall</c>, а именно он решает и роль якоря, и осевую линию.</summary>
    private KitchenElement MakeWall(string name = "SubjectWall")
    {
        var go = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 100), name,
            new Vector3(0f, 1.35f, -2f));
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    /// <summary>Четыреста деталей строятся с погашенной перестройкой мешей:
    /// снимок валидации меша не читает вовсе (геометрия считается из позы,
    /// габаритов и зазоров), а <c>GrooveMesh.Build</c> на каждую из четырёхсот
    /// превратил бы сенсор цены в самый дорогой тест набора.</summary>
    private List<KitchenElement> MakeBigScene()
    {
        var scene = new List<KitchenElement>(SceneSize);
        KitchenElement.SuppressVisualRebuild = true;
        try
        {
            for (int i = 0; i < SceneSize; i++)
                scene.Add(MakePrimitiveElement($"Board{i}", new Vector3Int(600, 720, 18),
                    new Vector3(i * 0.7f, 0.36f, 0f)));
        }
        finally
        {
            KitchenElement.SuppressVisualRebuild = _suppressBefore;
        }
        return scene;
    }

    private static ValidationElement SnapshotOf(List<KitchenElement> scene, int index)
    {
        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);
        return into[index];
    }

    /// <summary>Сенсор цены. На коде до правки все три числа равнялись размеру
    /// сцены: снимок собирался целиком каждый кадр, и это была вся цена, ради
    /// которой инкрементальная валидация и делалась.
    ///
    /// Первый ассерт — положительный контроль, и он обязан стоять первым:
    /// без него «одна пересборка» ниже была бы зелёной на мёртвом счётчике.</summary>
    [Test]
    public void AFrameThatMovesOneBoardOutOfFourHundred_RebuildsThatBoardOnly()
    {
        var scene = MakeBigScene();
        var into = new List<ValidationElement>();

        ValidationSnapshot.TakeGeometryBuilds();
        ValidationSnapshot.Build(scene, into);
        Assert.AreEqual(SceneSize, ValidationSnapshot.TakeGeometryBuilds(),
            "первый снимок обязан построить геометрию ВСЕЙ сцены — если счётчик молчит уже "
            + "здесь, то «одна пересборка» ниже не измеряет ничего");

        ValidationSnapshot.Build(scene, into);
        Assert.AreEqual(0, ValidationSnapshot.TakeGeometryBuilds(),
            "кадр, в котором не изменилась ни одна деталь, не обязан строить ни одной "
            + "геометрии");

        var moved = scene[7];
        var before = SnapshotOf(scene, 7);
        ValidationSnapshot.TakeGeometryBuilds();

        moved.transform.position += new Vector3(0.05f, 0f, 0f);
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            $"кадр жеста, двигающий ОДНУ деталь из {SceneSize}, обязан пересобрать геометрию "
            + "одной детали: именно ради этого инкрементальный проход валидации и делался, "
            + "а полный снимок сводил его выигрыш к нулю");
        Assert.AreNotEqual(before.Geometry.Min.x, into[7].Geometry.Min.x,
            "деталь сдвинули — снимок обязан отдать новую геометрию, иначе счётчик выше "
            + "измеряет экономию на устаревших данных");
    }

    /// <summary>Тот же счётчик, прочитанный через прибор приложения. Он врал в
    /// большую сторону: <c>ConstraintValidator</c> прибавлял размер сцены
    /// безусловно, не зная про переиспользование. Сломанный прибор хуже
    /// отсутствующего — по нему меряют лаги.</summary>
    [Test]
    public void TheValidatorsOwnCounter_ReportsRealRebuilds_NotTheSceneSize()
    {
        var scene = SceneAround(MakeBoard());

        ConstraintValidator.Validate(scene);
        ConstraintValidator.TakeElementGeometriesBuilt();

        ConstraintValidator.Validate(scene);
        Assert.AreEqual(0, ConstraintValidator.TakeElementGeometriesBuilt(),
            "вторая валидация той же неизменной сцены не строит ни одной геометрии — "
            + "счётчик, показывающий здесь размер сцены, измеряет собственную формулу, "
            + "а не работу");

        scene[0].transform.position += new Vector3(0.05f, 0f, 0f);
        ConstraintValidator.Validate(scene);
        Assert.AreEqual(1, ConstraintValidator.TakeElementGeometriesBuilt(),
            "сдвинули одну деталь из трёх — счётчик обязан показать одну пересборку");
    }

    /// <summary>Обратный вход к сенсору цены и к ловушке, оплаченной прошлой
    /// сессией: запись в <c>transform</c> ТЕМ ЖЕ значением — не изменение.
    /// Признак, построенный на <c>hasChanged</c> или на <c>SceneRevision</c>,
    /// здесь пересобрал бы всю сцену и молча вернул бы цену кадра на место.</summary>
    [Test]
    public void WritingTheSamePositionAgain_CostsNoRebuild()
    {
        var scene = SceneAround(MakeBoard());
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        ValidationSnapshot.TakeGeometryBuilds();

        foreach (var e in scene) e.transform.position = e.transform.position;
        SceneChangeTracker.Poll();
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(0, ValidationSnapshot.TakeGeometryBuilds(),
            "позиции переписали теми же значениями — геометрия не изменилась ни у одной "
            + "детали, и пересобирать нечего");
    }

    /// <summary>Основание, на котором вообще законно переиспользовать ЦЕЛЫЙ
    /// <c>ValidationElement</c> прошлого кадра: у детали, взятой в кэш, поля,
    /// которые считаются ПО СПИСКУ — индекс стены, индекс хозяина, второе тело —
    /// заведомо пусты, поэтому снимок не зависит от того, кто стоит рядом и на
    /// каком месте. Роль и осевая линия пустыми быть не обязаны: они выводятся
    /// из самой детали и её компонентов, то есть из ключа.
    ///
    /// Перебираются ВСЕ типы настоящей фабрики, а не список из трёх, написанный
    /// руками: годность решает выведенное правило (<c>ReusesItsSnapshot</c>), и
    /// новый тип обязан попасть под проверку сам. Появится тип, который признан
    /// годным и при этом несёт индекс от списка, — тест покраснеет раньше, чем
    /// кэш начнёт врать чужим индексом.</summary>
    [Test]
    public void EverySnapshotKeptBetweenFrames_CarriesNothingThatDependsOnTheRestOfTheScene()
    {
        var scene = new List<KitchenElement> { MakeWall("WallUnderTest") };
        foreach (var (type, make) in EveryElementType.Makers)
        {
            var go = make(type.Name + "_probe");
            _spawned.Add(go);
            scene.Add(go.GetComponent<KitchenElement>());
        }

        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);

        int reusable = 0;
        for (int i = 0; i < scene.Count; i++)
        {
            if (!ValidationSnapshot.ReusesItsSnapshot(scene[i])) continue;
            reusable++;

            string who = scene[i].GetType().Name;
            Assert.AreEqual(ValidationElement.NoIndex, into[i].AttachedWallIndex,
                $"{who}: индекс стены считается ПО СПИСКУ — такой снимок нельзя нести через кадр");
            Assert.AreEqual(ValidationElement.NoIndex, into[i].HostIndex,
                $"{who}: индекс хозяина считается ПО СПИСКУ");
            Assert.IsFalse(into[i].HasExtraBody,
                $"{who}: второе тело считается от хозяина, то есть по СПИСКУ");
        }

        Assert.GreaterOrEqual(reusable, 5,
            "ни один тип не признан годным — правило годности отказало целиком, и все "
            + "утверждения выше проверены на пустом множестве");
    }

    /// <summary>Отрицательный контроль к правилу годности: тип, который строит
    /// свою коробку САМ (<c>PillarElement</c> переопределяет
    /// <c>GetVertices</c>/<c>GetFaces</c>), в кэш не берётся — признак о его
    /// полях ничего не знает.</summary>
    [Test]
    public void ATypeThatBuildsItsOwnBox_IsNotReused()
    {
        var go = ElementFactory.CreatePillar(713, "Pillar", Vector3.zero, 87);
        _spawned.Add(go);
        var pillar = go.GetComponent<KitchenElement>();

        Assert.IsFalse(ValidationSnapshot.ReusesItsSnapshot(pillar),
            "тип, собирающий свою геометрию сам, обязан строиться каждый кадр: ключ "
            + "описывает коробку базового класса, а не его");

        var scene = new List<KitchenElement> { pillar };
        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);
        ValidationSnapshot.TakeGeometryBuilds();
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "деталь вне кэша обязана пересобираться на КАЖДОМ кадре — ноль здесь означает, "
            + "что кэш взял её молча");
    }

    /// <summary>Положительный контроль к тому же правилу, и он обязан стоять
    /// рядом с отрицательным: наследник, который НЕ трогает геометрию, берётся
    /// в кэш. Сборный фасад отличается от фасада рамкой в МЕШЕ и строкой в
    /// ведомости; валидируется он той же коробкой, что и фасад.</summary>
    [Test]
    public void AnAssembledFacade_IsReused_BecauseItsBoxIsTheFacadesOwn()
    {
        var go = ElementFactory.CreateAssembledFacade(new Vector3Int(451, 719, 19),
            "Assembled", Vector3.zero, AssembledFill.Glass);
        _spawned.Add(go);
        var assembled = go.GetComponent<KitchenElement>();

        Assert.IsTrue(ValidationSnapshot.ReusesItsSnapshot(assembled),
            "сборный фасад не переопределяет ни одного члена, из которых собирается "
            + "коробка валидации — значит ключ фасада описывает и его");

        var scene = new List<KitchenElement> { assembled };
        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);
        ValidationSnapshot.TakeGeometryBuilds();
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(0, ValidationSnapshot.TakeGeometryBuilds(),
            "неизменившийся сборный фасад не обязан пересобираться");
    }

    private void AssertTheChangeReachedTheSnapshot(string way, KitchenElement subject,
        System.Action<KitchenElement> change, System.Func<ValidationElement, object> read)
    {
        var scene = SceneAround(subject);
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        object before = read(into[0]);
        ValidationSnapshot.TakeGeometryBuilds();

        change(scene[0]);
        SceneChangeTracker.Poll();
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            $"{way}: изменённую деталь обязано пересобрать, а две нетронутые — нет; "
            + "иное число означает либо слепой признак, либо мёртвый кэш, и тогда "
            + "утверждение ниже проверяет не то");
        Assert.AreNotEqual(before, read(into[0]),
            $"{way}: снимок отдал ПРЕЖНЮЮ геометрию — валидация судит по устаревшим "
            + "данным, и пользователь видит подсветку, которой в сцене уже нет");
    }

    [Test]
    public void MovingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("сдвиг доски", MakeBoard(),
            e => e.transform.position += new Vector3(0.05f, 0f, 0f),
            s => s.Geometry.Min.x);

    [Test]
    public void RotatingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("поворот доски", MakeBoard(),
            e => e.transform.rotation = Quaternion.Euler(0f, 30f, 0f),
            s => s.Geometry.Max.z);

    [Test]
    public void ResizingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("изменение размера доски", MakeBoard(),
            e => e.DimensionsMM = new Vector3Int(900, 720, 18),
            s => s.Geometry.Max.x);

    [Test]
    public void ChangingAGapOnABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("правка зазора доски", MakeBoard(),
            e => e.SetGap(GapSide.Left, 20),
            s => s.Geometry.Min.x);

    [Test]
    public void AddingAGroove_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("смена формы: паз", MakeBoard(),
            e => Assert.IsTrue(e.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)),
                "паз обязан лечь на деталь стенда, иначе способ не проверен"),
            s => s.Geometry.GrooveSeatFaces.Length);

    [Test]
    public void RenamingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("переименование", MakeBoard(),
            e => e.PartName = "Renamed",
            s => s.Name);

    [Test]
    public void RegroupingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("смена группы", MakeBoard(),
            e => e.GroupId = 7,
            s => s.GroupId);

    [Test]
    public void MovingAFacade_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("сдвиг фасада", MakeFacade(),
            e => e.transform.position += new Vector3(0.05f, 0f, 0f),
            s => s.Geometry.Min.x);

    [Test]
    public void ResizingAFacade_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("изменение размера фасада", MakeFacade(),
            e => e.DimensionsMM = new Vector3Int(900, 716, 18),
            s => s.Geometry.Max.x);

    /// <summary>Зазор навески у фасада — не только геометрия: положительный
    /// <c>GapMM</c> поднимает <c>FloatingFacade</c>, то есть МЕНЯЕТ РОЛЬ, по
    /// которой ядро решает, нужна ли детали опора. Устаревшая роль в кэше — это
    /// «фасад висит в воздухе, и никто не против».</summary>
    [Test]
    public void ChangingAFacadeGap_ReachesTheSnapshotAndItsRole()
    {
        var subject = MakeFacade();
        var scene = SceneAround(subject);
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        Assert.IsFalse(into[0].Is(ElementKind.FloatingFacade),
            "фасад стенда обязан начинать без зазора навески — иначе переход ниже не переход");
        ValidationSnapshot.TakeGeometryBuilds();

        subject.SetGap(GapSide.Left, 20);
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "зазор фасада изменили — деталь обязана пересобраться");
        Assert.IsTrue(into[0].Is(ElementKind.FloatingFacade),
            "роль «висящий фасад» обязана прийти из НОВОГО снимка: по ней ядро решает, "
            + "требовать ли опору");
    }

    [Test]
    public void MovingADrawer_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("сдвиг ящика", MakeDrawer(),
            e => e.transform.position += new Vector3(0.05f, 0f, 0f),
            s => s.Geometry.Min.x);

    [Test]
    public void RotatingADrawer_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("поворот ящика", MakeDrawer(),
            e => e.transform.rotation = Quaternion.Euler(0f, 30f, 0f),
            s => s.Geometry.Max.z);

    /// <summary>Имя парной детали не геометрия, но оно едет в снимок и решает,
    /// считать ли две детали одной парой. Оно не входит в Stamp — оно
    /// сверяется с тем, что лежит в сохранённом снимке; тест держит эту вторую
    /// половину признака.</summary>
    [Test]
    public void RepairingADrawerToAnotherOne_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("перепривязка пары ящиков", MakeDrawer(),
            e => ((DrawerElement)e).PairedDrawerName = "Upper",
            s => s.PairedName ?? "");

    [Test]
    public void MovingAWall_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("сдвиг стены", MakeWall(),
            e => e.transform.position += new Vector3(0.05f, 0f, 0f),
            s => s.Geometry.Min.x);

    /// <summary>Осевая линия стены — не украшение снимка: по ней прилипают
    /// перегородки и считаются проёмы. Устаревшая осевая линия — это снэп,
    /// который ведёт деталь не туда, и заметить это можно только глазами.
    /// Поэтому у стены проверяется именно она, а не габариты.</summary>
    [Test]
    public void MovingAWall_ReachesItsCentreline() =>
        AssertTheChangeReachedTheSnapshot("сдвиг стены: осевая линия", MakeWall(),
            e => e.transform.position += new Vector3(0f, 0f, 0.25f),
            s => s.Centreline.Start.z);

    /// <summary>Опущенная стена (режим осмотра сверху) валидируется по ПОЛНОЙ
    /// высоте: <c>IsLowered</c> подменяет и высоту, и центр обратно на
    /// запомненные. Значит <c>transform</c> стены уехал, а её коробка обязана
    /// остаться прежней — пара «пересобрали, но получили то же» здесь и есть
    /// правило продукта. Без первого ассерта признак мог бы проспать режим,
    /// без второго — «пересборка» получалась бы ценой стены, которая в
    /// валидации присела вместе с картинкой.</summary>
    [Test]
    public void LoweringAWall_IsRebuiltButKeepsItsFullHeightBox()
    {
        var subject = MakeWall();
        var wall = subject.GetComponent<Wall>();
        var scene = SceneAround(subject);
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        var fullSpan = into[0].HeightSpan;
        var fullTop = into[0].Geometry.Max.y;
        ValidationSnapshot.TakeGeometryBuilds();

        wall.SetLowered(true, 0.3f);
        Assert.IsTrue(wall.IsLowered, "стенд обязан реально опустить стену");
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "стену опустили — снимок обязан пересобраться: то же число в localScale.y "
            + "теперь значит другое");
        Assert.AreEqual(fullSpan.Min, into[0].HeightSpan.Min,
            "опущенная стена валидируется по ПОЛНОЙ высоте — иначе режим просмотра "
            + "начинает менять документ");
        Assert.AreEqual(fullTop, into[0].Geometry.Max.y,
            "верх коробки опущенной стены обязан остаться на месте");
    }

    /// <summary>Роль якоря приходит от СОСЕДНЕГО компонента, а не от типа:
    /// <c>BasePlate</c> вешают на объект в рантайме. Признак обязан спрашивать
    /// про него каждый кадр, иначе деталь, ставшая полом, останется в кэше
    /// обычной доской — и вся сцена повиснет без опоры молча.</summary>
    [Test]
    public void AddingABasePlateAtRuntime_ReachesTheSnapshot()
    {
        var subject = MakeBoard();
        var scene = SceneAround(subject);
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        Assert.IsFalse(into[0].Is(ElementKind.Anchor),
            "доска стенда обязана начинать НЕ якорем, иначе переход ниже не переход");
        ValidationSnapshot.TakeGeometryBuilds();

        subject.gameObject.AddComponent<BasePlate>();
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "деталь стала полом — снимок обязан пересобраться");
        Assert.IsTrue(into[0].Is(ElementKind.FloorAnchor),
            "роль «пол» обязана прийти из НОВОГО снимка: по ней ядро решает, на чём "
            + "стоит вся сцена");
    }

    /// <summary>Самый тонкий из способов, и единственный, который не виден ни в
    /// одном значении позы: фасад переходит в открытую позу. С этого момента
    /// валидируется ЗАПОМНЕННАЯ закрытая поза, а не <c>transform</c>, — то есть
    /// смысл тех же чисел меняется на противоположный. Признак держит это через
    /// <c>PoseFollowsTransform</c>.
    ///
    /// Второй ассерт — обратный вход к первому и правило продукта: открытая
    /// дверца валидируется по закрытой позе, поэтому геометрия обязана остаться
    /// той же. Без него «пересобрали» можно было бы получить, сломав правило.</summary>
    [Test]
    public void AFacadeSwitchingToTheOpenPose_IsRebuilt()
    {
        var subject = MakeFacade();
        var scene = SceneAround(subject);
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        var closedMin = into[0].Geometry.Min;
        ValidationSnapshot.TakeGeometryBuilds();

        subject.SetOpen(true);
        Assert.IsFalse(subject.PoseFollowsTransform,
            "стенд обязан реально перевести фасад в открытую позу — иначе ниже проверяется "
            + "не переход, а его отсутствие");
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "фасад сменил режим позы — снимок обязан пересобраться: те же числа в transform "
            + "теперь значат другое");
        Assert.AreEqual(closedMin, into[0].Geometry.Min,
            "открытая дверца валидируется по ЗАКРЫТОЙ позе — геометрия обязана остаться той "
            + "же; иначе «пересобрали» получено ценой сломанного правила");
    }

    [Test]
    public void ADrawerSwitchingToTheOpenPose_IsRebuilt()
    {
        var subject = MakeDrawer();
        var scene = SceneAround(subject);
        var into = new List<ValidationElement>();

        ValidationSnapshot.Build(scene, into);
        var closedMin = into[0].Geometry.Min;
        ValidationSnapshot.TakeGeometryBuilds();

        subject.SetOpen(true);
        Assert.IsFalse(subject.PoseFollowsTransform,
            "стенд обязан реально выдвинуть ящик — иначе ниже проверяется не переход");
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "ящик сменил режим позы — снимок обязан пересобраться");
        Assert.AreEqual(closedMin, into[0].Geometry.Min,
            "выдвинутый ящик валидируется по ЗАДВИНУТОЙ позе — геометрия обязана остаться "
            + "той же");
    }

    /// <summary>Сцена кадра жеста ровно того вида, на котором
    /// <c>DragFrameWorkTests</c> требует работы: деталь, соседка вплотную и пол.
    /// Соседка стоит так, что её грань совпадает с гранью детали в ИСХОДНОЙ
    /// позе — именно это и делает стенд ниже осмысленным.</summary>
    private KitchenElement StartDraggingNextToANeighbour()
    {
        var dragged = MakePrimitiveElement("Dragged", new Vector3Int(600, 720, 18),
            new Vector3(0f, 0.36f, 0f));
        MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18),
            new Vector3(0.6f, 0.36f, 0f));
        MakePrimitiveElement("Floor", new Vector3Int(4000, 18, 4000),
            new Vector3(0f, -0.009f, 0f));

        _mover!.BeginDragOn(dragged);
        _mover.SaveDragMaterial(dragged);
        return dragged;
    }

    /// <summary>ПЕРВЫЙ из двух разделяющих тестов, и он про ПРЕДПОСЫЛКУ, а не
    /// про кэш. Кандидат в 20 мм вглубь соседки, с включённым прилипанием,
    /// возвращает деталь ровно туда, где она стояла: грани совпадают, и
    /// <c>ElementMover</c> в <c>transform</c> вообще ничего не пишет
    /// («if (position != settled)»). Кадр, названный «сдвинувшим деталь», её
    /// не сдвинул.
    ///
    /// Поэтому ноль пересборок на таком кадре — не ложь кэша, а правда: работы
    /// не было. Второй ассерт это и доказывает, спрашивая ГЕОМЕТРИЮ, а не
    /// счётчик: снимок из кэша совпадает со снимком, собранным с нуля.
    ///
    /// Пока счётчик показывал размер сцены, эта подмена была не видна: сторож
    /// говорил «три детали построены» на кадре, где не двигалось ничего.</summary>
    [Test]
    public void ADragFrameWhoseSnapPullsThePartBack_MovesNothing_AndTheCacheStaysHonest()
    {
        KitchenSettings.Instance.SnapEnabled = true;
        var dragged = StartDraggingNextToANeighbour();
        var scene = PartRegistry.GetAll();
        var start = dragged.transform.position;

        ValidationSnapshot.Build(scene, new List<ValidationElement>());
        ConstraintValidator.TakeElementGeometriesBuilt();

        _mover!.DragFrameOn(start + new Vector3(0.02f, 0f, 0f));

        Assert.AreEqual(start, dragged.transform.position,
            "прилипание обязано вернуть деталь к грани соседки, то есть ровно в исходную "
            + "позу — если деталь всё же уехала, то ноль пересборок означает слепой "
            + "признак, и чинить надо ключ, а не стенд");
        Assert.AreEqual(0, ConstraintValidator.TakeElementGeometriesBuilt(),
            "деталь не сдвинулась ни на микрон — пересобирать нечего, и счётчик обязан "
            + "показать ноль, а не размер сцены");
        AssertTheCacheAgreesWithAColdBuild(scene, "кадр, где прилипание вернуло деталь");
    }

    /// <summary>ВТОРОЙ разделяющий тест: тот же стенд, но прилипание выключено,
    /// поэтому деталь действительно уезжает. Здесь обязаны сработать оба
    /// прибора — и счётчик, и геометрия. Если бы дефект был в приборе (счётчик
    /// не видит пересборку после перевода <c>ConstraintValidator</c> на
    /// <c>GeometryBuildsInLastPass</c>), красным стал бы именно этот тест, а не
    /// соседний.</summary>
    [Test]
    public void ADragFrameThatReallyMovesThePart_RebuildsItsGeometry()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var dragged = StartDraggingNextToANeighbour();
        var scene = PartRegistry.GetAll();
        var start = dragged.transform.position;

        ValidationSnapshot.Build(scene, new List<ValidationElement>());
        ConstraintValidator.TakeElementGeometriesBuilt();

        _mover!.DragFrameOn(start + new Vector3(0.05f, 0f, 0f));

        Assert.AreNotEqual(start.x, dragged.transform.position.x,
            "без прилипания кадр обязан реально сдвинуть деталь — иначе ниже проверяется "
            + "не работа прибора, а та же подмена предпосылки");
        Assert.GreaterOrEqual(ConstraintValidator.TakeElementGeometriesBuilt(), 1,
            "кадр, который РЕАЛЬНО сдвинул деталь, обязан построить её геометрию заново, "
            + "и счётчик приложения обязан это увидеть: по нему меряют лаги, и он же "
            + "единственный сенсор на устаревшую геометрию");

        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);
        int i = scene.IndexOf(dragged);
        ElementGeometry.BoundsOf(dragged.GetVertices(), out var min, out _);
        Assert.AreEqual(min.x, into[i].Geometry.Min.x, 1e-6f,
            "снимок обязан описывать НОВУЮ позу детали, а не ту, с которой начался жест");
        AssertTheCacheAgreesWithAColdBuild(scene, "кадр, где деталь действительно уехала");
    }

    /// <summary>Деталь уехала из сцены — кэш обязан её отпустить, а не держать
    /// уничтоженный объект ключом. Урок <c>PartRegistry</c> из
    /// <c>agents/TEST-DESIGN.md</c>: статическая коллекция, пережившая своих
    /// читателей, потом падает в чужом тесте.</summary>
    [Test]
    public void ADestroyedBoard_LeavesTheCache()
    {
        var scene = SceneAround(MakeBoard());
        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);

        var gone = scene[2];
        scene.RemoveAt(2);
        UnityEngine.Object.DestroyImmediate(gone.gameObject);

        ValidationSnapshot.Build(scene, into);
        ValidationSnapshot.TakeGeometryBuilds();

        ValidationSnapshot.Build(scene, into);
        Assert.AreEqual(0, ValidationSnapshot.TakeGeometryBuilds(),
            "уничтожение соседки не обязано ничего пересобирать у оставшихся");
        Assert.AreEqual(2, into.Count, "снимок обязан отдать ровно оставшиеся детали");
    }
}
