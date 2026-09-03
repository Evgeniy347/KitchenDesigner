using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Пуфик — САМОСТОЯТЕЛЬНЫЙ тип, а не табуретка с большим радиусом.
/// Разница конструктивная: у табуретки жёсткое сиденье 30 мм на четырёх
/// отдельных ножках 40x40, у пуфика ножек нет вовсе — снизу глухая обитая
/// тумба до пола, сверху мягкая сидушка 50 мм. Разный список деталей, разные
/// меши (профильное выдавливание + подушка против выдавливания + примитивов) и
/// разные слоты декора («Обивка»/«Сиденье» против «Сиденье»/«Ножки»).
///
/// Наследовать его от табуретки было бы нельзя даже при полном совпадении
/// формы: каждый реестр проекта ветвится через <c>is XxxElement</c>, а
/// <c>ElementDuplicators</c> спрашивает <c>is StoolElement</c> первым —
/// подкласс молча дублировался бы табуреткой и терял сидушку.
///
/// Второе, что здесь удерживается, — ФИЗИЧЕСКОЕ пространство. Корневой
/// <c>localScale</c> обязан оставаться единичным, а габарит уезжает в привязку
/// через <c>EffectiveScale</c>: контур строится из окружностей в миллиметрах, и
/// неравномерное масштабирование корня превратило бы их в эллипсы. Этот
/// проект наступал на такое трижды.</summary>
public class PouffeElementTests
{
    private const float Tol = 1e-4f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => LogAssert.ignoreFailingMessages = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private PouffeElement Pouffe(int widthMM, int heightMM, int depthMM,
        int cornerRadiusMM = PouffeElement.DefaultCornerRadiusMM,
        int seatThicknessMM = PouffeElement.DefaultSeatThicknessMM)
    {
        var go = new GameObject("Пуфик-1");
        _spawned.Add(go);

        var pouffe = go.AddComponent<PouffeElement>();
        pouffe.PartName = go.name;
        pouffe.DimensionsMM = new Vector3Int(widthMM, heightMM, depthMM);
        pouffe.SeatThicknessMM = seatThicknessMM;
        pouffe.CornerRadiusMM = cornerRadiusMM;
        return pouffe;
    }

    private PouffeElement DefaultPouffe() => Pouffe(PouffeElement.DefaultWidthMM,
        PouffeElement.DefaultHeightMM, PouffeElement.DefaultDepthMM);

    private static Transform Seat(PouffeElement pouffe)
    {
        var seat = pouffe.transform.Find(PouffeElement.SeatChildName);
        Assert.IsNotNull(seat, "сидушка — ИМЕНОВАННЫЙ ребёнок: поиск ребёнка по индексу "
            + "однажды уже уронил тайлинг декора на ножку стола");
        return seat!;
    }

    private static Bounds BoundsOf(Vector3[] points)
    {
        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (var p in points) bounds.Encapsulate(p);
        return bounds;
    }

    [Test]
    public void Pouffe_IsNotAStoolNorASofa_SoTheRegistriesSeeItAsItsOwnType()
    {
        Assert.IsFalse(typeof(StoolElement).IsAssignableFrom(typeof(PouffeElement)),
            "ElementDuplicators спрашивает is StoolElement раньше: подкласс дублировался "
            + "бы табуреткой и молча терял сидушку и обитую тумбу");
        Assert.IsFalse(typeof(SofaElement).IsAssignableFrom(typeof(PouffeElement)),
            "и не диван: у дивана своя раскладка из спинки и четырёх подушек");
        Assert.IsTrue(typeof(KitchenElement).IsAssignableFrom(typeof(PouffeElement)),
            "и при этом пуфик обязан оставаться элементом: реестры выводят список типов "
            + "из цепочки наследования до KitchenElement");
    }

