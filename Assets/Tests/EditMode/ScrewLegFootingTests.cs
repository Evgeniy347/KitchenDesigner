using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>LEG-03: винтовая опора обязана на что-то опираться. Опирание — это
/// касание НИЖНЕЙ грани пятака с ВЕРХНЕЙ гранью чего-нибудь (пола, плиты
/// основания, любой детали) в пределах <see cref="Tolerance.ContactMm"/>; хозяин,
/// в который ввинчена резьба, опорой не считается — он ВЫШЕ пятака.
///
/// Уровень — предупреждение, а не ошибка: опора, висящая в воздухе, геометрически
/// представима, это недоделанный проект. Вошедшая же в деталь на 3 мм резьба
/// (LEG-02) — отказ крепления, поэтому там ошибка.
///
/// Правило родилось на живом проекте: у Vintovaya_opora_2 пятак стоял на 25 мм
/// выше плиты основания и не касался ничего — замечаний не было ни одного,
/// потому что правила не существовало.</summary>
public class ScrewLegFootingTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    /// <summary>Пол 3×3 м, верхняя грань ровно на нуле.</summary>
    private void Floor() =>
        _spawned.Add(ElementFactory.CreateFloor(new Vector3Int(3000, 100, 3000), "pol",
            new Vector3(0f, -50f * U, 0f)));

    private KitchenElement Board(string name, Vector3Int dims, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        el.transform.position = pos;
        PartRegistry.Register(el);
        _spawned.Add(go);
        return el;
    }

    /// <summary>Заводская опора ростом 58 мм: центр на 29 мм над низом пятака.</summary>
    private ScrewLegElement LegWithPadBottomAt(float padBottomMm)
    {
        var go = ElementFactory.CreateScrewLeg("opora",
            new Vector3(0f, (padBottomMm + 29f) * U, 0f));
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        Assert.AreEqual(58, leg.BodyHeightMM,
            "предусловие: опора заводская, 8 мм пятака плюс 50 мм резьбы — иначе "
            + "числа в тестах ниже перестают означать то, что написано");
        return leg;
    }

    private static AnalysisIssue? Footing(ScrewLegElement leg)
    {
        foreach (var issue in SceneAnalyzer.Analyze())
            if (issue.Code == IssueCatalog.CodeScrewLegNoFooting && issue.Target == leg)
                return issue;
        return null;
    }

    [Test]
    public void ALegStandingOnTheFloor_IsNotReported()
    {
        Floor();
        var leg = LegWithPadBottomAt(0f);

        Assert.IsNull(Footing(leg),
            "низ пятака ровно на верхней грани пола — правило обязано молчать именно "
            + "на правильной установке, иначе оно бесполезно");
    }

    [Test]
    public void ALegRaisedAboveTheFloor_IsReportedWithTheGapToIt()
    {
        Floor();
        var leg = LegWithPadBottomAt(25f);

        var issue = Footing(leg);
        Assert.IsNotNull(issue, "пятак на 25 мм выше пола — опоре не на чем стоять");
        Assert.AreEqual(IssueLevel.Warning, issue!.Value.Level,
            "висящая опора — недоделанный проект, а не отказ: ошибка тут запретила бы "
            + "пользователю двигать сборку по частям");
        Assert.AreEqual("opora ↔ pol", issue.Value.Detail,
            "в колонке «Деталь» обе стороны: и опора, и то, до чего она не достаёт");
        StringAssert.Contains($"до pol {25f:F1} мм", issue.Value.Message,
            "зазор назван числом — иначе по строке не понять, опустить опору "
            + "или поднять то, что под ней");
    }

    /// <summary>Опорой считается не только пол: опора, стоящая на детали, —
    /// нормальная сборка. Без этого теста правило можно было бы написать «касается
    /// пола», и оно кричало бы на каждую опору, стоящую на цоколе.</summary>
    [Test]
    public void ALegStandingOnAPlainPart_IsNotReported()
    {
        Board("podstavka", new Vector3Int(200, 20, 200), new Vector3(0f, 10f * U, 0f));
        var leg = LegWithPadBottomAt(20f);

        Assert.IsNull(Footing(leg),
            "деталь 0..20 мм под пятаком — такая же опора, как пол");
    }

    /// <summary>Тот же пятак на той же высоте, но деталь отодвинута по X на
    /// полметра: контроль к тесту выше — правило смотрит НА ЧТО опора встала,
    /// а не «есть ли в сцене хоть одна деталь на нужной высоте».</summary>
    [Test]
    public void APartBesideTheLeg_DoesNotCountAsItsFooting()
    {
        Board("podstavka", new Vector3Int(200, 20, 200), new Vector3(500f * U, 10f * U, 0f));
        var leg = LegWithPadBottomAt(20f);

        Assert.IsNotNull(Footing(leg),
            "деталь стоит в полуметре сбоку — под пятаком по-прежнему пусто");
    }

    [Test]
    public void ALegWithNothingUnderneath_SaysSoInsteadOfNamingAGap()
    {
        var leg = LegWithPadBottomAt(0f);

        var issue = Footing(leg);
        Assert.IsNotNull(issue, "в сцене нет ни пола, ни детали — опора висит в пустоте");
        Assert.AreEqual("opora", issue!.Value.Detail,
            "второй стороны нет — колонка не превращается в «opora ↔ —»");
        StringAssert.Contains("ни пола, ни детали", issue.Value.Message,
            "зазор «до ближайшей опоры» тут не существует, и печатать вместо него "
            + "ноль значило бы соврать: ноль читается как «касается»");
    }
}
