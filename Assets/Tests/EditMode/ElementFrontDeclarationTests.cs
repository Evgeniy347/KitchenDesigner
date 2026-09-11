using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сторож правила «изометрия снимает объект С ЛИЦА».
///
/// Дефект был таким. Лицо элемента в проекте смотрит в +Z, а сырой
/// изометрический вектор <c>IsoCameraRig.IsoDir</c> стоит на -Z — то есть в
/// затылок КАЖДОМУ типу без исключения. Миниатюра каталога это уже победила:
/// она выводит сторону съёмки из объявленной фронтальной оси
/// (<c>ElementFacing.CameraDirection</c>) и получается правильной сразу.
/// Изометрия же лечилась поимённо — константа разворота на 180° была
/// выписана у дивана, кровати, настенной арматуры и стиральных машин. Кому не
/// выписали, тот снимался коробкой со спины: <c>iso_dishwasher</c>, и по тому
/// же признаку духовка и варочная. Их эталоны были ПРИНЯТЫ и не стерегли
/// ничего — голая коробка совпадает с голой коробкой при любой поломке лица.
///
/// Теперь механизм один и общий: сторона съёмки выводится один раз
/// (<c>IsoCameraRig.ViewDir</c>), оба съёмщика её читают, разворотов нет ни у
/// одного типа. Этот набор держит то, что механизмом не выражается:
///
///   1. новый тип обязан ОБЪЯВИТЬ своё лицо сам — иначе его снимут затылком,
///      и заметит это глаз, а не прогон;
///   2. объявленная лицевая деталь обязана быть ВИДНА со стороны съёмщика —
///      это и есть отличие кадра с лицом от кадра с пустой гранью, измеренное
///      без глаза;
///   3. поимённые развороты не имеют права вернуться — их ищет скан.</summary>
public class ElementFrontDeclarationTests
{
    /// <summary>Сколько типов объявили, что отдельной лицевой детали у них
    /// нет. Потолок, а не факт: он обязан только падать. Новый тип, тихо
    /// присоединившийся к этой группе, — ровно тот случай, ради которого
    /// сторож написан, и он краснеет здесь ещё до того, как кадр снимут.</summary>
    private const int FacelessCeiling = 33;

    private static IReadOnlyList<ElementSurfaceSweep.Row> Rows => ElementSurfaceSweep.Rows;

    /// <summary>Наследование ответа запрещено намеренно. Объяви базовый класс
    /// «лицевой детали нет» — и каждый новый тип получил бы этот ответ молча,
    /// то есть сторож перестал бы краснеть ровно на том случае, ради которого
    /// заведён.</summary>
    [Test]
    public void EveryConcreteElementType_DeclaresItsOwnFront_NotTheOneItInherited()
    {
        var silent = new List<string>();
        foreach (var type in EveryElementType.Declared())
        {
            var property = type.GetProperty(nameof(KitchenElement.Front));
            Assert.IsNotNull(property,
                type.Name + ": свойства Front больше нет — правило про лицо элемента "
                + "исчезло вместе с ним, и этот скан проходит ни на чём");
            var getter = property!.GetGetMethod();
            Assert.IsNotNull(getter, type.Name + ": у Front нет геттера");
            if (getter!.DeclaringType != type) silent.Add(type.Name);
        }

        Assert.IsEmpty(silent,
            "тип обязан объявить своё лицо САМ — одной строкой рядом с собой:\n"
            + "  ElementFront.Parts(имена лицевых деталей) — если лицо собрано из "
            + "именованных детей (люк, стекло, панель, ручка);\n"
            + "  ElementFront.NoSeparateFacePart(почему) — если отдельной лицевой "
            + "детали у типа нет (тело вращения, плита, верхняя поверхность).\n"
            + "Без объявления тип молча унаследовал чужой ответ, и его кадр перестал "
            + "стеречь что бы то ни было. Молчат: " + string.Join(", ", silent));
    }

    [Test]
    public void EveryTypeWithoutANamedFront_SaysWhyItHasNone()
    {
        var mute = Rows.Where(r => !r.DeclaresNamedFront && r.FrontReason.Length == 0)
            .Select(r => r.Name).ToList();

        Assert.IsEmpty(mute,
            "«лицевой детали нет» без причины неотличимо от «не разбирался»: причина "
            + "это то единственное, что через полгода отделит осознанное объявление от "
            + "отписки. Молчат: " + string.Join(", ", mute));
    }

    /// <summary>Главный вопрос набора, и он задаётся по РЕАЛЬНОЙ сцене: деталь,
    /// объявленную лицевой, не закрывает собственный корпус элемента, если
    /// смотреть на него оттуда, где стоит съёмщик. До этой правки посудомойка,
    /// духовка и машины отвечали бы здесь красным.</summary>
    [Test]
    public void EveryDeclaredFrontPart_IsVisibleFromWhereTheRigStands()
    {
        var blind = Rows.Where(r => r.FrontHidden.Count > 0)
            .Select(r => r.Name + ": " + string.Join(", ", r.FrontHidden)).ToList();

        Assert.IsEmpty(blind,
            "объявленные лицевыми детали не видно со стороны съёмщика — значит кадр "
            + "снимается с затылка и показывает голую коробку, которая совпадёт сама с "
            + "собой при любой поломке лица. Либо съёмщик вернулся на сырой IsoDir, "
            + "либо деталь переехала за корпус, либо объявление называет не тот "
            + "ребёнок:\n  " + string.Join("\n  ", blind));
    }