    [Test]
    public void Pouffe_HasNoLegs_JustTheUpholsteredBoxAndTheSeatOnTop()
    {
        var pouffe = DefaultPouffe();

        Assert.AreEqual(1, pouffe.transform.childCount,
            "у пуфика РОВНО один ребёнок — сидушка. Ножек нет: их роль играет нижний "
            + "объём, стоящий на полу. Появление четырёх кубов означало бы, что пуфик "
            + "переписали в табуретку");
        Assert.AreEqual(PouffeElement.SeatChildName, pouffe.transform.GetChild(0).name,
            "и этот единственный ребёнок — именно сидушка");
    }

    [Test]
    public void Pouffe_AfterRebuild_KeepsUnitLocalScale_SoItsCornersStayCircular()
    {
        var pouffe = Pouffe(600, 400, 350, 175);

        Assert.AreEqual(Vector3.one, pouffe.transform.localScale,
            "контур тумбы строится из окружностей в физических миллиметрах. Растянуть "
            + "корень значило бы превратить их в эллипсы, а заданный радиус — в число "
            + "без смысла; проект наступал на это трижды");
        Assert.AreEqual(Vector3.one, Seat(pouffe).localScale,
            "и сидушку тоже: SoftSlabMesh строится сразу в физическом размере, ему "
            + "масштаб не нужен. Её контур — те же окружности, что у тумбы, только "
            + "сжатые внутрь на отступ, и растянутый ребёнок превратил бы их в эллипсы "
            + "ровно так же");
    }

    [Test]
    public void Pouffe_ReportsItsPhysicalSizeToSnapping_DespiteTheUnitScale()
    {
        var pouffe = Pouffe(600, 400, 350);
        var box = BoundsOf(pouffe.GetVertices());
        float toU = AppConstants.MM_TO_UNITS;

        Assert.AreEqual(600 * toU, box.size.x, Tol,
            "габарит для привязки берётся из EffectiveScale, а не из localScale: без "
            + "переопределения пуфик приезжал бы в снап кубом 1x1x1 м");
        Assert.AreEqual(400 * toU, box.size.y, Tol, "высота — то же самое");
        Assert.AreEqual(350 * toU, box.size.z, Tol,
            "и глубина: три РАЗНЫХ числа взяты нарочно, на кубе перепутанные оси дали "
            + "бы тот же ответ");
    }

    [Test]
    public void Pouffe_Geometry_StaysInsideItsDeclaredBox()
    {
        var pouffe = Pouffe(450, 400, 450, 120);
        var renderers = pouffe.GetComponentsInChildren<MeshRenderer>();
        Assert.IsNotEmpty(renderers, "без единого рендерера тест позеленел бы, ничего "
            + "не проверив");

        var world = renderers[0].bounds;
        foreach (var r in renderers) world.Encapsulate(r.bounds);
        float toU = AppConstants.MM_TO_UNITS;

        Assert.LessOrEqual(world.size.x, 450 * toU + Tol,
            "ни тумба, ни сидушка не вправе торчать за габарит: рамка изометрического "
            + "снимка и AABB привязки строятся по DimensionsMM");
        Assert.LessOrEqual(world.size.y, 400 * toU + Tol, "по высоте — то же");
        Assert.LessOrEqual(world.size.z, 450 * toU + Tol, "по глубине — то же");
        Assert.AreEqual(400 * toU, world.size.y, 1e-3f,
            "и при этом ЗАПОЛНЯЮТ его по высоте: тумба стоит на полу, сидушка кончается "
            + "вровень с верхом. Щель сверху или снизу видна только на рендере");
    }

