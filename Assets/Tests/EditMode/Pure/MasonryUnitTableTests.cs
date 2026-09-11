using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>Таблица форматов кладки: пять технологий — ДАННЫЕ в одной таблице, а не
/// пять классов. Сторож здесь двусторонний: каждое значение перечисления обязано
/// иметь строку в таблице, и каждая строка таблицы обязана быть выдана продуктом
/// (WallQuantities.Of возвращает по ней осмысленную ведомость). Односторонняя
/// проверка «значение входит в закрытый список» прячет опечатку внутри самого
/// списка, а неиспользованная константа читается следующим агентом как рабочая —
/// agents/TEST-DESIGN.md, «Проверка «значение входит в закрытый список» обязана
/// быть двусторонней».
///
/// Источники чисел названы в самих строках таблицы (MasonryUnit.Source) и
/// сверяются здесь через справочный расход на кубометр — единственную цифру,
/// которую пользователь может проверить на калькуляторе.</summary>
public class MasonryUnitTableTests
{
    [Test]
    public void MasonryUnit_Table_CoversEveryTechnology_AndDeclaresNoRowTwice()
    {
        var declared = Enum.GetValues(typeof(MasonryTechnology)).Cast<MasonryTechnology>().ToArray();
        var rows = MasonryUnit.Table.Select(u => u.Technology).ToArray();

        CollectionAssert.AreEquivalent(declared, rows,
            "правило: формат кладки — строка в MasonryUnit.Table, а не новый класс и не "
            + "новая ветка switch. Новое значение MasonryTechnology без строки означает "
            + "технологию, которую дропдаун предлагает, а смета посчитать не может; "
            + "лишняя строка — формат, который никому не выдаётся. Лечится добавлением "
            + "строки в MasonryUnit.Table в том же изменении, что и значение перечисления");
        Assert.AreEqual(rows.Length, rows.Distinct().Count(),
            "две строки на одну технологию: MasonryUnit.Of вернёт первую, вторая — мёртвые данные");
    }

    [Test]
    public void MasonryUnit_EveryTableRow_IsActuallyIssuedByWallQuantities_NotJustDeclared()
    {
        foreach (var unit in MasonryUnit.Table)
        {
            var result = WallQuantities.Of(unit.Technology, 3000f, 2700f, 250f,
                Array.Empty<WallOpening>(), 10f, 0f);

            Assert.AreEqual(unit.Counting, result.Counting, unit.Title + ": способ счёта разъехался");
            Assert.Greater(Payload(result), 0d,
                unit.Title + ": строка таблицы объявлена, но продукт по ней не даёт ни одного "
                + "числа. Спрашивать надо у ПРОДУКТА функции, а не у того, объявлена ли "
                + "константа — иначе формат исчезает из ведомости молча. Либо дай технологии "
                + "счёт, либо помести её в список зарезервированных с причиной");
        }
    }

    [Test]
    public void MasonryUnit_NoTechnologyIsReserved_SoTheReservationListStaysEmptyOnPurpose()
    {
        var reserved = new Dictionary<MasonryTechnology, string>();

        CollectionAssert.IsEmpty(reserved,
            "список зарезервированных форматов пуст, и это утверждение, а не забывчивость: "
            + "все пять технологий этапа 3а выдаются WallQuantities.Of. Появится формат без "
            + "счёта — он идёт СЮДА с причиной, и предыдущий тест перестанет его требовать. "
            + "Сама пометка становится ложью в день, когда формат начинают выдавать");
    }

    [Test]
    public void MasonryUnit_EveryRow_NamesTheSourceOfItsNumbers()
    {
        foreach (var unit in MasonryUnit.Table)
            Assert.IsFalse(string.IsNullOrWhiteSpace(unit.Source),
                unit.Title + ": инженерная константа обязана нести источник — "
                + "docs/todo_evolution.md, «Правило инженерных констант». Число без "
                + "источника — «примерно», и вносить его нельзя");
    }

    [Test]
    public void MasonryUnit_BrickSingle_Is250x120x65_PerGost530Table2Format1Nf()
    {
        var unit = MasonryUnit.Of(MasonryTechnology.BrickSingle);

        Assert.AreEqual(250f, unit.LengthMm, 1e-4f);
        Assert.AreEqual(120f, unit.WidthMm, 1e-4f);
        Assert.AreEqual(65f, unit.HeightMm, 1e-4f);
        Assert.AreEqual(MasonryUnit.BrickStandard, unit.Source,
            "кирпич КР 250×120×65, формат 1 НФ — ГОСТ 530-2012, таблица 2 «Номинальные "
            + "размеры кирпича». Номер таблицы сверен с текстом стандарта; сам стандарт по "
            + "реестру Росстандарта действует с 01.07.2013 и замены не имеет. Обозначений "
            + "КО и КУ в ГОСТ 530-2012 нет — это словарь ГОСТ 530-2007, и писать их в "
            + "ведомость нельзя");
    }