    /// <summary>Положительный контроль к проверке выше: она обязана иметь дело
    /// хоть с чем-то. Список объявивших лицо пуст — и «ни одна лицевая деталь
    /// не спрятана» становится правдой ни о чём.</summary>
    [Test]
    public void SomeTypesDoDeclareNamedFrontParts_OrTheVisibilityCheckGuardsNothing()
    {
        var withParts = Rows.Where(r => r.DeclaresNamedFront).Select(r => r.Name).ToList();

        Assert.IsNotEmpty(withParts,
            "ни один тип не объявил лицо именованными деталями: проверка видимости "
            + "тогда зелёная на пустоте");
        CollectionAssert.Contains(withParts, nameof(DishwasherElement),
            "посудомойка — тот самый тип, чей эталон месяцами был голой коробкой; "
            + "именно её лицо обязано быть объявлено и проверено");
    }

    [Test]
    public void TheFacelessGroup_OnlyEverShrinks()
    {
        var faceless = Rows.Where(r => !r.DeclaresNamedFront).Select(r => r.Name).ToList();

        Assert.LessOrEqual(faceless.Count, FacelessCeiling,
            "новый тип объявил, что лицевой детали у него нет. Иногда это правда "
            + "(труба, плита, столешница), и тогда потолок поднимается ОТДЕЛЬНЫМ "
            + "изменением с причиной. Но чаще это способ не разбираться — а кадр "
            + "такого типа не стережёт ничего. Сейчас: " + faceless.Count + " из "
            + Rows.Count + " — " + string.Join(", ", faceless));
    }

    // ─ Скан: поимённый разворот не имеет права вернуться ─────

    private const string TurnConstantName = "FrontTowardsCameraDeg";

    private const string RawIsoVector = "0.5f, 0.5f, -0.866f";

    private const string RigFile = "IsoCameraRig.cs";

    /// <summary>Файл самого сторожа из выборки исключён, и это не поблажка: обе
    /// искомые строки он держит КОНСТАНТАМИ, иначе искать было бы нечем. Всё
    /// остальное, включая соседние наборы кадров, проверяется.</summary>
    private const string GuardFile = "ElementFrontDeclarationTests.cs";

    private const int MinScannedSources = 200;

    private static List<string> SourcesUnder(string relativeDir) =>
        Directory.GetFiles(Path.Combine(Application.dataPath, relativeDir), "*.cs",
            SearchOption.AllDirectories).ToList();

    private static List<string> ScannedSources() =>
        SourcesUnder("Scripts").Concat(SourcesUnder("Tests"))
            .Where(p => Path.GetFileName(p) != GuardFile).ToList();

    [Test]
    public void TheScanReadsRealSources_SoAnEmptyResultCannotPassForGreen()
    {
        var sources = ScannedSources();

        Assert.Greater(sources.Count, MinScannedSources,
            "скан не нашёл исходников: оба скана ниже проходят ни на чём. Найдено: "
            + sources.Count);
        Assert.IsTrue(sources.Any(p => Path.GetFileName(p) == RigFile),
            RigFile + " не попал в выборку — значит скан смотрит не туда, и «сырой "
            + "вектор нигде не скопирован» становится утверждением о пустоте");
        Assert.IsTrue(sources.Any(p => Path.GetFileName(p) == RigFile
                && File.ReadAllText(p).Contains(RawIsoVector)),
            "сам сырой вектор " + RawIsoVector + " не найден НИГДЕ, даже в "
            + RigFile + ": либо он переписан другой записью, либо скан ищет по строке, "
            + "которой больше нет — и тогда он не поймает копию");
    }

    [Test]
    public void Sources_KeepNoPerTypeTurn_TowardsTheCamera()
    {
        var offenders = ScannedSources()
            .Where(p => File.ReadAllText(p).Contains(TurnConstantName))
            .Select(p => Path.GetFileName(p)!).ToList();

        Assert.IsEmpty(offenders,
            "разворот элемента лицом к камере вернулся списком случаев. Так это уже "
            + "было: константу выписали дивану, кровати, арматуре и машинам, а "
            + "посудомойке, духовке и варочной не выписал никто — и три эталона "
            + "месяцами были снимками затылка. Сторона съёмки выводится один раз "
            + "(IsoCameraRig.ViewDir) и читается обоими съёмщиками; разворачивать "
            + "снимаемый элемент не нужно ни одному типу. Держат: "
            + string.Join(", ", offenders));
    }

    [Test]
    public void NoSourceCopiesTheRawIsoVector_TheyAllAskTheRig()
    {
        var offenders = ScannedSources()
            .Where(p => Path.GetFileName(p) != RigFile)
            .Where(p => File.ReadAllText(p).Contains(RawIsoVector))
            .Select(p => Path.GetFileName(p)!).ToList();

        Assert.IsEmpty(offenders,
            "копия сырого изометрического вектора — это вторая сторона съёмки, и она "
            + "разъедется с общей молча: копия не знает про объявленное лицо и ставит "
            + "камеру в затылок. Спрашивайте IsoCameraRig.ViewDir (или Position). "
            + "Копируют: " + string.Join(", ", offenders));
    }
}
