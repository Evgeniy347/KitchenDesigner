using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож дефекта «WallManager.LateUpdate обходит PartRegistry.All каждый кадр,
/// отсеивая не-стены через GetComponent&lt;Wall&gt;()». Для кухни (десятки элементов) это
/// копейки, для дома на сотни элементов — уже нет: каждый кадр каждая деталь платит за
/// GetComponent, который почти всегда возвращает null. Фикс завёл отдельный реестр стен
/// (<see cref="PartRegistry.Walls"/>, наполняемый Wall.Awake/OnDestroy) — LateUpdate теперь
/// обходит только его. Считаем СОБЫТИЯ (<see cref="WallManager.TakeWallsVisited"/>) — сколько
/// элементов реально попало в тело цикла, а не миллисекунды, которые плавают от машины к
/// машине.</summary>
public class WallManagerForeignElementsPerfGuardTests
{
    private GameObject? _cameraGo;
    private WallManager? _wallManager;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement MakeWall(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<MeshRenderer>();
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        go.AddComponent<Wall>();
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private KitchenElement MakeForeignElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.AddComponent<MeshRenderer>();
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        WallManager.TakeWallsVisited();

        _cameraGo = new GameObject("TestCamera");
        _cameraGo!.tag = "MainCamera";
        _cameraGo!.AddComponent<Camera>();

        var go = new GameObject("WallManager");
        _wallManager = go.AddComponent<WallManager>();
        _spawned.Add(go);
    }

    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);

        WallManager.TakeWallsVisited();
    }

    /// <summary>Главный репродукт: много чужих деталей и одна стена — цикл обязан посетить
    /// РОВНО одну запись, а не всех зарегистрированных в PartRegistry.All. До фикса счётчик
    /// (посчитанный бы через GetComponent&lt;Wall&gt;() на каждой) рос бы вместе с числом
    /// чужих элементов; теперь он зависит только от числа стен.</summary>
    [Test]
    public void LateUpdate_WithManyForeignElements_VisitsOnlyTheWall()
    {
        MakeWall("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        for (int i = 0; i < 50; i++)
            MakeForeignElement($"Деталь{i}", new Vector3Int(600, 720, 560), new Vector3(i, 0, 0));

        _wallManager!.LateUpdate();

        Assert.AreEqual(1, WallManager.TakeWallsVisited(),
            "цикл обязан пройти только по стенам (одна штука), а не по всем 51 " +
            "зарегистрированным элементам");
    }

    /// <summary>Отрицательный контроль: сцена без единой стены — счётчик обязан остаться
    /// нулём, даже если чужих элементов много. Без него «1» из теста выше могло бы
    /// оказаться артефактом чего угодно, а не подсчётом именно стен.</summary>
    [Test]
    public void LateUpdate_WithNoWalls_VisitsNothing()
    {
        for (int i = 0; i < 20; i++)
            MakeForeignElement($"Деталь{i}", new Vector3Int(600, 720, 560), new Vector3(i, 0, 0));

        _wallManager!.LateUpdate();

        Assert.AreEqual(0, WallManager.TakeWallsVisited(),
            "без единой стены в сцене цикл обязан посетить ноль записей");
    }

    /// <summary>Положительный контроль к обоим тестам выше: несколько стен без единого
    /// чужого элемента обязаны быть посчитаны все до одной.</summary>
    [Test]
    public void LateUpdate_WithSeveralWalls_VisitsEachOfThem()
    {
        MakeWall("Стена1", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        MakeWall("Стена2", new Vector3Int(1800, 2500, 100), new Vector3(3, 1.25f, 0));
        MakeWall("Стена3", new Vector3Int(1600, 2500, 100), new Vector3(6, 1.25f, 0));

        _wallManager!.LateUpdate();

        Assert.AreEqual(3, WallManager.TakeWallsVisited(),
            "три зарегистрированные стены обязаны быть посчитаны все");
    }

    /// <summary>Доказывает, что реестр стен — не просто зеркало PartRegistry.All: удаление
    /// стены (Wall.OnDestroy) обязано убрать её из PartRegistry.Walls немедленно, а не только
    /// после следующей чистки мёртвых записей.</summary>
    [Test]
    public void DestroyedWall_RemovedFromWallRegistry_Immediately()
    {
        var wallElement = MakeWall("Стена", new Vector3Int(2000, 2500, 100), new Vector3(0, 1.25f, 0));
        Assert.AreEqual(1, PartRegistry.Walls.Count, "предусловие: стена в реестре");

        _spawned.Remove(wallElement.gameObject);
        Object.DestroyImmediate(wallElement.gameObject);

        Assert.AreEqual(0, PartRegistry.Walls.Count,
            "OnDestroy стены обязан снять её с реестра стен немедленно");
    }
}
