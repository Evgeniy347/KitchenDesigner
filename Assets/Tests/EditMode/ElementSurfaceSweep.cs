using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>ОДИН проход по всем объявленным типам элементов с настоящим спавном,
/// посчитанный один раз на весь прогон EditMode и прочитанный четырьмя наборами
/// сторожей: <see cref="CoplanarSurfaceCoverageTests"/>,
/// <see cref="ElementTintCoverageTests"/>, <see cref="DragTintCoverageTests"/> и
/// <see cref="ViolationTintSweepTests"/>.
///
/// До 2026-09-10 таких проходов было ВОСЕМЬ (один в Coplanar, четыре в
/// ElementTint, три в Drag) плюс один свой в ViolationTint — вместе около 340
/// спавнов настоящей фабрикой с построением мешей. Спавн — единственное, что
/// здесь дорого: вопросы сторожей это чтение полей и сравнение цветов, и стоят
/// они микросекунды. Поэтому проход один, а на каждом типе он собирает ВСЁ, что
/// спрашивают все сторожа (`agents/TEST-DESIGN.md` → «Дорогой перебор всех типов
/// делается ОДИН раз», по образцу <see cref="SpecificationSweep"/>).
///
/// Чувствительность не изменилась: каждый сторож задаёт РОВНО свой прежний
/// вопрос по своим показаниям — просто показания сняты с одного экземпляра, а не
/// с восьми.
///
/// ПОРЯДОК ЗАМЕРОВ на одном экземпляре — часть контракта, и он выбран так, чтобы
/// каждый шаг отдавал материалы назад прежде, чем начнётся следующий:
///   1. геометрия (`Renderer.bounds`) — чистое чтение, ничего не меняет;
///   2. кто носит собственный декор элемента — читается по ПЕРВОЗДАННОМУ
///      состоянию, до любой покраски;
///   3. подсветка перетаскивания и возврат;
///   4. то же, но краску перед возвратом ЧИТАЮТ (`renderer.material` — мутация);
///   5. выделение и снятие выделения;
///   6. НАЗНАЧЕНИЕ ДЕКОРА — единственный шаг, который меняет материал НАВСЕГДА и
///      обратно не отдаёт, поэтому он идёт после всех, кому нужна первозданность;
///   7. тон нарушения — последним, потому что первозданность ему НЕ нужна: он
///      спрашивает про СОБСТВЕННЫЙ цвет, каким бы тот ни был, и читает его на
///      месте, ровно как читал прежний отдельный проход `ViolationTintSweepTests`.
///      Собственный цвет назначенного декора вместо серого умолчания вопрос только
///      усиливает: значение, совпавшее с умолчанием, не отличить от потерянного.
///
/// Шаги 3–5 обязаны вернуть материал каждому рендереру, и каждый из них проверяет
/// это САМ своим сторожем. Чтобы сломанный возврат не выглядел потом дефектом
/// СОСЕДНЕГО сторожа, перед шагом 6 проход сверяет состояние с первозданным и,
/// если оно разошлось, пишет это в <see cref="Row.NotPristine"/> — отдельным
/// сообщением, а не молчаливым искажением чужих чисел.
///
/// Из сцены наружу не уходит ни одного объекта — только строки, числа и цвета,
/// поэтому порядок ТЕСТОВ ничего не решает, а сам проход самодостаточен: он
/// заводит себе <see cref="SelectionManager"/>, <see cref="ElementMover"/> и
/// декор каталога и убирает их за собой, не полагаясь на `[SetUp]` того класса,
/// чей тест случайно оказался первым.</summary>
public static class ElementSurfaceSweep
{
    public const string DecorId = "tint-coverage-decor";

    public sealed class Row
    {
        public Row(Type type) { ElementType = type; }

        public Type ElementType { get; }
        public string Name => ElementType.Name;

        /// <summary>Сколько рендереров нашла <c>ElementRenderers.BodyOf</c>.</summary>
        public int BodyCount;

        /// <summary>Есть ли меш на КОРНЕ элемента. У двери, окна и унитаза его нет
        /// вовсе — именно поэтому они не подсвечивались ничем.</summary>
        public bool HasRootRenderer;

