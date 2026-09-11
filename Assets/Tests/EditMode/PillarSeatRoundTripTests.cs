using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Круг «открыл → сохранил → открыл» для опоры, на ЗАМОРОЖЕННОЙ выдержке из
/// проекта пользователя.
///
/// Дефект, ради которого набор написан: пользователь ставил опору на пол, сохранял,
/// открывал сохранённое — и опора снова висела в воздухе на 20 мм. Виноват был
/// <see cref="PillarAutoFit.FloorUnder"/>: он брал за пол ЛЮБУЮ поверхность, чей
/// габарит проходит в пределах <see cref="PillarAutoFit.OverlapMarginUnits"/> (50 мм)
/// от ЦЕНТРА опоры, а не под её пятой. Рядом с опорой лежит дно цокольного ящика
/// (верх на 20 мм), опора стоит от него в 16 мм по X и 11 мм по Z — не касаясь
/// вовсе, — и проход после загрузки честно «сажал» её на эту фантомную полку.
/// Ни команды, ни отмены при этом не появлялось (conventions/SERIALIZATION.md →
/// «проход после загрузки чинит СТЫК, а не РАЗМЕР»), а следующее сохранение уносило
/// подмену в файл: починка не переживала круг.
///
/// Почему набор стоит именно на данных пользователя: на синтетической сцене ловушка
/// не возникает сама — её надо знать заранее и построить. Фикстура
/// <c>Fixtures/pillar-beside-plinth.save.json</c> — выдержка из
/// <c>docs/example.save.json</c> (пол Pol_1, дно цокольного ящика
/// A2_plint_drawer_bottom, дно короба сверху A12_bottom и сама опора Leg_9),
/// снятая один раз и замороженная. Живой файл тесты не читают: он переписывается
/// автосохранением десктопа (agents/TESTS.md → «docs/example.save.json — NEVER TOUCH IT»).
/// Опора в фикстуре стоит там, куда её поставил пользователь своей последней
/// записью в хронике — низом ровно на полу.</summary>
public class PillarSeatRoundTripTests
{
    private const string FixtureName = "Fixtures/pillar-beside-plinth.save.json";
    private const string LegName = "Leg_9";
    private const string PhantomShelfName = "A2_plint_drawer_bottom";

    private const float FloorTopUnits = 0f;
    private const float ToleranceUnits = 1e-4f;

    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", FixtureName);
        Assert.IsTrue(File.Exists(fullPath), $"фикстура не найдена: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);

        _guard = ProjectLoadStateGuard.Capture();
        var settings = KitchenSettings.Instance;
        Assert.IsNotNull(settings);
        settings!.AutoSave = false;
    }

    [TearDown]
    public void TearDown() => ClearScene();

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        ClearScene();
        _guard?.Restore();
        _guard = null;
    }

    [Test]
    public void LoadSaveLoad_LeavesTheLegOnTheFloor_NotOnTheDrawerBottomBesideIt()
    {
        RestoreFixture();
        AssertTheTrapIsStillArmed();

        Assert.AreEqual(FloorTopUnits, BottomOf(Leg()), ToleranceUnits,
            "первое открытие: опора обязана остаться на полу, куда её поставил пользователь");

        SaveAndReopen();

        Assert.AreEqual(FloorTopUnits, BottomOf(Leg()), ToleranceUnits,
            "круг «сохранил → открыл»: опора уехала от пола — ровно то, что видит "
            + "пользователь, когда его починка не переживает сериализацию");
    }

    [Test]
    public void SavedFile_CarriesThePositionTheUserLeft_NotTheOneTheLoaderInvented()
    {
        float inTheFile = LegDataOf(FixtureData()).Position.y;

        RestoreFixture();
        var reopened = SaveAndReopen();

        Assert.AreEqual(inTheFile, LegDataOf(reopened).Position.y, ToleranceUnits,
            "в файл обязано уехать то же число, что стояло в сцене: молчаливая подмена "
            + "на загрузке закрепляется первым же сохранением и обратно не отменяется");
    }

    /// <summary>Ловушка фикстуры названа вслух, иначе набор однажды позеленеет на
    /// сцене, где проверять уже нечего: дно ящика обязано лежать РЯДОМ (в пределах
    /// прежнего полуметрового допуска от центра опоры), но НЕ под её пятой.</summary>
    private static void AssertTheTrapIsStillArmed()
    {
        var leg = Leg();
        var shelf = ByName(PhantomShelfName);
        var legBox = ElementAabb.Of(leg);
        var shelfBox = ElementAabb.Of(shelf);

        Assert.Greater(shelfBox.maxY, FloorTopUnits + ToleranceUnits,
            "фантомная полка обязана быть ВЫШЕ пола, иначе садиться на неё не больно");
        Assert.IsTrue(shelfBox.CoversInXZ(leg.transform.position, PillarAutoFit.OverlapMarginUnits),
            "полка обязана попадать в прежний допуск от центра опоры — иначе прежний код "
            + "на этой сцене и не ошибся бы, и тест не смог бы покраснеть");
        Assert.IsFalse(PillarAutoFit.CarriesTheFootprint(legBox, shelfBox),
            "и при этом обязана НЕ быть под пятой опоры — в этом и состоит дефект");
    }

    private void RestoreFixture()
    {
        var objects = SaveLoadManager.RestoreScene(FixtureData());
        Assert.IsNotEmpty(objects);
    }

    private ProjectData FixtureData()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        return data!;
    }

    private static ElementData LegDataOf(ProjectData data)
    {
        var found = data.elements.FirstOrDefault(e => e != null && e.name == LegName);
        Assert.IsNotNull(found, $"в данных проекта нет опоры {LegName}");
        return found!;
    }

    private ProjectData SaveAndReopen()
    {
        var captured = SaveLoadManager.CaptureScene(PartRegistry.GetAll());
        var json = SaveLoadManager.Serialize(captured);

        ClearScene();

        var reopened = SaveLoadManager.Deserialize(json);
        Assert.IsNotNull(reopened);
        var objects = SaveLoadManager.RestoreScene(reopened!);
        Assert.IsNotEmpty(objects);
        return reopened!;
    }

    private static PillarElement Leg()
    {
        var found = ByName(LegName) as PillarElement;
        Assert.IsNotNull(found, $"{LegName} обязана восстановиться именно опорой");
        return found!;
    }

    private static KitchenElement ByName(string partName)
    {
        var all = PartRegistry.GetAll();
        var found = all.FirstOrDefault(e => e != null && e.PartName == partName);
        Assert.IsNotNull(found, $"в восстановленной сцене нет детали {partName}");
        return found!;
    }

    private static float BottomOf(KitchenElement element)
    {
        float min = float.MaxValue;
        foreach (var v in element.GetVertices()) if (v.y < min) min = v.y;
        return min;
    }

    private static void ClearScene()
    {
        foreach (var e in UnityEngine.Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }
}
