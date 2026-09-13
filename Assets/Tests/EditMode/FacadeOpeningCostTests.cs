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
/// или начнёт пересчитывать предел мимо памяти `OpeningScanMemo`: тогда счётчики вырастают с
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

    /// <summary>КОРЕНЬ семнадцати миллисекунд. `BuildObstacles` не отсеивает по расстоянию
    /// вовсе: если сосед не КАСАЕТСЯ закрытой коробки, `ContactShadow.ActivePieces` кладёт
    /// его в препятствия ЦЕЛИКОМ (`Touches` ложно → `into.Add(neighbour)`). На проекте
    /// пользователя это 411 коробок, и каждая из 128 ступеней скана проверяла их все.
    ///
    /// Отсев по достижимости: дверца метёт заведомо ограниченный объём, и всё, что вне
    /// него, не может ей помешать НИКОГДА. Объём считается один раз по тем же 128
    /// ступеням (это дёшево: `getBoxes` строит одну-две коробки) и расширяется на
    /// `TouchGapMm` = 5 мм — запас с трёхсоткратным перекрытием над выпуклостью дуги между
    /// соседними ступенями (0,7° при радиусе 0,8 м — это 15 мкм) и над порогом
    /// `MinBlockingPenetrationMm` = 1 мм, на котором вообще считается блокировка.</summary>
    [Test]
    public void ObstaclesOutOfReach_AreDroppedBeforeTheScan_AndTheOneOnThePathIsNot()
    {
        var f = MakeSlidingFacade();
        MakeObstacleInFrontOfTheDoor(100f);
        for (int i = 0; i < 20; i++)
        {
            var far = ElementFactory.CreatePart(new Vector3Int(400, 700, 18), "Далеко" + i,
                new Vector3(3f + i * 0.5f, 0f, 0f));
            _spawned.Add(far);
        }
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);

        Assert.AreEqual(1, OpeningCollision.ObstaclesInLastScan,
            "двадцать одна деталь в сцене, а помешать может ровно одна — та, что на пути; "
            + "остальные двадцать стоят в трёх метрах и в развёртку дверцы не попадают");
        Assert.Less(f.DoorProgress, 1f,
            "и эта одна дверцу ОСТАНОВИЛА — отсев не смеет выбросить настоящее препятствие");
    }

    /// <summary>Обратный вход к отсеву, без которого он был бы «просто выключить проверку»:
    /// препятствие подводят к дверце вплотную — и оно обязано появиться в скане и остановить
    /// её там, где она и должна встать.</summary>
    [Test]
    public void ObstacleBroughtIntoReach_ShowsUpInTheScan_AndStopsTheDoor()
    {
        var f = MakeSlidingFacade();
        var wanderer = ElementFactory.CreatePart(new Vector3Int(400, 700, 18), "Пришелец",
            new Vector3(4f, 0f, 0f)).GetComponent<KitchenElement>()!;
        _spawned.Add(wanderer.gameObject);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);

        Assert.AreEqual(0, OpeningCollision.ObstaclesInLastScan,
            "пока деталь в четырёх метрах, скану не с чем работать");
        Assert.AreEqual(1f, f.DoorProgress, 1e-4f, "и дверца открылась полностью");

        f.ForceClose();
        wanderer.transform.position = new Vector3(0f, 0f, (9f + 100f + 9f)
            * AppConstants.MM_TO_UNITS);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);

        Assert.AreEqual(1, OpeningCollision.ObstaclesInLastScan,
            "деталь подвели вплотную — она обязана войти в скан");
        Assert.Less(f.DoorProgress, 1f,
            "и остановить дверцу: отсев по достижимости не смеет ослеплять её");
    }

    /// <summary>Дамп `perf_20260913_080751.csv`: пользователь вёл дверь к стене мышью —
    /// 96 кадров, ВСЕ дороже 50 мс, среднее 64,5. Из них 19,1 мс — `FacadeElement.StepDoor`,
    /// и внутри `ScanForBlock` 17,1 мс. Прибор назвал виновника без догадок:
    /// `FacadeElement.ApplyDoor` на этих кадрах — **ноль из 96**, а
    /// `ElementMover.ApplyDragFrame` — тоже ноль, зато `DoorElement.SnapToWall` — 96 из 96.
    /// То есть вели ЧУЖУЮ деталь, а сканировала упёршаяся дверца в стороне, и семнадцать
    /// миллисекунд скана не меняли её позу НИ РАЗУ.
    ///
    /// Ключ `OpeningScanKey` тут бессилен по устройству: в нём стоит ревизия сцены,
    /// а при ведении детали сцена меняется каждый кадр ПО-НАСТОЯЩЕМУ. Отвечает на это
    /// `OpeningScanMemory`: препятствия пересобираются (это дёшево, 1,6 мс — и это
    /// тот самый «индекс, построенный один раз»), но если набор коробок совпал с прошлым
    /// вплоть до бита, ответ берётся готовым, а 128 шагов скана не делаются вовсе.
    /// Совпал вход — совпадёт и ответ; ошибиться тут нечем.</summary>
    [Test]
    public void SomethingElseBeingLedAcrossTheScene_DoesNotRescanTheBlockedDoor()
    {
        var f = MakeSlidingFacade();
        var obstacle = MakeObstacleInFrontOfTheDoor(100f);
        var bystander = ElementFactory.CreatePart(new Vector3Int(400, 2000, 18), "Ведомая дверь",
            new Vector3(5f, 0f, 0f)).GetComponent<KitchenElement>()!;
        _spawned.Add(bystander.gameObject);
        SettleTheSceneAndZeroTheSensors();

        f.SetOpen(true);
        RunGesture(f, bumpEveryFrame: false);
        float stopped = f.DoorProgress;
        Assert.Less(stopped, 1f,
            "дверца обязана упереться — иначе сканировать было бы нечего и тест ни о чём");

        SettleTheSceneAndZeroTheSensors();
        OpeningCollision.TakeScanForBlockCalls();

        for (int i = 0; i < GestureFrames; i++)
        {
            bystander.transform.position += new Vector3(0.01f, 0f, 0f);
            SceneChangeTracker.Poll();
            f.StepDoor(Dt);
        }

        int scans = OpeningCollision.TakeScanForBlockCalls();
        int builds = OpeningCollision.TakeBuildObstacleCalls();
        Assert.AreEqual(0, scans,
            $"чужая деталь ездит в пяти метрах — препятствия у ЭТОЙ дверцы те же самые, "
            + $"и пересчитывать предел незачем. Получено {scans} сканов за {GestureFrames} "
            + "кадров: столько же раз прогнаны 128 шагов по всей сцене");
        Assert.Greater(builds, 0,
            "а вот препятствия пересобираться обязаны — иначе дверца не узнала бы о "
            + "настоящем изменении, и тест был бы зелёным по неверной причине");
        Assert.AreEqual(stopped, f.DoorProgress, 1e-6f,
            "и стоит дверца ровно там, где её остановило препятствие");

        obstacle.transform.position += new Vector3(0f, 0f, 0.05f);
        SceneChangeTracker.Poll();
        f.StepDoor(Dt);

        Assert.AreEqual(1, OpeningCollision.TakeScanForBlockCalls(),
            "ОБРАТНЫЙ ВХОД: сдвинулось НАСТОЯЩЕЕ препятствие — скан обязан произойти");

        RunGesture(f, bumpEveryFrame: false);
        Assert.Greater(f.DoorProgress, stopped,
            "препятствие отодвинули на 50 мм — дверца обязана поехать дальше, "
            + "а не остаться на старом пределе");
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
