using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Открывание дверцы стоило 216 мс на кадр при нулевом мусоре и нулевой отрисовке: чистый
/// CPU-обход. Причина — самоотравляющийся кэш. Дверца двигала СВОЙ трансформ, `SceneChangeTracker`
/// видел `hasChanged`, звал `SettleDerivedLinks` и поднимал ревизию сцены — каждый кадр. Кэш
/// предельного прогресса сверялся с этой ревизией и потому не совпадал НИКОГДА, так что
/// `FindMaxProgress` заново строил препятствия по всей сцене и гнал 128 шагов сканирования на
/// каждом кадре жеста.
///
/// Тесты считают ВЫЗОВЫ, а не миллисекунды: счётчик воспроизводим на любой машине, время нет.
/// Два сенсора — `OpeningCollision.TakeBuildObstacleCalls()` и `SceneRevision.TakeBumps()`.
///
/// Дефект возвращается, если кто-то снимет `SceneChangeTracker.NoteSelfAnimated` в `StepDoor`
/// или начнёт пересчитывать предел мимо `_obstacleCheckRevision`: тогда счётчики вырастают с
/// 1–2 до числа кадров жеста, и эти тесты краснеют. Проверено ревертом обеих правок.
/// </summary>
public class FacadeOpeningCostTests
{
    private const float Dt = 1f / 60f;
    private const int GestureFrames = 40;   // 0,67 с при длительности анимации 0,4 с

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        SceneRevision.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        SceneRevision.Reset();
    }

    /// <summary>Выдвижной режим (`DrawerOut`) — чистый сдвиг вперёд на известное расстояние,
    /// поэтому препятствие ставится по линейке и предел жеста считается в уме.</summary>
    private FacadeElement MakeSlidingFacade()
    {
        var go = new GameObject("Дверца");
        var f = go.AddComponent<FacadeElement>();
        f.PartName = "Дверца";
        f.DimensionsMM = new Vector3Int(400, 700, 18);
        f.Mode = DoorMode.DrawerOut;
        go.transform.position = Vector3.zero;
        PartRegistry.Register(f);
        _spawned.Add(go);
        return f;
    }

    private KitchenElement MakeObstacleInFrontOfTheDoor(float gapMm)
    {
        float boardHalf = 9f * AppConstants.MM_TO_UNITS;
        float z = boardHalf + gapMm * AppConstants.MM_TO_UNITS + boardHalf;
        var go = ElementFactory.CreatePart(new Vector3Int(400, 700, 18), "Препятствие",
            new Vector3(0f, 0f, z));
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>()!;
    }

    /// <summary>Кадр приложения целиком: `Update` двигает дверцу, `LateUpdate` опрашивает сцену.
    /// Без второй половины дефект не воспроизводится — он живёт именно в их паре.</summary>
    private void RunGesture(FacadeElement f, bool bumpEveryFrame, int frames = GestureFrames)
    {
        for (int i = 0; i < frames; i++)
        {
            if (bumpEveryFrame) SceneRevision.Bump();
            f.StepDoor(Dt);
            SceneChangeTracker.Poll();
        }
    }

    private void SettleTheSceneAndZeroTheSensors()
    {
        SceneChangeTracker.Poll();
        SceneChangeTracker.Poll();
        OpeningCollision.TakeBuildObstacleCalls();
        SceneRevision.TakeBumps();
    }

    [Test]
    public void BlockedGesture_BuildsObstacles_OnceOrTwice_NotEveryFrame()
    {
        var f = MakeSlidingFacade();
        MakeObstacleInFrontOfTheDoor(100f);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);

        int builds = OpeningCollision.TakeBuildObstacleCalls();
        Assert.LessOrEqual(builds, 2,
            $"препятствия за время одного жеста не двигаются, поэтому предельный прогресс "
            + $"считается один раз (плюс один пересчёт, когда сцена доседает после последнего "
            + $"движения). Получено {builds} построений за {GestureFrames} кадров — значит кэш "
            + "снова протух на собственном движении дверцы");
        Assert.Greater(builds, 0, "препятствия обязаны быть построены хотя бы раз, иначе дверца слепа");
    }

    [Test]
    public void OpenGesture_DoesNotBumpSceneRevision_EveryFrame()
    {
        var f = MakeSlidingFacade();
        MakeObstacleInFrontOfTheDoor(100f);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);

        int bumps = SceneRevision.TakeBumps();
        Assert.LessOrEqual(bumps, 2,
            $"собственное движение анимируемой дверцы — не изменение сцены. Ревизия сдвинулась "
            + $"{bumps} раз за {GestureFrames} кадров: столько же раз отработали "
            + "`SettleDerivedLinks`, `ScrewLegHostLink.ApplyAll`, `PipeFittingSizeLink.ApplyAll` "
            + "и полный `SceneAnalyzer.Analyze` в панели ошибок");
    }

    /// <summary>Главный сторож поведения: кэш не имеет права сдвинуть точку упора. Прогон с
    /// принудительным бампом ревизии на каждом кадре — это в точности старый, покадровый
    /// пересчёт; оба обязаны дать одно и то же число.</summary>
    [Test]
    public void CachedLimit_StopsTheDoor_AtExactlyTheSameProgress_AsAPerFrameScan()
    {
        var f = MakeSlidingFacade();
        MakeObstacleInFrontOfTheDoor(100f);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: true);
        float perFrameScan = f.DoorProgress;

        f.ForceClose();
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);
        float cached = f.DoorProgress;

        Assert.Less(perFrameScan, 1f,
            "препятствие в 100 мм перед дверцей обязано её остановить — иначе тест сравнивает "
            + "два беспрепятственных открывания и не проверяет ничего");
        Assert.AreEqual(perFrameScan, cached, 1e-6f,
            "кэшированный предел разошёлся с покадровым — дверца упирается не там, где раньше");
    }

    /// <summary>Обычный случай, которого никто не просил чинить: беспрепятственная дверца
    /// обязана по-прежнему открываться до конца.</summary>
    [Test]
    public void UnobstructedDoor_StillOpensFully()
    {
        var f = MakeSlidingFacade();
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);

        Assert.AreEqual(1f, f.DoorProgress, 1e-4f,
            "пустая сцена ничего не загораживает — дверца открывается полностью");
    }

    /// <summary>Кэш живёт на жесте, но обязан заметить настоящее изменение сцены посреди него:
    /// пользователь удалил соседний шкаф — дверца обязана поехать дальше.</summary>
    [Test]
    public void ObstacleRemovedMidGesture_LetsTheDoorContinue()
    {
        var f = MakeSlidingFacade();
        var obstacle = MakeObstacleInFrontOfTheDoor(100f);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);
        float stopped = f.DoorProgress;
        Assert.Less(stopped, 1f, "дверца обязана упереться в препятствие");

        PartRegistry.Unregister(obstacle);
        Object.DestroyImmediate(obstacle.gameObject);

        RunGesture(f, bumpEveryFrame: false);

        Assert.AreEqual(1f, f.DoorProgress, 1e-4f,
            "препятствие убрали посреди жеста, ревизия сцены сдвинулась — кэш обязан был "
            + "протухнуть и пересчитать предел, иначе дверца навсегда упирается в пустоту");
    }

    /// <summary>Второе изменение того же рода: препятствие не удалили, а отодвинули. Изменение
    /// приходит через `hasChanged` ЧУЖОГО элемента, а не через реестр.</summary>
    [Test]
    public void ObstacleMovedAwayMidGesture_LetsTheDoorContinue()
    {
        var f = MakeSlidingFacade();
        var obstacle = MakeObstacleInFrontOfTheDoor(100f);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);
        Assert.Less(f.DoorProgress, 1f, "дверца обязана упереться в препятствие");

        obstacle.transform.position += Vector3.right * 2f;
        RunGesture(f, bumpEveryFrame: false);

        Assert.AreEqual(1f, f.DoorProgress, 1e-4f,
            "соседа отодвинули — `SceneChangeTracker` обязан был увидеть ЕГО `hasChanged` "
            + "(гасится только собственное движение анимируемой дверцы) и сдвинуть ревизию");
    }

    /// <summary>Сторож самого сенсора: без глушения он действительно считает по вызову на кадр.
    /// Тест, который не может покраснеть, ничего не стоит — здесь красный воспроизводится
    /// принудительным бампом, то есть ровно тем, что делал `SceneChangeTracker` до правки.</summary>
    // ───────────────────────── тот же дефект у ящика ─────────────────────────

    /// <summary>Ящик болел ровно тем же: `ApplyAnimPose` двигал собственный трансформ каждый
    /// кадр и НЕ звал `NoteSelfAnimated`, а `FindMaxProgress` гонялся безусловно, без всякого
    /// кэша. Сенсоры и пороги здесь те же, что у дверцы, — потому что дефект количественно
    /// тот же.</summary>
    private DrawerElement MakeDrawer()
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400,
            "Ящик", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<DrawerElement>()!;
    }

    /// <summary>Ящик выезжает вперёд по локальному Z; половина глубины 350мм — 175мм.</summary>
    private KitchenElement MakeObstacleInFrontOfTheDrawer(float gapMm)
    {
        float z = (175f + gapMm + 9f) * AppConstants.MM_TO_UNITS;
        var go = ElementFactory.CreatePart(new Vector3Int(400, 700, 18), "Препятствие",
            new Vector3(0f, 0f, z));
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>()!;
    }

    private void RunDrawerGesture(DrawerElement d, bool bumpEveryFrame, int frames = GestureFrames)
    {
        for (int i = 0; i < frames; i++)
        {
            if (bumpEveryFrame) SceneRevision.Bump();
            d.StepAnimation(Dt);
            SceneChangeTracker.Poll();
        }
    }

    [Test]
    public void BlockedDrawerGesture_BuildsObstacles_OnceOrTwice_NotEveryFrame()
    {
        var d = MakeDrawer();
        MakeObstacleInFrontOfTheDrawer(100f);
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: false);

        int builds = OpeningCollision.TakeBuildObstacleCalls();
        Assert.LessOrEqual(builds, 2,
            $"препятствия за время выдвижения не двигаются — предел жеста считается один раз. "
            + $"Получено {builds} построений за {GestureFrames} кадров: у ящика кэша предела "
            + "не было вовсе, `FindMaxProgress` звался безусловно на каждом кадре");
        Assert.Greater(builds, 0, "препятствия обязаны быть построены хотя бы раз, иначе ящик слеп");
    }

    [Test]
    public void DrawerGesture_DoesNotBumpSceneRevision_EveryFrame()
    {
        var d = MakeDrawer();
        MakeObstacleInFrontOfTheDrawer(100f);
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: false);

        int bumps = SceneRevision.TakeBumps();
        Assert.LessOrEqual(bumps, 2,
            $"собственное движение выезжающего ящика — не изменение сцены. Ревизия сдвинулась "
            + $"{bumps} раз за {GestureFrames} кадров: столько же раз отработали "
            + "`SettleDerivedLinks`, обе `ApplyAll` и полный `SceneAnalyzer.Analyze`");
    }

    [Test]
    public void DrawerCachedLimit_StopsTheDrawer_AtExactlyTheSameProgress_AsAPerFrameScan()
    {
        var d = MakeDrawer();
        MakeObstacleInFrontOfTheDrawer(100f);
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: true);
        float perFrameScan = d.AnimProgress;

        d.ForceClose();
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: false);
        float cached = d.AnimProgress;

        Assert.Less(perFrameScan, 1f,
            "препятствие в 100 мм перед ящиком обязано его остановить — иначе тест сравнивает "
            + "два беспрепятственных выдвижения и не проверяет ничего");
        Assert.AreEqual(perFrameScan, cached, 1e-6f,
            "кэшированный предел разошёлся с покадровым — ящик упирается не там, где раньше");
    }

    [Test]
    public void UnobstructedDrawer_StillOpensFully()
    {
        var d = MakeDrawer();
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: false);

        Assert.AreEqual(1f, d.AnimProgress, 1e-4f,
            "пустая сцена ничего не загораживает — ящик выезжает полностью");
    }

    [Test]
    public void ObstacleRemovedMidDrawerGesture_LetsTheDrawerContinue()
    {
        var d = MakeDrawer();
        var obstacle = MakeObstacleInFrontOfTheDrawer(100f);
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: false);
        Assert.Less(d.AnimProgress, 1f, "ящик обязан упереться в препятствие");

        PartRegistry.Unregister(obstacle);
        Object.DestroyImmediate(obstacle.gameObject);

        RunDrawerGesture(d, bumpEveryFrame: false);

        Assert.AreEqual(1f, d.AnimProgress, 1e-4f,
            "препятствие убрали посреди жеста — кэш обязан протухнуть по ревизии сцены, "
            + "иначе ящик навсегда упирается в пустоту");
    }

    [Test]
    public void DrawerSensor_GoesRed_WhenTheRevisionIsPoisonedEveryFrame()
    {
        var d = MakeDrawer();
        MakeObstacleInFrontOfTheDrawer(100f);
        SettleTheSceneAndZeroTheSensors();

        d.SetOpen(true);
        RunDrawerGesture(d, bumpEveryFrame: true);

        int builds = OpeningCollision.TakeBuildObstacleCalls();
        Assert.Greater(builds, 10,
            "если ревизию травить каждый кадр, кэш ящика обязан промахиваться каждый кадр — "
            + "иначе сенсор считает не то, и зелёный в соседних тестах ничего не доказывает");
    }

    [Test]
    public void Sensor_GoesRed_WhenTheRevisionIsPoisonedEveryFrame()
    {
        var f = MakeSlidingFacade();
        MakeObstacleInFrontOfTheDoor(100f);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: true);

        int builds = OpeningCollision.TakeBuildObstacleCalls();
        Assert.Greater(builds, 10,
            "если ревизию травить каждый кадр, кэш обязан промахиваться каждый кадр — иначе "
            + "сенсор считает не то, и зелёный в соседних тестах ничего не доказывает");
    }
}