        /// <summary>Пары граней, спорящих за один пиксель (z-fighting).</summary>
        public List<string> CoplanarFights = new List<string>();

        /// <summary>Тип объявил лицо ИМЕНОВАННЫМИ деталями
        /// (<c>ElementFront.Parts</c>), а не «лицевой детали нет».</summary>
        public bool DeclaresNamedFront;

        /// <summary>Причина, по которой у типа нет отдельной лицевой детали.</summary>
        public string FrontReason = "";

        /// <summary>Объявленные лицевыми детали, которых НЕ видно со стороны
        /// общей изометрической камеры: либо их закрывает собственный корпус
        /// (значит кадр снимают с затылка), либо такой детали в сцене нет.</summary>
        public List<string> FrontHidden = new List<string>();

        public List<string> DragMissed = new List<string>();
        public List<string> DragStuck = new List<string>();
        public List<string> DragStuckAfterRead = new List<string>();
        /// <summary>Чтение <c>renderer.material</c> действительно подменило материал
        /// копией. Если Unity когда-нибудь перестанет копировать, сторож возврата
        /// обязан стать неопределённым, а не ложно зелёным.</summary>
        public bool CopyOnRead = true;

        /// <summary>Рендереры с читаемым базовым цветом (URP <c>_BaseColor</c> или
        /// встроенный <c>_Color</c>) — только про них можно спрашивать про цвет.</summary>
        public int ColourWatched;
        public List<string> SelectMissed = new List<string>();
        public List<string> SelectPale = new List<string>();
        public List<string> SelectStuck = new List<string>();

        public int TintWatched;
        public List<string> TintUntinted = new List<string>();
        public List<string> TintForgotten = new List<string>();
        public List<string> TintFlattened = new List<string>();

        public int DecorWearing;
        public List<string> DecorMissed = new List<string>();

        /// <summary>Шаг, после которого состояние материалов разошлось с
        /// первозданным. Пустой список — каждый шаг отдал материалы назад.</summary>
        public List<string> NotPristine = new List<string>();
    }

    private static List<Row>? _rows;

    public static IReadOnlyList<Row> Rows => _rows ??= Run();

    public static Row Of(Type type) => Rows.First(r => r.ElementType == type);

    private static List<Row> Run()
    {
        LogAssert.ignoreFailingMessages = true;
        var globals = ProjectLoadStateGuard.Capture();
        bool tintVisibleBefore = ElementHighlighter.ViolationTintVisible;
        var selectionBefore = SelectionManager.Instance;

        ElementHighlighter.ViolationTintVisible = true;
        EveryElementType.ClearScene();
        ValidityTint.Clear();
        MaterialManager.ClearCache();
        MaterialCatalog.Register(new MaterialDef(DecorId, "Декор сторожа", "ЛДСП",
            new Color(0.13f, 0.47f, 0.29f)));

        var selectionGo = new GameObject("SelectionManager обхода");
        var selection = selectionGo.AddComponent<SelectionManager>();
        SelectionManager.Instance = selection;
        var moverGo = new GameObject("ElementMover обхода");
        var mover = moverGo.AddComponent<ElementMover>();

        var rows = new List<Row>();
        try
        {
            foreach (var type in EveryElementType.Declared())
            {
                EveryElementType.ClearScene();
                ValidityTint.Clear();
                rows.Add(Measure(type, selection, mover));
                selection.DeselectAll();
            }
        }
        finally
        {
            selection.DeselectAll();
            SelectionManager.Instance = selectionBefore;
            UnityEngine.Object.DestroyImmediate(selectionGo);
            UnityEngine.Object.DestroyImmediate(moverGo);
            EveryElementType.ClearScene();
            ValidityTint.Clear();
            MaterialManager.ClearCache();
            MaterialCatalog.Reset();
            ElementHighlighter.ViolationTintVisible = tintVisibleBefore;
            globals.Restore();
        }
        return rows;
    }

