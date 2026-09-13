using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Близнец контракта коробки валидации — и он закрывает дыру, которую
/// сам контракт закрыть не мог.
///
/// <c>ValidationGeometryContract</c> выглядит как выведенное правило, но оно
/// выведено только НАПОЛОВИНУ: рефлексия честно доказывает, что перечисленные
/// члены объявлены лишь в доказанных типах, — а сам ПЕРЕЧЕНЬ этих членов
/// написан руками. Следовательно новый <c>virtual</c> в <c>KitchenElement</c>,
/// который прочтёт <c>GetFacesAt</c>, войдёт в кэш молча у 360 деталей живого
/// проекта: контракт про него не знает, признак его не сторожит, и валидация
/// начнёт судить по вчерашней коробке. Ни один тест производительности этого
/// не видит — он покажет, что работы стало МЕНЬШЕ.
///
/// Правило здесь поэтому такое: каждый <c>virtual</c>/<c>abstract</c> член
/// <c>KitchenElement</c> либо входит в контракт, либо назван ниже с причиной,
/// по которой он на коробку валидации не влияет. Третьего варианта нет — новый
/// член роняет тест и НАЗЫВАЕТ своё имя.
///
/// Список причин написан руками намеренно. Руками пишется ЗНАНИЕ («материал
/// коробку не двигает»), а не перечень: перечень выводится рефлексией и
/// сверяется с этим знанием на каждом прогоне.</summary>
public class ValidationBoxContractTests
{
    /// <summary>Члены базового типа, которые коробку валидации не строят, с
    /// причиной на каждый. Причина — не украшение: через полгода она
    /// единственное, что отличает осознанное исключение от недосмотра.</summary>
    private static readonly (string member, string why)[] OutsideTheBox =
    {
        ("MaterialId", "материал детали; коробку не двигает"),
        ("DecorRenderer", "рендерер, на котором лежит декор"),
        ("DecorSurfaceMM", "размер площадки под текстуру для UV, а не габарит детали"),
        ("AttachIsDerived", "кто ведёт привязку, хозяин или пассажир"),
        ("AttachContactHolds", "ЧИТАЕТ геометрию, чтобы решить, держится ли контакт с "
            + "хозяином, но не строит её"),
        ("ParticipatesInGapChecks", "фильтр пар в поиске зазоров (FindNearContacts); "
            + "в ValidationCore.Validate, чей ответ и кэшируется, не входит вовсе"),
        ("CanFollowAnAttachParent", "правило привязки: вправе ли деталь ехать на хозяине"),
        ("CanCarryAttachedParts", "правило привязки: вправе ли деталь возить пассажиров"),
        ("SupportsGaps", "разрешает ли тип зазоры вообще; сами зазоры лежат в Gaps и в признаке"),
        ("DisplayTypeName", "подпись типа в интерфейсе"),
        ("Front", "какая грань считается лицевой; выбор грани, а не её положение"),
        ("IsFlatBoardElement", "классификация для кромки и спецификации"),
        ("CutoutRole", "роль детали в вырезах"),
        ("Disposal", "как деталь уничтожается"),
        ("PrepareForDestruction", "жизненный цикл"),
        ("OnResetToPristineState", "жизненный цикл"),
        ("OnOwnPoseVersionBumped", "уведомление о смене версии позы; сама поза в признаке"),
        ("OnElementDestroyed", "жизненный цикл"),
        ("InspectedElement", "кого показывать в инспекторе"),
        ("RefreshSubmeshMaterials", "материалы подмешей"),
        ("ApplyDimensions", "перестраивает МЕШ по габаритам; коробка валидации считается "
            + "от DimensionsMM напрямую и меша не читает"),
    };

    /// <summary>Перечень выводится из СБОРКИ, и делает это сам контракт
    /// (<c>ValidationGeometryContract.VirtualMembersOf</c>), а не тест. Так
    /// правильнее по существу, а не только по букве сторожа за рефлексией в
    /// тестах: обе половины правила — «из чего строится коробка» и «что вообще
    /// можно переопределить» — обязаны считаться в ОДНОМ месте, иначе они
    /// разъедутся ровно так же, как разъезжается любая вторая копия.</summary>
    private static List<string> EveryVirtualMemberOfTheBaseElement() =>
        new List<string>(ValidationGeometryContract.VirtualMembersOf(typeof(KitchenElement)));

