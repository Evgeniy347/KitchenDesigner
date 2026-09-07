using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Пул отдаёт объект, который уже жил чужой жизнью.
///
/// Дефект, с которого написан файл: пользователь задал доске зазоры, удалил её,
/// создал новую — и новая доска пришла с чужими зазорами. Молча: в мешe их нет,
/// в спецификации и в валидации есть.
///
/// Чинить перечнем полей нельзя — ровно об этом conventions/CORRECTNESS.md →
/// «State a rule by its MECHANISM, not as a list of the cases you happened to fix».
/// Прежний ResetComponent чистил имя, габарит, пазы и кромку: список, в котором забыли
/// пятое поле. Дописать «и зазоры» — поставить ту же ловушку шестому.
///
/// Механизм вместо списка: всё пользовательское состояние KitchenElement и так лежит в
/// ОДНОМ объекте — PartData. Сброс не перечисляет поля, а заменяет объект целиком тем же
/// выражением, которым он создаётся у нового элемента: `_data = new PartData()`. Новое
/// поле PartData получает свой умолчательный вид из того же инициализатора, что и сброс,
/// поэтому под сброс попадает само, без единой правки здесь.
///
/// Позу пул терял половинами. Позицию пулуемому объекту переставляла фабрика при выдаче,
/// поворот не переставлял никто — и AttachRestRotation с ClosedRotation, оба производные
/// от transform.rotation, приезжали к следующему фасаду от предыдущего. Поэтому позу
/// ставит ОДИН вызов SetPositionAndRotation: половины у него не бывает, а фабрика больше
/// не участвует в сбросе случайно.
///
/// У MonoBehaviour-подкласса своего PartData нет и `new` ему запрещён, поэтому остаток —
/// собственные поля FacadeElement и три поля базы вне PartData — закрывает хук
/// OnResetToPristineState. Хук — это опять список, и держит его честным сторож ниже:
/// он сравнивает переиспользованный элемент со свежим ПО ВСЕМ публичным свойствам, а
/// разбиение свойств на «пишется», «пачкается вручную» и «производное» обязано быть
/// исчерпывающим. Новое свойство не попадает ни в одну корзину — тест краснеет и говорит,
/// что делать.</summary>
public class ElementPoolResetTests
{
    private const string ProbeName = "Проба";
    private static readonly Vector3Int ProbeDims = new Vector3Int(800, 400, 18);
    private static readonly Vector3 ProbePos = new Vector3(1.5f, 0.5f, 2.5f);

    /// <summary>Правило целиком, первым абзацем каждого красного сообщения: его читает не
    /// человек, а агент, пришедший делать что-то другое (conventions/TEST-NAMING.md →
    /// «Читатель красного стража — агент, посланный за другим»).</summary>
    private const string Rule =
        "ПРАВИЛО ПРОЕКТА: объект, вернувшийся в пул, обязан отдаваться следующему элементу "
        + "неотличимым от только что созданного. Уцелевшее поле — это тихая порча данных: "
        + "деталь получает чужие зазоры, чужой декор, чужой режим петли, которых пользователь "
        + "не задавал, и они уходят в раскрой и в валидацию. Чинить здесь и сейчас, разрешения "
        + "не спрашивать. Адрес правки: KitchenElement.ResetToPristineState "
        + "(Assets/Scripts/Core/Elements/KitchenElement.cs) — состояние в PartData сбрасывается "
        + "заменой объекта и правки не требует; собственные поля подкласса — в его override "
        + "OnResetToPristineState. Если свойство состояния не несёт, впиши его в Derived с "
        + "причиной; если несёт, но пишется только методом — в Dirtiers. ";

    /// <summary>Что кладут в пул и что он отдаёт следующим. Стена — не опечатка: у неё
    /// ровно typeof(KitchenElement), значит её Disposal тоже PartPool, и её состояние
    /// достаётся следующей ДОСКЕ.</summary>
    private sealed class PoolCase
    {
        public string Name = "";
        public Func<GameObject> CreateConsumer = null!;
        public Action<GameObject> ReleaseConsumer = null!;
        public (string Name, Func<GameObject> Create, Action<GameObject> Release)[] Producers =
            Array.Empty<(string, Func<GameObject>, Action<GameObject>)>();
    }

