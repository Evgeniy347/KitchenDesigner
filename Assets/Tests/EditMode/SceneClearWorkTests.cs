using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Очистка сцены перед загрузкой проекта — самый дорогой кадр из всех, что
/// прибор видел за две сессии: 860 мс и 548 обходов сцены на кадре 2789 дампа
/// пользователя. Удаление шло по одной детали, и каждое <c>PartRegistry.Unregister</c>
/// заново объявляло сцене, что состав изменился: отметка членства, бамп ревизии группы,
/// бамп ревизии сцены, сброс видимости — 411 раз подряд, и на каждое объявление
/// откликались слушатели.
///
/// Сенсор считает РАБОТУ через уже существующий счётчик <c>SceneRevision.TakeBumps</c>:
/// объявлений о смене состава должно быть столько, сколько самих очисток, а не столько,
/// сколько удалённых деталей. Время здесь не меряется вовсе.
///
/// Третий тест сторожит цену быстрого пути: он берётся ТОЛЬКО когда список покрывает
/// весь реестр. Вычистить реестр целиком по списку из двух деталей значило бы потерять
/// из него все остальные — а это молчаливая потеря сцены, не экономия.</summary>
public class SceneClearWorkTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Make(string name)
    {
        var go = new GameObject(name);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = new Vector3Int(800, 400, 18);
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void Setup() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    [Test]
    public void ClearingTheWholeScene_AnnouncesTheChangeOnce_NotOncePerElement()
    {
        var plate = Make("BasePlate");
        plate.gameObject.AddComponent<BasePlate>();
        for (int i = 0; i < 20; i++) Make("Board" + i);

        var all = PartRegistry.GetAll();
        SceneRevision.TakeBumps();

        SaveLoadManager.ClearBoards(all);

        int bumps = SceneRevision.TakeBumps();
        Assert.LessOrEqual(bumps, 2,
            $"объявлений о смене состава было {bumps} на 20 удалённых деталей. "
            + "Реестр очищается ОДНИМ действием, уцелевшие возвращаются обратно — "
            + "объявление на каждую деталь и есть та работа, что дала 860 мс на кадре 2789");
    }

    [Test]
    public void ClearingTheWholeScene_KeepsTheBasePlate_AndEmptiesTheRest()
    {
        var plate = Make("BasePlate");
        plate.gameObject.AddComponent<BasePlate>();
        var board = Make("Board");

        SaveLoadManager.ClearBoards(PartRegistry.GetAll());

        var left = PartRegistry.GetAll();
        Assert.AreEqual(1, left.Count, "в реестре обязана остаться ровно опорная плита");
        Assert.AreSame(plate, left[0],
            "быстрый путь чистит реестр целиком — уцелевшие обязаны вернуться в него, "
            + "иначе плита есть в сцене, но её нет ни в одном обходе");
        Assert.IsTrue(board == null || board.Equals(null), "деталь удалена");
    }

    [Test]
    public void ClearingOnlyPartOfTheScene_LeavesTheRestRegistered()
    {
        var doomed = Make("Doomed");
        var bystander = Make("Bystander");

        SaveLoadManager.ClearBoards(new List<KitchenElement> { doomed });

        var left = PartRegistry.GetAll();
        Assert.AreEqual(1, left.Count,
            "список покрывает не весь реестр, значит быстрый путь брать НЕЛЬЗЯ: "
            + "очистка реестра целиком выкинула бы из него деталь, которую никто не удалял");
        Assert.AreSame(bystander, left[0], "непричастная деталь остаётся в реестре");
        Assert.IsTrue(doomed == null || doomed.Equals(null), "названная деталь удалена");
    }
}