    [Test]
    public void Pouffe_KeepsItsMeshAndColliderOnTheRoot_SoItCanBeClickedAndPainted()
    {
        var pouffe = DefaultPouffe();

        Assert.IsNotNull(pouffe.GetComponent<MeshFilter>(), "меш тумбы живёт на корне");
        var renderer = pouffe.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "и рендерер тоже");
        Assert.IsNotNull(pouffe.GetComponent<MeshCollider>(),
            "коллайдер обязан быть на корне: у детей семьи мебели коллайдеров нет, и без "
            + "корневого пуфик стало бы невозможно выделить мышью");
        Assert.AreSame(renderer, pouffe.DecorRenderer,
            "поверхность декора называется явно, а не ищется GetComponentInChildren: "
            + "первым найденным оказалась бы сидушка, и тайлинг уехал бы на неё");
    }

    [Test]
    public void Pouffe_DecorSurface_IsWidthByDepth_NotWidthByHeight()
    {
        var pouffe = Pouffe(450, 400, 350);

        Assert.AreEqual(new Vector2Int(450, 350), pouffe.DecorSurfaceMM,
            "у горизонтальной детали вторая ось развёртки — ГЛУБИНА: с высотой декор "
            + "растянулся бы вместо мощения (CONVENTIONS.md → «a decor tiles, it never "
            + "stretches»)");
    }

    [Test]
    public void Pouffe_Defaults_AreTheAgreedFourFiftyByFourHundredByFourFifty()
    {
        var pouffe = DefaultPouffe();

        Assert.AreEqual(new Vector3Int(450, 400, 450), pouffe.DimensionsMM,
            "габариты пуфика: 450 x 400 x 450 мм");
        Assert.AreEqual(50, pouffe.SeatThicknessMM, "сидушка 50 мм — как просил пользователь");
        Assert.AreEqual(120, pouffe.CornerRadiusMM,
            "радиус скругления в плане 120 мм — тот же, что у дивана по умолчанию");
    }

    [Test]
    public void Pouffe_CornerRadius_IsClampedToHalfTheSmallerSideOfThePlan()
    {
        var pouffe = Pouffe(600, 400, 350, 10000);

        Assert.AreEqual(175, pouffe.CornerRadiusMM,
            "полностью круглый пуфик — это радиус в половину МЕНЬШЕЙ стороны плана; "
            + "больше некуда, и обрезать обязан код, а не пользователь");
        Assert.AreEqual(Vector3.one, pouffe.transform.localScale,
            "и после подрезки корень по-прежнему единичный");
    }

    [Test]
    public void Pouffe_SeatThickness_IsClampedToAThirdOfTheHeight()
    {
        var pouffe = Pouffe(450, 400, 450, 120, 10000);

        Assert.AreEqual(133, pouffe.SeatThicknessMM,
            "сидушка толще трети пуфика превращает его в две одинаковые коробки — "
            + "конструкция «тумба с подушкой» перестаёт читаться");
    }

    [Test]
    public void Pouffe_MadeThinner_GivesTheHeightBackToTheBody()
    {
        var pouffe = Pouffe(450, 400, 450, 120, 100);
        float toU = AppConstants.MM_TO_UNITS;

        float seatTop = Seat(pouffe).localPosition.y + 100 * 0.5f * toU;
        Assert.AreEqual(400 * 0.5f * toU, seatTop, Tol,
            "верх сидушки любой толщины остаётся вровень с верхом пуфика");

        pouffe.SeatThicknessMM = 50;
        Assert.AreEqual(400 * 0.5f * toU,
            Seat(pouffe).localPosition.y + 50 * 0.5f * toU, Tol,
            "а высоту, которую сидушка отдала, забирает нижний объём: между ними не "
            + "должно появиться щели");
    }

    [Test]
    public void Pouffe_SlotLabels_TalkAboutUpholstery_NotAboutLegsItDoesNotHave()
    {
        var pouffe = DefaultPouffe();

        Assert.AreEqual("Обивка", pouffe.PrimarySlotLabel,
            "у пуфика нет столешницы — нижний объём это обивка");
        Assert.AreEqual("Сиденье", pouffe.SecondarySlotLabel,
            "и нет ножек: второй слот — сидушка. Подписи «Столешница»/«Ножки» верны "
            + "только для стола и врут для всей мягкой мебели");
    }

    [Test]
    public void Pouffe_TwoSlots_KeepTwoSeparateMaterialIds()
    {
        var pouffe = DefaultPouffe();

        pouffe.PrimaryMaterialId = "oak";
        pouffe.SecondaryMaterialId = "velvet";

        Assert.AreEqual("oak", pouffe.PrimaryMaterialId, "обивка помнит свой декор");
        Assert.AreEqual("velvet", pouffe.SecondaryMaterialId,
            "а сидушка — свой: один общий id означал бы, что второй слот в меню ничего "
            + "не делает");
        Assert.AreEqual("oak", pouffe.MaterialId,
            "MaterialId — псевдоним слота обивки: слой рендеринга держит элементы как "
            + "KitchenElement и читает именно его");
    }

    [Test]
    public void Pouffe_NullMaterialId_FallsBackToTheDefault_InsteadOfStoringNull()
    {
        var pouffe = DefaultPouffe();

        pouffe.PrimaryMaterialId = null!;

        Assert.AreEqual(MaterialCatalog.DefaultId, pouffe.PrimaryMaterialId,
            "null в id декора уехал бы в сохранение и вернулся оттуда исключением при "
            + "загрузке чужого проекта");
    }

    /// <summary>Ловушка, на которой споткнулась кровать, но в другую сторону.
    /// Скругление и толщина сидушки подрезаются ГАБАРИТОМ, поэтому фабрика
    /// обязана выставить DimensionsMM ПЕРВЫМ: на умолчательном габарите
    /// свежего компонента (0x0x0) потолок радиуса равен нулю, и заказанные
    /// 120 мм схлопнулись бы в 0 молча — пуфик приехал бы квадратным.</summary>
    [Test]
    public void Factory_SetsTheSizeBeforeTheProperties_SoNeitherIsSilentlyClamped()
    {
        var go = ElementFactory.CreatePouffe(new Vector3Int(520, 380, 410), 90, 70,
            "Пуфик-Ф", Vector3.zero);
        _spawned.Add(go);
        var pouffe = go.GetComponent<PouffeElement>();

        Assert.IsNotNull(pouffe, "фабрика обязана вернуть PouffeElement");
        Assert.AreEqual(new Vector3Int(520, 380, 410), pouffe!.DimensionsMM,
            "габарит доезжает целиком");
        Assert.AreEqual(90, pouffe.CornerRadiusMM,
            "и радиус тоже: подрезка нулевым габаритом обнулила бы его молча");
        Assert.AreEqual(70, pouffe.SeatThicknessMM,
            "и толщина сидушки — её потолок тоже считается от высоты");
    }

    [Test]
    public void Duplicate_OfAPouffe_IsAPouffe_AndKeepsBothOfItsOwnValues()
    {
        var source = Pouffe(520, 380, 410, 90, 70);
        source.PrimaryMaterialId = "oak";
        source.SecondaryMaterialId = "velvet";

        var copy = ElementDuplicators.Copy(ElementFactory.Instance, source, Vector3.one);
        _spawned.Add(copy);
        var made = copy.GetComponent<PouffeElement>();

        Assert.IsNotNull(made,
            "каждый реестр ветвится через is XxxElement: пропущенная ветка не падает, "
            + "а молча отдаёт обычную доску");
        Assert.AreEqual(90, made!.CornerRadiusMM, "копия сохраняет форму");
        Assert.AreEqual(70, made.SeatThicknessMM, "и толщину сидушки");
        Assert.AreEqual("oak", made.PrimaryMaterialId, "и декор обивки");
        Assert.AreEqual("velvet", made.SecondaryMaterialId,
            "и декор сидушки: копия с одним общим декором означала бы, что второй "
            + "слот при дублировании теряется");
    }

    [Test]
    public void Pouffe_Destroyed_TakesItsSeatWithIt()
    {
        var pouffe = DefaultPouffe();
        var seat = Seat(pouffe).gameObject;

        pouffe.PrepareForDestruction();

        Assert.IsTrue(seat == null,
            "сидушка — отдельный GameObject со своим мешем; оставленная в сцене, она "
            + "переживёт пуфик и утечёт мешем на каждую перестройку (Mesh — это "
            + "UnityEngine.Object, GC его не собирает)");
    }
}