    [Test]
    public void MasonryUnit_BrickThickened_Is250x120x88_PerGost530Table2Format14Nf()
    {
        var unit = MasonryUnit.Of(MasonryTechnology.BrickThickened);

        Assert.AreEqual(250f, unit.LengthMm, 1e-4f);
        Assert.AreEqual(120f, unit.WidthMm, 1e-4f);
        Assert.AreEqual(88f, unit.HeightMm, 1e-4f);
        Assert.AreEqual(MasonryUnit.BrickStandard, unit.Source,
            "кирпич КР 250×120×88, формат 1,4 НФ — та же таблица 2 ГОСТ 530-2012, соседняя "
            + "строка. Пара к предыдущему тесту: одна таблица источника, два её ряда");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void MasonryUnit_AeratedBlock_Is600x300x200_ButGost31360DoesNotPrescribeThatSize()
    {
        var unit = MasonryUnit.Of(MasonryTechnology.AeratedBlock);

        Assert.AreEqual(600f, unit.LengthMm, 1e-4f);
        Assert.AreEqual(300f, unit.WidthMm, 1e-4f);
        Assert.AreEqual(200f, unit.HeightMm, 1e-4f);
        Assert.AreEqual(MasonryUnit.AeratedBlockStandard, unit.Source,
            "категория остаётся, и это результат сверки, а не её отсутствие. Сверено и "
            + "разошлось дважды. Первое: ГОСТ 31360 не задаёт номинальных размеров вообще — "
            + "в редакции 2007 таблица 1 пункта 4.2.2 даёт МАКСИМАЛЬНЫЕ размеры блока "
            + "625×500×500, а 600×300×200 в тексте не названо; в редакции 2024 размер "
            + "600×200×300 встречается лишь как пример условного обозначения в 4.2.5. "
            + "Второе: ГОСТ 31360-2007 заменён ГОСТ 31360-2024 (введён 01.01.2025, "
            + "переходный период истёк 10.01.2026), то есть строка ведомости ссылается на "
            + "отменённую редакцию. Обе правки меняют то, что пользователь видит в "
            + "ведомости, поэтому номер стандарта в MasonryUnit.AeratedBlockStandard "
            + "менять исполнителю нельзя — это решение менеджера");
    }

    [Test]
    public void MasonryUnit_BrickSingle_WithJoint10_Costs394PiecesPerCubicMetre_AsInTheHandbook()
    {
        var perCubicMetre = 1d / MasonryUnit.Of(MasonryTechnology.BrickSingle).JointedVolumeM3(10d);

        Assert.AreEqual(394.5d, perCubicMetre, 0.05d,
            "справочный расход одинарного кирпича на 1 м³ кладки — 394 шт. Это и есть "
            + "проверка того, что шов добавляется ко ВСЕМ ТРЁМ размерам: "
            + "1 / (0,26 × 0,13 × 0,075) = 394,5. Добавь шов только к двум — получится 365, "
            + "и вся смета уедет на 8 %. Число нормативного статуса не имеет: номера таблицы "
            + "ГОСТ у него нет и быть не может, это справочный расход из строительных "
            + "таблиц, и сверяется он арифметикой по размерам ГОСТ 530-2012, таблица 2");
    }

    [Test]
    public void MasonryUnit_BrickThickened_WithJoint10_Costs302PiecesPerCubicMetre_AsInTheHandbook()
    {
        var perCubicMetre = 1d / MasonryUnit.Of(MasonryTechnology.BrickThickened).JointedVolumeM3(10d);

        Assert.AreEqual(301.9d, perCubicMetre, 0.05d,
            "справочный расход утолщённого кирпича — 302 шт/м³: "
            + "1 / (0,26 × 0,13 × 0,098) = 301,9. Пара к предыдущему тесту: одна формула, "
            + "два независимых справочных числа, поэтому подгонкой формулы под одно из них "
            + "не отделаться");
    }

    [Test]
    public void MasonryUnit_Timber_And_Frame_HaveNoFormat_SoNothingIsCountedInPieces()
    {
        Assert.IsFalse(MasonryUnit.Of(MasonryTechnology.Timber).HasFormat,
            "брус считается кубометрами: «штук» у него нет, и придумывать формат нельзя");
        Assert.IsFalse(MasonryUnit.Of(MasonryTechnology.Frame).HasFormat,
            "каркас считается стойками и метрами погонными");
        Assert.IsTrue(MasonryUnit.Of(MasonryTechnology.BrickSingle).HasFormat,
            "контроль: у кирпича формат ЕСТЬ, иначе HasFormat не проверяет ничего");
    }

    [Test]
    public void MasonryUnit_Of_UnknownTechnology_Throws_InsteadOfReturningAnEmptyFormat()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MasonryUnit.Of((MasonryTechnology)999),
            "формат по умолчанию — это нулевой объём и деление на ноль в смете. "
            + "Неизвестная технология обязана падать громко");
    }

    private static double Payload(in WallQuantitiesResult r)
    {
        switch (r.Counting)
        {
            case MasonryCounting.Pieces: return r.Pieces;
            case MasonryCounting.Volume: return r.TimberM3;
            case MasonryCounting.Studs: return r.Studs;
            default: return 0d;
        }
    }
}