    /// <summary>Сама рефлексия обязана что-то находить: скан по типу, который
    /// перестал резолвиться, или по неверным флагам проходит бодро и не
    /// проверяет ничего. Поэтому сначала — что сканер видит ИЗВЕСТНЫЕ члены с
    /// обеих сторон правила.</summary>
    [Test]
    public void TheScanOfTheBaseElement_SeesMembersOnBothSidesOfTheRule()
    {
        var found = EveryVirtualMemberOfTheBaseElement();

        Assert.Greater(found.Count, 20,
            $"сканер нашёл всего {found.Count} виртуальных членов KitchenElement — столько "
            + "их не бывает, значит флаги или тип не те, и весь тест ниже зелен на пустом "
            + "месте");
        CollectionAssert.Contains(found, "GetFacesAt",
            "сканер обязан видеть член ИЗ контракта — иначе он не проверяет ту половину");
        CollectionAssert.Contains(found, "EffectiveScale",
            "сканер обязан видеть и защищённое свойство, а не только публичные");
        CollectionAssert.Contains(found, "MaterialId",
            "сканер обязан видеть член ВНЕ контракта — иначе он не проверяет вторую половину");
    }

    /// <summary>Положительный контроль к списку причин: каждое записанное имя
    /// обязано ещё существовать. Иначе строка переживает свой член и начинает
    /// молча прощать следующего, кто займёт это имя, — та же болезнь, что у
    /// списка разрешённых файлов у соседних сторожей.</summary>
    [Test]
    public void EveryNameRecordedAsOutsideTheBox_IsStillAMemberOfTheBaseElement()
    {
        var found = EveryVirtualMemberOfTheBaseElement();

        foreach (var (member, why) in OutsideTheBox)
            CollectionAssert.Contains(found, member,
                $"{member} записан как «на коробку не влияет» ({why}), но такого "
                + "виртуального члена в KitchenElement больше нет: строка пережила свой "
                + "член и теперь прощает того, кто займёт это имя");
    }

    /// <summary>То же самое для второй половины: каждое имя, которое контракт
    /// проверяет рефлексией, обязано существовать. Опечатка в этом перечне —
    /// самая тихая из возможных поломок: контракт спокойно «докажет», что
    /// несуществующий член объявлен только в доказанных типах.</summary>
    [Test]
    public void EveryNameTheContractChecks_IsStillAMemberOfTheBaseElement()
    {
        var found = EveryVirtualMemberOfTheBaseElement();

        foreach (var member in ValidationGeometryContract.MembersTheBoxIsBuiltFrom)
            CollectionAssert.Contains(found, member,
                $"контракт проверяет член {member}, которого в KitchenElement нет. "
                + "Проверка несуществующего имени всегда «проходит» — и коробка перестаёт "
                + "быть закрытой ровно по этому члену");
    }

    /// <summary>Главное требование. Разбиение обязано быть ПОЛНЫМ: третьего
    /// варианта — члена, о котором не сказано ничего, — быть не может.
    ///
    /// Падает этот тест ровно в том случае, ради которого написан: кто-то
    /// добавил в <c>KitchenElement</c> новый <c>virtual</c>. Дальше выбор из
    /// двух, и оба требуют думать: либо член строит коробку и его имя идёт в
    /// контракт, либо не строит и его имя идёт в список выше — с причиной.</summary>
    [Test]
    public void EveryVirtualMemberOfTheBaseElement_IsEitherInTheContract_OrRecordedAsOutsideTheBox()
    {
        var inTheContract = new List<string>(ValidationGeometryContract.MembersTheBoxIsBuiltFrom);
        var recorded = new List<string>();
        foreach (var (member, _) in OutsideTheBox) recorded.Add(member);

        var unaccounted = new List<string>();
        foreach (var member in EveryVirtualMemberOfTheBaseElement())
            if (!inTheContract.Contains(member) && !recorded.Contains(member))
                unaccounted.Add(member);

        CollectionAssert.IsEmpty(unaccounted,
            "в KitchenElement появились виртуальные члены, про которые не сказано ничего: "
            + string.Join(", ", unaccounted)
            + ". Каждый обязан быть либо в ValidationGeometryContract (если из него строится "
            + "коробка валидации — тогда переопределивший его тип выпадет из кэша), либо в "
            + "OutsideTheBox с причиной. Промолчать нельзя: член, читающий GetFacesAt и не "
            + "попавший в контракт, войдёт в кэш молча у 360 деталей живого проекта, и "
            + "валидация начнёт судить по вчерашней коробке");
    }

    /// <summary>Ни одно имя не вправе стоять в обоих списках сразу: тогда
    /// «доказан» и «не влияет» начинают значить одно и то же, и правило
    /// перестаёт быть разбиением.</summary>
    [Test]
    public void NoMember_StandsInBothTheContractAndTheRecordOfWhatIsOutsideTheBox()
    {
        var inTheContract = new List<string>(ValidationGeometryContract.MembersTheBoxIsBuiltFrom);

        foreach (var (member, why) in OutsideTheBox)
            CollectionAssert.DoesNotContain(inTheContract, member,
                $"{member} записан как «на коробку не влияет» ({why}) и одновременно "
                + "проверяется контрактом как строящий её. Одно из двух утверждений ложно");
    }
}