    private static Row Measure(Type type, SelectionManager selection, ElementMover mover)
    {
        var row = new Row(type);
        var element = EveryElementType.Spawn(type, "Обход" + type.Name);

        var body = ElementRenderers.BodyOf(element).Where(r => r != null).ToList();
        row.BodyCount = body.Count;
        row.HasRootRenderer = element.gameObject.GetComponent<MeshRenderer>() != null;
        var front = element.Front;
        row.DeclaresNamedFront = front.ShowsNamedParts;
        row.FrontReason = front.Reason;
        if (body.Count == 0) return row;

        var path = new string[body.Count];
        var original = new Material[body.Count];
        for (int i = 0; i < body.Count; i++)
        {
            path[i] = ElementRenderers.PathOf(element, body[i]);
            original[i] = body[i].sharedMaterial;
        }

        // 1. Геометрия. Renderer.bounds — AABB, а не поверхность: у цилиндра
        // боковая грань AABB это КАСАТЕЛЬНАЯ, возможен ложный сигнал (оговорка
        // живёт в сводке CoplanarSurfaceCoverageTests).
        if (body.Count >= 2)
        {
            var boxes = body.Select(r => CoplanarSurfaceDetector.FromWorldBounds(r.bounds)).ToArray();
            row.CoplanarFights = CoplanarSurfaceDetector.Fights(boxes, i => path[i]);
        }

        // 1б. Лицо. Тот же список рендереров, тот же AABB — вопрос другой:
        // видно ли объявленную лицевую деталь с той стороны, где стоит общая
        // изометрическая камера. Невидимая означает кадр с затылка.
        if (front.ShowsNamedParts)
        {
            var faceParts = new List<FacePart>(body.Count);
            foreach (var renderer in body)
                faceParts.Add(new FacePart(renderer.gameObject.name, renderer.bounds));
            row.FrontHidden = FrontFaceVisibility.Hidden(faceParts, front.PartNames,
                IsoCameraRig.ViewDir);
        }

        // 2. Кто носит собственный декор элемента — по первозданному состоянию.
        var ownDecor = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(element.MaterialId));
        var wearing = new List<int>();
        if (ownDecor != null)
            for (int i = 0; i < body.Count; i++)
                if (ReferenceEquals(original[i], ownDecor)) wearing.Add(i);
        row.DecorWearing = wearing.Count;

        // Рендереры с читаемым цветом: только про них сторожа спрашивают про ЦВЕТ.
        var colourWatched = new List<int>();
        var colourBefore = new Dictionary<int, Color>();
        for (int i = 0; i < body.Count; i++)
            if (TryReadBaseColor(original[i], out var colour))
            {
                colourWatched.Add(i);
                colourBefore[i] = colour;
            }
        row.ColourWatched = colourWatched.Count;

        // 3. Подсветка перетаскивания и возврат.
        mover.SaveDragMaterial(element);
        for (int i = 0; i < body.Count; i++)
            if (ReferenceEquals(body[i].sharedMaterial, original[i])) row.DragMissed.Add(path[i]);
        mover.RestoreDragMaterial();
        for (int i = 0; i < body.Count; i++)
            if (!ReferenceEquals(body[i].sharedMaterial, original[i])) row.DragStuck.Add(path[i]);

        // 4. Тот же возврат, но краску перед ним ЧИТАЮТ: renderer.material —
        // мутация, Unity отдаёт КОПИЮ, и возврат «по адресу» видит чужой материал.
        mover.SaveDragMaterial(element);
        foreach (var renderer in body)
        {
            var painted = renderer.sharedMaterial;
            var copy = renderer.material;
            if (painted != null && ReferenceEquals(copy, painted)) row.CopyOnRead = false;
        }
        mover.RestoreDragMaterial();
        for (int i = 0; i < body.Count; i++)
            if (!ReferenceEquals(body[i].sharedMaterial, original[i]))
                row.DragStuckAfterRead.Add(path[i]);

