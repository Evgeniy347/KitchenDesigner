using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Быстрый путь берёт только коробки, стоящие по мировым осям; всё
/// остальное обязано уходить в общий перебор 6×6 и получать тот же ответ.
///
/// Этот тест существует потому, что доказать откат на сцене пользователя
/// НЕВОЗМОЖНО: там все 411 деталей повёрнуты кратно 90°, и ветка отката не
/// исполняется ни разу (`AxisAlignedContactFastPathTests`). Тест, у которого нет
/// входа в проверяемую ветку, зелен на чём угодно — поэтому поворот на 45° здесь
/// написан руками.
///
/// Кватернион задан литералом, а не через <c>Quaternion.Euler</c>: тот ECall и под
/// dotnet падает SecurityException (`conventions/STRUCTURE.md` → «A class without a
/// scene lives on the fast path»).</summary>
public class AxisAlignedContactFallbackTests
{
    private const float MM = 0.001f;

    /// <summary>Поворот на 45° вокруг Y: ни одна локальная ось не легла на мировую.</summary>
    private static readonly Quaternion Yaw45 =
        new Quaternion(0f, 0.38268343f, 0f, 0.92387953f);

    private static readonly Quaternion Yaw90 =
        new Quaternion(0f, 0.70710678f, 0f, 0.70710678f);

    private static ElementGeometry Board(string name, float yCentreMm, Quaternion rotation) =>
        ElementGeometry.Box(name, new Vector3(0f, yCentreMm * MM, 0f),
            new Vector3(600f, 18f, 400f) * MM, rotation);

    private static ValidationElement Part(in ElementGeometry geometry, ElementKind kind) =>
        new ValidationElement(geometry, System.Array.Empty<Vector3>(), kind,
            ValidationElement.NoGroup, null,
            Span.FromCenter(0.5f * (geometry.Min.y + geometry.Max.y),
                geometry.Max.y - geometry.Min.y), ValidationElement.NoIndex);

    private static bool IndexHandles(in ElementGeometry geometry)
    {
        var index = new AxisAlignedBoxIndex();
        index.Reset(1);
        return index.Handles(0, geometry);
    }

    [Test]
    public void AxisAlignedBoxIndex_PartStandingOnTheWorldAxes_IsHandled()
    {
        Assert.IsTrue(IndexHandles(Board("straight", 0f, Quaternion.identity)),
            "Неповёрнутая коробка — это и есть случай, ради которого быстрый путь писался");
    }

    [Test]
    public void AxisAlignedBoxIndex_PartTurnedByAMultipleOf90_IsHandled()
    {
        Assert.IsTrue(IndexHandles(Board("quarter", 0f, Yaw90)),
            "Поворот на 90° оставляет оси на мировых — быстрый путь обязан его брать, "
            + "иначе «в кухне почти все осевые» превращается в «почти никто»");
    }

    [Test]
    public void AxisAlignedBoxIndex_PartTurnedBy45Degrees_IsRefused()
    {
        Assert.IsFalse(IndexHandles(Board("skew", 0f, Yaw45)),
            "Коробка, повёрнутая на 45°, интервальной арифметике не поддаётся: "
            + "её грани не параллельны мировым осям, и перекрытие AABB — не перекрытие граней");
    }

    /// <summary>Положительный контроль к отказу: мало отказать — общий перебор
    /// обязан найти на тех же деталях настоящий контакт. Без этой половины тест
    /// «повёрнутое не берётся» был бы зелен и на коде, который просто теряет такие
    /// пары.</summary>
    [Test]
    public void ValidationCore_TwoPartsTurnedBy45Degrees_StillFindTheirContact()
    {
        var lower = Board("skew_lower", 0f, Yaw45);
        var upper = Board("skew_upper", 18f, Yaw45);

        var result = ValidationCore.Validate(new[]
        {
            Part(lower, ElementKind.Anchor),
            Part(upper, ElementKind.None),
        });

        Assert.That(result.Contacts.Count(c => c.IsFaceToFace), Is.GreaterThan(0),
            "Пара, отвергнутая быстрым путём, обязана пройти общим перебором и получить "
            + "свой контакт — иначе отказ означает не откат, а потерю");
    }

    [Test]
    public void ValidationCore_AxisAlignedPairAndTurnedPair_AgreeOnTheContactCount()
    {
        var straight = ContactsBetweenStackedBoards(Quaternion.identity);
        var turned = ContactsBetweenStackedBoards(Yaw45);

        Assert.That(turned, Is.EqualTo(straight),
            "Два пути обязаны давать одинаковый ответ на одинаковой геометрии: "
            + $"по осям {straight} контактов, повёрнутая на 45° — {turned}");
    }

    private static int ContactsBetweenStackedBoards(Quaternion rotation)
    {
        var result = ValidationCore.Validate(new[]
        {
            Part(Board("lower", 0f, rotation), ElementKind.Anchor),
            Part(Board("upper", 18f, rotation), ElementKind.None),
        });
        return result.Contacts.Count;
    }

    [Test]
    public void AxisAlignedBoxIndex_GeometryThatIsNotSixFaces_IsRefused()
    {
        var notABox = new ElementGeometry(1, "sliver", System.Array.Empty<Face>(),
            System.Array.Empty<Face>(), System.Array.Empty<Face>(),
            Vector3.zero, Vector3.one, false);

        Assert.IsFalse(IndexHandles(notABox),
            "Геометрия не из шести граней — не коробка: интервальная арифметика "
            + "по её AABB отвечала бы про фигуру, которой нет");
    }

    [Test]
    public void AxisAlignedBoxIndex_ResetForgetsWhatItLearnedAboutAnIndex()
    {
        var index = new AxisAlignedBoxIndex();
        index.Reset(1);
        Assert.IsFalse(index.Handles(0, Board("skew", 0f, Yaw45)),
            "Подготовка ответа");

        index.Reset(1);
        Assert.IsTrue(index.Handles(0, Board("straight", 0f, Quaternion.identity)),
            "Ответ закэширован по НОМЕРУ детали, а номера переиспользуются от кадра "
            + "к кадру: не забудь Reset — и повёрнутая деталь навсегда сделает своего "
            + "преемника непригодным для быстрого пути");
    }

    [Test]
    public void AxisAlignedBoxIndex_KeepsTheAnswerPerIndex_WithinOneReset()
    {
        var index = new AxisAlignedBoxIndex();
        index.Reset(2);
        var straight = Board("straight", 0f, Quaternion.identity);
        var skew = Board("skew", 0f, Yaw45);

        Assert.IsTrue(index.Handles(0, straight), "Первая деталь — коробка");
        Assert.IsFalse(index.Handles(1, skew), "Вторая — нет");
        Assert.IsTrue(index.Handles(0, straight),
            "Повторный вопрос о той же детали обязан дать тот же ответ: кэш по номеру "
            + "не должен затираться соседом");

        var contacts = new List<CoreContact>();
        Assert.IsFalse(index.TryAppendContacts(0, 1, straight, skew,
                ValidationCore.ContactDistUnits, Tolerance.ContactUnits, contacts),
            "Пара, где хоть одна деталь не коробка, целиком уходит в общий перебор");
        Assert.That(contacts, Is.Empty,
            "Отказ обязан быть ПОЛНЫМ: дописав половину контактов и вернув false, "
            + "быстрый путь удвоил бы их — общий перебор добавит свои поверх");
    }
}
