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
/// сдвиг, поворот, размер, зазор, паз, имя, группа — стоит здесь отдельным
/// тестом, а не пунктом в общем цикле: падение обязано НАЗЫВАТЬ способ.
///
/// Признак построен на ЗНАЧЕНИЯХ (поза, поза покоя, масштаб, габариты, зазоры,
/// пазы, имя, группа), а не на <c>Transform.hasChanged</c> и не на
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
        BoardSnapshotReuse.Clear();
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
        BoardSnapshotReuse.Clear();
    }

    /// <summary>Сцена из трёх деталей: две соседки нужны затем, чтобы «пересборок
    /// ровно одна» означало «кэш живой и всё равно пропустил изменение», а не
    /// «кэша нет».</summary>
    private List<KitchenElement> MakeSmallScene()
    {
        var scene = new List<KitchenElement>
        {
            MakePrimitiveElement("Subject", new Vector3Int(600, 720, 18), new Vector3(0f, 0.36f, 0f)),
            MakePrimitiveElement("Neighbour", new Vector3Int(600, 720, 18), new Vector3(0.6f, 0.36f, 0f)),
            MakePrimitiveElement("Shelf", new Vector3Int(564, 18, 560), new Vector3(0.3f, 0.4f, 0f)),
        };
        return scene;
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

    /// <summary>Обратный вход к сенсору цены и к ловушке, оплаченной прошлой
    /// сессией: запись в <c>transform</c> ТЕМ ЖЕ значением — не изменение.
    /// Признак, построенный на <c>hasChanged</c> или на <c>SceneRevision</c>,
    /// здесь пересобрал бы всю сцену и молча вернул бы цену кадра на место.</summary>
    [Test]
    public void WritingTheSamePositionAgain_CostsNoRebuild()
    {
        var scene = MakeSmallScene();
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
    /// <c>ValidationElement</c> прошлого кадра: у простой детали все поля,
    /// зависящие от СПИСКА (роль, индекс стены, индекс хозяина, второе тело,
    /// осевая линия), заведомо пусты, поэтому снимок не зависит от того, кто
    /// стоит рядом и на каком месте. Расширится <c>IsPlainBoard</c> — этот тест
    /// покраснеет раньше, чем кэш начнёт врать чужим индексом.</summary>
    [Test]
    public void APlainBoardSnapshot_CarriesNothingThatDependsOnTheRestOfTheScene()
    {
        var scene = MakeSmallScene();
        Assert.IsTrue(ValidationSnapshot.IsPlainBoard(scene[0]),
            "деталь стенда обязана быть простой доской — иначе тесты ниже мерили бы "
            + "отказ от переиспользования, а не переиспользование");

        var snapshot = SnapshotOf(scene, 0);

        Assert.AreEqual(ElementKind.None, snapshot.Kind, "простая доска не несёт ролей");
        Assert.AreEqual(ValidationElement.NoIndex, snapshot.AttachedWallIndex,
            "простая доска не привязана к стене");
        Assert.AreEqual(ValidationElement.NoIndex, snapshot.HostIndex,
            "у простой доски нет хозяина");
        Assert.IsFalse(snapshot.HasExtraBody, "у простой доски одно тело");
        Assert.IsNull(snapshot.PairedName, "простая доска ни с чем не спарена");
    }

    private void AssertTheChangeReachedTheSnapshot(string way,
        System.Action<KitchenElement> change, System.Func<ValidationElement, object> read)
    {
        var scene = MakeSmallScene();
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
        AssertTheChangeReachedTheSnapshot("сдвиг",
            e => e.transform.position += new Vector3(0.05f, 0f, 0f),
            s => s.Geometry.Min.x);

    [Test]
    public void RotatingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("поворот",
            e => e.transform.rotation = Quaternion.Euler(0f, 30f, 0f),
            s => s.Geometry.Max.z);

    [Test]
    public void ResizingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("изменение размера",
            e => e.DimensionsMM = new Vector3Int(900, 720, 18),
            s => s.Geometry.Max.x);

    [Test]
    public void ChangingAGap_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("правка зазора в панели",
            e => e.SetGap(GapSide.Left, 20),
            s => s.Geometry.Min.x);

    [Test]
    public void AddingAGroove_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("смена формы: паз",
            e => Assert.IsTrue(e.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top)),
                "паз обязан лечь на деталь стенда, иначе способ не проверен"),
            s => s.Geometry.GrooveSeatFaces.Length);

    [Test]
    public void RenamingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("переименование",
            e => e.PartName = "Renamed",
            s => s.Name);

    [Test]
    public void RegroupingABoard_ReachesTheSnapshot() =>
        AssertTheChangeReachedTheSnapshot("смена группы",
            e => e.GroupId = 7,
            s => s.GroupId);

    /// <summary>Деталь уехала из сцены — кэш обязан её отпустить, а не держать
    /// уничтоженный объект ключом. Урок <c>PartRegistry</c> из
    /// <c>agents/TEST-DESIGN.md</c>: статическая коллекция, пережившая своих
    /// читателей, потом падает в чужом тесте.</summary>
    [Test]
    public void ADestroyedBoard_LeavesTheCache()
    {
        var scene = MakeSmallScene();
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
