using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Дефект D6. «Пассажиры» жеста — элементы, пристёгнутые к дверце, — двигаются вместе
/// с ней каждый кадр, поэтому их собственное движение тоже надо гасить через
/// `SceneChangeTracker.NoteSelfAnimated`. Список пассажиров присваивался внутри `SafeProgress`,
/// а тот зовётся только при ОТКРЫВАНИИ: на всём закрытии работал список, собранный в прошлом
/// жесте (или пустой). Пассажир, прицепленный к уже открытой дверце, в него не попадал — его
/// трансформ считался настоящим изменением сцены, и вся картина «216 мс на кадр» возвращалась
/// на закрывающей половине жеста.
///
/// Сенсор — `SceneRevision.TakeBumps()`: считаем ВЫЗОВЫ (сколько раз сцена сочла себя
/// изменившейся), а не миллисекунды.</summary>
public class FacadeGestureRidersTests : ElementTestBase
{
    private const float Dt = 1f / 60f;
    private const int GestureFrames = 40;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        SceneRevision.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        SceneRevision.Reset();
    }

    /// <summary>Кадр приложения целиком: `Update` двигает дверцу, `LateUpdate` катает
    /// пассажиров и опрашивает сцену.</summary>
    private void RunGesture(FacadeElement f, int frames = GestureFrames)
    {
        for (int i = 0; i < frames; i++)
        {
            f.StepDoor(Dt);
            AttachRider.Step();
            SceneChangeTracker.Poll();
        }
    }

    private void SettleTheSceneAndZeroTheSensor()
    {
        AttachRider.Step();
        SceneChangeTracker.Poll();
        SceneChangeTracker.Poll();
        SceneRevision.TakeBumps();
    }

    private FacadeElement MakeSlidingFacade()
    {
        var f = MakePrimitiveFacade("Дверца", new Vector3Int(400, 700, 18), Vector3.zero);
        f.Mode = DoorMode.DrawerOut;
        return f;
    }

    private KitchenElement MakeRiderOn(FacadeElement f)
    {
        var rider = MakePrimitiveElement("Пассажир", new Vector3Int(100, 100, 100),
            new Vector3(0f, 0.5f, 0f));
        rider.AttachedToName = f.PartName;
        return rider;
    }

    [Test]
    public void RiderAttachedToAnOpenDoor_IsMaskedOnTheClosingHalfToo()
    {
        var f = MakeSlidingFacade();
        f.SetOpen(true);
        RunGesture(f);
        Assert.AreEqual(1f, f.DoorProgress, 1e-4f, "предусловие: дверца открылась полностью");

        MakeRiderOn(f);
        SettleTheSceneAndZeroTheSensor();

        f.SetOpen(false);
        RunGesture(f);

        int bumps = SceneRevision.TakeBumps();
        Assert.AreEqual(0f, f.DoorProgress, 1e-4f, "предусловие: дверца закрылась");
        Assert.LessOrEqual(bumps, 2,
            $"пассажира прицепили к уже открытой дверце — на закрытии он обязан быть в списке "
            + $"глушения. Ревизия сдвинулась {bumps} раз за {GestureFrames} кадров: столько же "
            + "раз отработали `SettleDerivedLinks`, обе `ApplyAll` и `SceneAnalyzer.Analyze`");
    }

    /// <summary>Положительный контроль к тесту выше: без глушения пассажир действительно
    /// поднимает ревизию на каждом кадре. Тест, который не может покраснеть, ничего не стоит —
    /// здесь красный воспроизводится элементом, который к дверце НЕ пристёгнут, а просто
    /// двигается сам.</summary>
    [Test]
    public void Sensor_GoesRed_WhenAnUnmaskedNeighbourMovesEveryFrame()
    {
        var f = MakeSlidingFacade();
        var loose = MakePrimitiveElement("Сам по себе", new Vector3Int(100, 100, 100),
            new Vector3(1f, 0.5f, 0f));
        SettleTheSceneAndZeroTheSensor();

        f.SetOpen(true);
        for (int i = 0; i < GestureFrames; i++)
        {
            f.StepDoor(Dt);
            loose.transform.position += new Vector3(0f, 0.001f, 0f);
            SceneChangeTracker.Poll();
        }

        Assert.Greater(SceneRevision.TakeBumps(), 10,
            "чужое движение обязано поднимать ревизию каждый кадр — иначе сенсор считает не то, "
            + "и зелёный в тесте выше ничего не доказывает");
    }

    /// <summary>Обратный случай: отцепленный пассажир не имеет права оставаться
    /// замаскированным. Список, собранный один раз и живущий вечно, именно это и делал бы —
    /// настоящее движение элемента терялось бы, пока дверца катается.</summary>
    [Test]
    public void FormerRider_StopsBeingMasked_AfterItIsDetached()
    {
        var f = MakeSlidingFacade();
        var rider = MakeRiderOn(f);
        f.SetOpen(true);
        RunGesture(f);

        rider.AttachedToName = "";
        rider.transform.position += new Vector3(1f, 0f, 0f);
        SettleTheSceneAndZeroTheSensor();
        int poseBefore = rider.PoseVersion;

        f.SetOpen(false);
        const int FramesInsideTheGesture = 15;   // 0,25 с при длительности жеста 0,4 с
        for (int i = 0; i < FramesInsideTheGesture; i++)
        {
            f.StepDoor(Dt);
            rider.transform.position += new Vector3(0f, 0.001f, 0f);
            SceneChangeTracker.Poll();
        }

        Assert.Greater(f.DoorProgress, 0f,
            "предусловие: замеряем ВНУТРИ жеста — после его конца глушение снимается само "
            + "и тест перестал бы что-либо проверять");
        Assert.Greater(rider.PoseVersion, poseBefore,
            "элемент отцепили — дверца больше не двигает его трансформ, и его собственное "
            + "движение обязано снова считаться изменением сцены");
    }
}