    private static PoolCase[] Pools() => new[]
    {
        new PoolCase
        {
            Name = "пул деталей",
            CreateConsumer = () => ElementFactory.CreatePart(ProbeDims, ProbeName, ProbePos),
            ReleaseConsumer = ElementFactory.DestroyPart,
            Producers = new (string, Func<GameObject>, Action<GameObject>)[]
            {
                ("доска", () => ElementFactory.CreatePart(ProbeDims, ProbeName, ProbePos),
                    ElementFactory.DestroyPart),
                ("стена", () => ElementFactory.CreateWall(ProbeDims, "Стена", ProbePos),
                    ElementFactory.DestroyElement),
            },
        },
        new PoolCase
        {
            Name = "пул фасадов",
            CreateConsumer = () => ElementFactory.CreateFacade(ProbeDims, ProbeName, ProbePos),
            ReleaseConsumer = ElementFactory.DestroyFacade,
            Producers = new (string, Func<GameObject>, Action<GameObject>)[]
            {
                ("фасад", () => ElementFactory.CreateFacade(ProbeDims, ProbeName, ProbePos),
                    ElementFactory.DestroyFacade),
            },
        },
    };

    /// <summary>Свойства, которые состояния не хранят: ответ считается из типа, габарита,
    /// сцены или других свойств. Они всё равно СРАВНИВАЮТСЯ — просто пачкать их незачем.</summary>
    private static readonly Dictionary<string, string> Derived = new Dictionary<string, string>
    {
        ["Data"] = "сам контейнер состояния; каждое его поле проверяется через своё свойство-обёртку",
        ["Gaps"] = "производное от Gap* — шести отдельных свойств",
        ["GapMM"] = "сумма Gap*",
        ["PoseFollowsTransform"] = "считается из Movable и режима прицепа",
        ["Transformable"] = "считается из Movable и PoseFollowsTransform",
        ["DecorRenderer"] = "поиск рендерера в дереве, не поле",
        ["DecorSurfaceMM"] = "производное от DimensionsMM",
        ["AttachIsDerived"] = "константа типа",
        ["ParticipatesInGapChecks"] = "константа типа",
        ["AttachRestPosition"] = "производное от transform и режима прицепа",
        ["AttachRestRotation"] = "производное от transform и режима прицепа",
        ["CanFollowAnAttachParent"] = "константа типа",
        ["CanCarryAttachedParts"] = "константа типа",
        ["SupportsGaps"] = "константа типа",
        ["SupportsGrooves"] = "считается из типа и навешанных компонентов",
        ["SupportsTextureOverlays"] = "считается из типа и навешанных компонентов",
        ["SupportsEdges"] = "считается из типа и габарита",
        ["DisplayTypeName"] = "константа типа",
        ["CutoutRole"] = "считается из навешанных компонентов",
        ["BlocksCutout"] = "производное от CutoutRole",
        ["AlignsCutout"] = "производное от CutoutRole",
        ["Disposal"] = "константа типа",
        ["InspectedElement"] = "ссылка на себя или на хозяина, не поле",
        ["CutoutHoleAxis"] = "производное от AttachedCutouts",
        ["BareFaceMask"] = "производная от соседей по сцене; пересчитывается EdgeSubstrate.Sync",
        ["IsClosedPose"] = "производное от IsOpen и DoorProgress",
        ["IsDoorClosed"] = "производное от IsOpen и DoorProgress",
        ["OpenActionLabel"] = "подпись пункта меню, считается из IsOpen и хозяина фасада",
        ["ClosedPosition"] = "производное от transform и IsDoorClosed",
        ["ClosedRotation"] = "производное от transform и IsDoorClosed",
    };