        // 5. Выделение и снятие выделения.
        selection.Select(element);
        for (int i = 0; i < body.Count; i++)
            if (ReferenceEquals(body[i].sharedMaterial, original[i])) row.SelectMissed.Add(path[i]);
        foreach (int i in colourWatched)
        {
            if (!TryReadBaseColor(body[i].sharedMaterial, out var now))
            {
                row.SelectPale.Add(path[i] + " (цвет пропал)");
                continue;
            }
            if (Visibly(colourBefore[i], now)) continue;
            row.SelectPale.Add(path[i] + " " + Describe(colourBefore[i]) + " → " + Describe(now));
        }
        selection.DeselectAll();
        for (int i = 0; i < body.Count; i++)
            if (!ReferenceEquals(body[i].sharedMaterial, original[i])) row.SelectStuck.Add(path[i]);

        // 6. Декор. Единственный шаг, который меняет материал НАВСЕГДА и обратно не
        // отдаёт, — поэтому он предпоследний, а последним идёт тон нарушения,
        // которому первозданность и не нужна: он спрашивает про СОБСТВЕННЫЙ цвет,
        // каким бы тот ни был, и читает его на месте (так же читал и прежний
        // отдельный проход ViolationTintSweepTests). Собственный цвет назначенного
        // декора вместо серого умолчания вопрос только усиливает: значение,
        // совпавшее с умолчанием, не отличить от потерянного.
        NotePristine(row, "назначением декора", body, original, path);
        if (wearing.Count > 0)
        {
            var def = MaterialCatalog.Get(DecorId);
            ApplyDecorThroughEverySlot(element, def);
            var newDecor = MaterialManager.GetSharedMaterial(def);
            foreach (int i in wearing)
                if (!ReferenceEquals(body[i].sharedMaterial, newDecor)) row.DecorMissed.Add(path[i]);
        }

        // 7. Тон нарушения. Задаётся ПРЯМО, а не выстраиванием наезда: вопрос —
        // «получил ли тон объект, про который сказано, что он нарушает», и он
        // обязан звучать для типов, которым валидатор наезда не выпишет вовсе.
        var own = new Material[body.Count];
        var tintWatched = new List<int>();
        for (int i = 0; i < body.Count; i++)
        {
            own[i] = body[i].sharedMaterial;
            if (own[i] != null && HasColour(own[i])) tintWatched.Add(i);
        }
        row.TintWatched = tintWatched.Count;

        if (tintWatched.Count > 0)
        {
            ElementHighlighter.ApplyMaterial(element, isValid: false);
            foreach (int i in tintWatched)
            {
                var ownColour = ValidityTint.BaseColorOf(own[i]);
                var now = body[i].sharedMaterial;

                if (now == null || ReferenceEquals(now, own[i]))
                {
                    row.TintUntinted.Add(path[i] + " " + Describe(ownColour)
                        + " — материал не тронут вовсе");
                    continue;
                }

                if (!Visibly(ownColour, ValidityTint.BaseColorOf(now))
                    && FarFrom(ownColour, ValidityTint.ViolationColor))
                {
                    row.TintUntinted.Add(path[i] + " " + Describe(ownColour)
                        + " — материал другой, а ЦВЕТ тот же: пользователь не увидит ничего");
                    continue;
                }

                if (!ReferenceEquals(ValidityTint.OwnOf(now), own[i])) row.TintForgotten.Add(path[i]);

                var tinted = ValidityTint.BaseColorOf(now);
                if (FarFrom(ownColour, ValidityTint.ViolationColor)
                    && !Visibly(tinted, ValidityTint.ViolationColor))
                    row.TintFlattened.Add(path[i] + " " + Describe(ownColour)
                        + " → " + Describe(tinted));
            }
        }

