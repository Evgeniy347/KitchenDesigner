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
/// имя парной детали), а не на <c>Transform.hasChanged</c> и не на
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

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        ElementSnapshotReuse.Clear();
        _suppressBefore = KitchenElement.SuppressVisualRebuild;
    }

    [TearDown]
    public void TearDown()
    {
        KitchenElement.SuppressVisualRebuild = _suppressBefore;
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) UnityEngine.Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementSnapshotReuse.Clear();
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
    /// <c>ValidationElement</c> прошлого кадра: у детали, взятой в кэш, все
    /// поля, зависящие от СПИСКА (индекс стены, индекс хозяина, второе тело,
    /// осевая линия), заведомо пусты, поэтому снимок не зависит от того, кто
    /// стоит рядом и на каком месте. Роль при этом ПУСТОЙ быть не обязана —
    /// у ящика и фасада она своя, — но она выводится из типа и зазоров, то есть
    /// из ключа, а не из соседей.
    ///
    /// Расширится <c>ReusesItsSnapshot</c> на тип, у которого эти поля не пусты
    /// (стена, проём, винтовая опора, варочная), — тест покраснеет раньше, чем
    /// кэш начнёт врать чужим индексом.</summary>
    [Test]
    public void EverySnapshotKeptBetweenFrames_CarriesNothingThatDependsOnTheRestOfTheScene()
    {
        var subjects = new KitchenElement[] { MakeBoard(), MakeFacade(), MakeDrawer() };
        var scene = new List<KitchenElement>(subjects);
        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);

        for (int i = 0; i < subjects.Length; i++)
        {
            string who = subjects[i].GetType().Name;
            Assert.IsTrue(ValidationSnapshot.ReusesItsSnapshot(subjects[i]),
                $"{who}: деталь стенда обязана попадать в кэш — иначе тесты честности "
                + "мерили бы отказ от переиспользования, а не переиспользование");

            Assert.AreEqual(ValidationElement.NoIndex, into[i].AttachedWallIndex,
                $"{who}: индекс стены зависит от СПИСКА — такой снимок нельзя нести через кадр");
            Assert.AreEqual(ValidationElement.NoIndex, into[i].HostIndex,
                $"{who}: индекс хозяина зависит от СПИСКА");
            Assert.IsFalse(into[i].HasExtraBody,
                $"{who}: второе тело считается от хозяина, то есть от СПИСКА");
            Assert.IsFalse(into[i].Is(ElementKind.Anchor),
                $"{who}: якорь — роль, которую снимок обязан пересчитывать по сцене");
        }
    }

    /// <summary>Отрицательный контроль к правилу «тип ТОЧНО такой»: наследник в
    /// кэш не берётся. Сборный фасад — не фасад с точки зрения геометрии, и
    /// признак о его полях ничего не знает.</summary>
    [Test]
    public void ASubclassOfACachedType_IsNotReused()
    {
        KitchenElement.SuppressVisualRebuild = true;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Assembled";
        var assembled = go.AddComponent<AssembledFacadeElement>();
        assembled.PartName = "Assembled";
        PartRegistry.Register(assembled);
        _spawned.Add(go);
        KitchenElement.SuppressVisualRebuild = _suppressBefore;

        Assert.IsFalse(ValidationSnapshot.ReusesItsSnapshot(assembled),
            "наследник кэшируемого типа обязан строиться каждый кадр: его геометрия "
            + "складывается из полей, которых признак не видит");

        var scene = new List<KitchenElement> { assembled };
        var into = new List<ValidationElement>();
        ValidationSnapshot.Build(scene, into);
        ValidationSnapshot.TakeGeometryBuilds();
        ValidationSnapshot.Build(scene, into);

        Assert.AreEqual(1, ValidationSnapshot.TakeGeometryBuilds(),
            "деталь вне кэша обязана пересобираться на КАЖДОМ кадре — ноль здесь означает, "
            + "что кэш взял её молча");
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