    /// <summary>Свойства, которые состояние хранят, но пишутся не сеттером, а методом.
    /// Каждое обязано быть здесь или в Derived — иначе тест разбиения краснеет.</summary>
    private static readonly Dictionary<string, Action<KitchenElement>> Dirtiers =
        new Dictionary<string, Action<KitchenElement>>
        {
            ["Grooves"] = el => el.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Top)),
            ["TextureOverlays"] = el => el.SetTextureOverlays(
                new[] { TextureOverlaySpec.FullFace(OverlaySide.A, MaterialCatalog.DefaultId) }),
            ["AttachedCutouts"] = el => el.RegisterCutout(new StubCutout()),
            ["IsAttachRidden"] = el => el.BeginAttachRide(new Vector3(9f, 9f, 9f),
                ManagedRotation.RotY(37f)),
            ["PoseVersion"] = el => el.BumpPoseVersion(),
            ["IsOpen"] = DirtyDoor,
            ["DoorProgress"] = DirtyDoor,
        };

    /// <summary>Открыть дверцу так, чтобы изменились ОБА поля — и цель открывания, и
    /// доля пройденного пути. `SetOpen` ставит только цель, прогресс двигает `StepDoor`,
    /// а он у пассажира не работает вовсе, поэтому режим пассажира на время снимается.</summary>
    private static void DirtyDoor(KitchenElement el)
    {
        if (el is not FacadeElement facade) return;
        bool passenger = facade.IsPassenger;
        facade.IsPassenger = false;
        facade.SetOpen(true);
        facade.StepDoor(0.05f);
        facade.IsPassenger = passenger;
    }

    private sealed class StubCutout : IPartCutout
    {
        public string PartName => "вырез-заглушка";

        public GrooveMesh.Rect2 CutoutRectIn(KitchenElement part) =>
            new GrooveMesh.Rect2 { xMin = -0.2f, xMax = 0.2f, yMin = -0.2f, yMax = 0.2f };

        public int HoleAxisIn(KitchenElement part) => 2;
    }

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var el in PartRegistry.GetAll())
            if (el != null) ElementFactory.DestroyElement(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    // ---------------------------------------------------------------- репродукция

    /// <summary>Тот самый дефект, числом. На старом коде красный шестью значениями:
    /// 17/13/11/7/5/3 вместо нулей.</summary>
    [Test]
    public void Pool_BoardReleasedWithGaps_NextBoardComesBackWithZeroGaps()
    {
        var first = ElementFactory.CreatePart(ProbeDims, "С зазорами", ProbePos);
        var el = first.GetComponent<KitchenElement>();
        el.GapLeft = 17;
        el.GapRight = 13;
        el.GapTop = 11;
        el.GapBottom = 7;
        el.GapFront = 5;
        el.GapBack = 3;
        ElementFactory.DestroyPart(first);

        var second = ElementFactory.CreatePart(ProbeDims, "Новая доска", ProbePos);
        var fresh = second.GetComponent<KitchenElement>();

        Assert.AreEqual("0/0/0/0/0/0", SixGaps(fresh),
            "Зазоры предыдущей жизни пережили возврат в пул (лево/право/верх/низ/перед/зад). "
            + "Пользователь их не задавал: в спецификации и в валидации такая доска шире "
            + "собственного меша. " + Rule);
        ElementFactory.DestroyPart(second);
    }

    /// <summary>Вторая половина того же дефекта: декор. Доска возвращается в пул с чужим
    /// materialId, а рендерер при выдаче красится серым по умолчанию — поле и картинка
    /// расходятся молча.</summary>
    [Test]
    public void Pool_BoardReleasedWithADecor_NextBoardComesBackWithTheDefaultOne()
    {
        var first = ElementFactory.CreatePart(ProbeDims, "С декором", ProbePos);
        first.GetComponent<KitchenElement>().MaterialId = "чужой_декор";
        ElementFactory.DestroyPart(first);

        var second = ElementFactory.CreatePart(ProbeDims, "Новая доска", ProbePos);
        Assert.AreEqual(MaterialCatalog.DefaultId,
            second.GetComponent<KitchenElement>().MaterialId,
            "Декор предыдущей жизни пережил возврат в пул. " + Rule);
        ElementFactory.DestroyPart(second);
    }

    /// <summary>Тот же дефект, но в железе сцены, а не в полях. GameObject стены собран
    /// не из примитива: у него MeshCollider по мешу стены и BoxCollider отсутствует вовсе.
    /// Тип стены — ровно typeof(KitchenElement), поэтому её объект уходит в пул ДЕТАЛЕЙ, а
    /// выдача из пула умеет только ВКЛЮЧИТЬ BoxCollider — включать нечего. Следующая доска
    /// приходит с коллайдером формы стены: клик, снэп и подсветка меряют чужую геометрию,
    /// хотя меш уже перестроен и выглядит доской.</summary>
    [Test]
    public void Pool_WallReleased_NextBoardComesBackWithItsOwnBoxCollider()
    {
        var wall = ElementFactory.CreateWall(ProbeDims, "Стена", ProbePos);
        ElementFactory.DestroyElement(wall);

        var board = ElementFactory.CreatePart(ProbeDims, "Новая доска", ProbePos);

        Assert.IsTrue(board.GetComponent<MeshCollider>() == null,
            "Доска из пула несёт MeshCollider стены — коллайдер прежней жизни. " + Rule);

        var box = board.GetComponent<BoxCollider>();
        Assert.IsTrue(box != null && box.enabled,
            "У доски из пула нет включённого BoxCollider: чужой коллайдер сняли, а свой "
            + "не вернули, и деталь стала неосязаемой. " + Rule);

        ElementFactory.DestroyPart(board);
    }

    // ---------------------------------------------------------------- сторож

    /// <summary>Разбиение — это и есть механизм. Каждое публичное свойство обязано попасть
    /// ровно в одну корзину: пишется сеттером (пачкается само), пачкается методом из
    /// Dirtiers, или объявлено производным в Derived с причиной. Новое свойство не попадает
    /// никуда и краснит этот тест раньше, чем успеет уцелеть в пуле.</summary>
    [Test]
    public void EveryPublicProperty_OfAPooledType_IsWritable_OrDirtiable_OrDeclaredDerived()
    {
        var orphans = new List<string>();
        foreach (var type in PooledTypes())
            foreach (var p in StateProperties(type))
            {
                if (p.CanWrite && p.SetMethod != null && p.SetMethod.IsPublic) continue;
                if (Dirtiers.ContainsKey(p.Name)) continue;
                if (Derived.ContainsKey(p.Name)) continue;
                orphans.Add($"{type.Name}.{p.Name} ({p.PropertyType.Name})");
            }

        Assert.IsEmpty(orphans, Rule
            + "Эти свойства не отнесены ни к одной корзине, значит сторож ниже про них "
            + "ничего не знает и уцелеть в пуле они могут беззвучно: "
            + string.Join(", ", orphans));

        var strangers = Derived.Keys.Concat(Dirtiers.Keys)
            .Where(n => !PooledTypes().SelectMany(StateProperties).Any(p => p.Name == n))
            .ToList();
        Assert.IsEmpty(strangers, Rule
            + "В списках Derived/Dirtiers остались имена, которых у пулуемых типов больше "
            + "нет — свойство переименовали или удалили, а запись забыли: "
            + string.Join(", ", strangers));
    }

    /// <summary>Главный сторож. Свежий элемент — эталон; второй элемент пачкается по всем
    /// свойствам сразу и уходит в пул; третий берётся из пула теми же аргументами и обязан
    /// совпасть с эталоном ПОЛЕ В ПОЛЕ. Ни одного имени поля в теле теста нет — список
    /// свойств берётся рефлексией, поэтому следующее попадает под проверку само.</summary>
    [Test]
    public void Pool_RecycledElement_IsIndistinguishableFromAFreshOne()
    {
        var complaints = new List<string>();

        foreach (var pool in Pools())
        {
            var reference = pool.CreateConsumer();
            var pristine = Snapshot(reference.GetComponent<KitchenElement>());
            pool.ReleaseConsumer(reference);

            foreach (var producer in pool.Producers)
            {
                var dirty = producer.Create();
                DirtyEverything(dirty.GetComponent<KitchenElement>());
                producer.Release(dirty);

                var reused = pool.CreateConsumer();
                var after = Snapshot(reused.GetComponent<KitchenElement>());
                foreach (var key in pristine.Keys)
                    if (after.TryGetValue(key, out var got) && got != pristine[key])
                        complaints.Add($"{pool.Name}: после «{producer.Name}» "
                            + $"{key} = {got}, у свежего {pristine[key]}");
                pool.ReleaseConsumer(reused);
            }
        }

        Assert.IsEmpty(complaints, Rule + "Пережило возврат в пул: "
            + string.Join("; ", complaints));
    }

    /// <summary>Знаменатель сторожа: он меряет ровно столько, сколько успел испачкать.
    /// Если генератор значения промахнулся мимо умолчания, сторож выше зелен и не проверяет
    /// ничего — conventions/TEST-NAMING.md → «Prove the harness before you trust what it
    /// measures». Здесь доказывается, что каждое свойство хотя бы на одной поверхности
    /// реально стало другим.</summary>
    [Test]
    public void DirtyEverything_ActuallyChangesEveryPropertyItClaimsTo()
    {
        var changedSomewhere = new HashSet<string>();
        var expected = new HashSet<string>();

        foreach (var pool in Pools())
            foreach (var producer in pool.Producers)
            {
                var go = producer.Create();
                var el = go.GetComponent<KitchenElement>();
                foreach (var p in StateProperties(el.GetType()))
                {
                    if (Derived.ContainsKey(p.Name)) continue;
                    expected.Add(p.Name);
                }

                var before = Snapshot(el);
                DirtyEverything(el);
                var after = Snapshot(el);
                foreach (var key in before.Keys)
                    if (after.TryGetValue(key, out var got) && got != before[key])
                        changedSomewhere.Add(key);
                producer.Release(go);
            }

        var untouched = expected.Except(changedSomewhere).OrderBy(n => n).ToList();
        Assert.IsEmpty(untouched, Rule
            + "Эти свойства сторож пула не смог испачкать ни на одной поверхности, значит "
            + "про них он ничего не доказывает. Научи генератор их типу в DirtyValue, "
            + "поправь запись в Dirtiers или, если состояния там нет, перенеси имя в "
            + "Derived с причиной: " + string.Join(", ", untouched));
    }

    // ---------------------------------------------------------------- механика

    private static string SixGaps(KitchenElement el) =>
        $"{el.GapLeft}/{el.GapRight}/{el.GapTop}/{el.GapBottom}/{el.GapFront}/{el.GapBack}";

    private static IEnumerable<Type> PooledTypes()
    {
        yield return typeof(KitchenElement);
        yield return typeof(FacadeElement);
    }

    private static List<PropertyInfo> StateProperties(Type type)
    {
        var seen = new HashSet<string>();
        var result = new List<PropertyInfo>();
        for (Type? cur = type; cur != null && typeof(KitchenElement).IsAssignableFrom(cur);
             cur = cur.BaseType)
            foreach (var p in cur.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                if (seen.Add(p.Name)) result.Add(p);
            }
        result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return result;
    }

    private static void DirtyEverything(KitchenElement el)
    {
        var props = StateProperties(el.GetType());
        var untaught = new List<string>();

        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i];
            if (Derived.ContainsKey(p.Name)) continue;
            if (!p.CanWrite || p.SetMethod == null || !p.SetMethod.IsPublic) continue;

            var value = DirtyValue(p.PropertyType, p.GetValue(el), i);
            if (value == null) { untaught.Add($"{p.Name} ({p.PropertyType.Name})"); continue; }
            p.SetValue(el, value);
        }

        Assert.IsEmpty(untaught, Rule
            + "Генератор DirtyValue не умеет делать «другое» значение для этих типов, "
            + "поэтому свойства остались чистыми и сторож про них молчит. Добавь ветку типа "
            + "в DirtyValue: " + string.Join(", ", untaught));

        foreach (var p in props)
            if (Dirtiers.TryGetValue(p.Name, out var dirty)) dirty(el);
    }

    private static object? DirtyValue(Type t, object? current, int salt)
    {
        if (t == typeof(bool)) return !(bool)(current ?? false);
        if (t == typeof(int)) return (int)(current ?? 0) + 1 + (salt % 5);
        if (t == typeof(float)) return (float)(current ?? 0f) + 1.5f;
        if (t == typeof(string)) return ((string?)current ?? "") + "!грязь";
        if (t == typeof(Vector3Int)) return (Vector3Int)current! + new Vector3Int(7, 7, 7);
        if (t.IsEnum)
        {
            var values = Enum.GetValues(t);
            for (int k = 0; k < values.Length; k++)
                if (!Equals(values.GetValue(k), current)) return values.GetValue(k);
            return null;
        }
        return null;
    }

    private static Dictionary<string, string> Snapshot(KitchenElement el)
    {
        var map = new Dictionary<string, string>();
        foreach (var p in StateProperties(el.GetType()))
        {
            object? value;
            try { value = p.GetValue(el); }
            catch (TargetInvocationException e) { value = "<исключение: " + e.InnerException?.Message + ">"; }
            map[p.Name] = Stringify(value);
        }
        return map;
    }

    private static string Stringify(object? value)
    {
        if (value is null) return "null";
        if (value is UnityEngine.Object obj) return obj == null ? "объект:нет" : "объект:есть";
        if (value is float f) return f.ToString("F4", CultureInfo.InvariantCulture);
        if (value is string s) return "«" + s + "»";
        if (value is IEnumerable list)
        {
            int count = 0;
            var parts = new List<string>();
            foreach (var item in list) { count++; parts.Add(Stringify(item)); }
            return "[" + count + ": " + string.Join(",", parts) + "]";
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }
}