        return row;
    }

    private static void NotePristine(Row row, string beforeWhat, List<MeshRenderer> body,
        Material[] original, string[] path)
    {
        var drifted = new List<string>();
        for (int i = 0; i < body.Count; i++)
            if (!ReferenceEquals(body[i].sharedMaterial, original[i])) drifted.Add(path[i]);
        if (drifted.Count == 0) return;
        row.NotPristine.Add("перед " + beforeWhat + " материал не был первозданным у "
            + drifted.Count + " из " + body.Count + " — " + string.Join(", ", drifted));
    }

    private static void ApplyDecorThroughEverySlot(KitchenElement element, MaterialDef def)
    {
        if (element is IHasTwoDecorSlots slots)
        {
            MaterialManager.ApplyPrimarySlot(slots, def);
            MaterialManager.ApplySecondarySlot(slots, def);
            return;
        }
        MaterialManager.Apply(element, def);
    }

    public const float MinColorStep = 0.05f;

    public static bool Visibly(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) > MinColorStep
        || Mathf.Abs(a.g - b.g) > MinColorStep
        || Mathf.Abs(a.b - b.b) > MinColorStep;

    /// <summary>Порог «своего цвета видно» умножен на запас: тон — половина пути
    /// к красному (<c>ViolationStrength</c>), поэтому объект, чей собственный цвет
    /// и так почти красный, сходится к красному законно.</summary>
    public static bool FarFrom(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) > 0.3f
        || Mathf.Abs(a.g - b.g) > 0.3f
        || Mathf.Abs(a.b - b.b) > 0.3f;

    public static string Describe(Color c) =>
        "(" + c.r.ToString("0.00") + " " + c.g.ToString("0.00") + " " + c.b.ToString("0.00") + ")";

    private static bool HasColour(Material material) =>
        material.HasProperty("_BaseColor") || material.HasProperty("_Color");

    /// <summary>URP-шейдеры держат цвет в <c>_BaseColor</c>, встроенные — в
    /// <c>_Color</c>. Материал без обоих в счёт не идёт: о его цвете сторож ничего
    /// сказать не может и врать не должен.</summary>
    public static bool TryReadBaseColor(Material? material, out Color color)
    {
        color = default;
        if (material == null) return false;
        if (material.HasProperty("_BaseColor")) { color = material.GetColor("_BaseColor"); return true; }
        if (material.HasProperty("_Color")) { color = material.GetColor("_Color"); return true; }
        return false;
    }

    /// <summary>Сторож самого прохода: сломанный возврат на одном шаге исказил бы
    /// числа СОСЕДНЕГО сторожа, и чинить пошли бы не то. Читается всеми четырьмя
    /// наборами через <see cref="ElementSurfaceSweepTests"/>.</summary>
    public static List<string> DriftReport() =>
        Rows.Where(r => r.NotPristine.Count > 0)
            .Select(r => r.Name + ": " + string.Join("; ", r.NotPristine))
            .ToList();
}

/// <summary>Сторож прохода, а не продукта: сам по себе общий проход обязан быть
/// честным стендом для четырёх наборов, которые его читают.</summary>
public class ElementSurfaceSweepTests
{
    [Test]
    public void TheSweep_SeesEveryDeclaredType_OtherwiseItIsNotASweep()
    {
        var rows = ElementSurfaceSweep.Rows;
        Assert.AreEqual(EveryElementType.Declared().Count, rows.Count,
            "проход обязан пройти по КАЖДОМУ объявленному типу — иначе он перебор только "
            + "на словах");
        Assert.Greater(rows.Count, 20,
            "типов в сборке заметно меньше, чем было — рефлексия по сборке сузилась, "
            + "и перебор больше не перебор");
    }

    /// <summary>Каждый шаг прохода обязан отдать материалы назад, иначе следующий
    /// шаг замеряет чужую покраску и его сторож краснеет не за свой дефект.</summary>
    [Test]
    public void EveryStepOfTheSweep_GivesTheMaterialsBack_SoTheNextGuardMeasuresItsOwnDefect()
    {
        var drift = ElementSurfaceSweep.DriftReport();
        Assert.IsEmpty(drift,
            "общий проход снимает показания нескольких сторожей с ОДНОГО экземпляра, и "
            + "каждый шаг обязан вернуть материал каждому рендереру. Здесь он не вернул — "
            + "значит числа следующего сторожа замерены не с первозданного состояния, и "
            + "его падение (или зелень) ничего не значит. Чинить надо шаг, названный ниже, "
            + "а не того, кто покраснел следом:\n  " + string.Join("\n  ", drift));
    }
}
