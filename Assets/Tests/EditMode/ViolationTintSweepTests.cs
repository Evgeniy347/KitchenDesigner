using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Правило пользователя: ЛЮБОЙ объект с нарушением затонирован, и тон
/// — это тон, а не перекраска.
///
/// Почему сторож перебирает типы, а не проверяет три случая поимённо. Тонировка
/// нарушения жила на трёх исключениях, и каждое из них было отдельным способом
/// НЕ показать нарушение: <c>KeepsItsOwnMaterialAlways</c> выводил из покраски
/// подложку-план и светильник, <c>TintedOnlyByItsOwnDecor</c> — стену и пол, а
/// <c>MaterialManager.HasCustomDecor</c> делал развилку по признаку «деталь
/// серая» (декор дефолтный). Причина у всех трёх была одна: тон был СПЛОШНОЙ
/// заливкой и съедал декор, поэтому целые классы объектов от него уводили.
/// Причина ушла вместе с заливкой — тон подмешивается к собственному цвету
/// (<see cref="ValidityTint"/>), — и исключений не осталось ни одного. Список
/// типов здесь не выписан руками: он берётся из
/// <c>EveryElementType.Declared()</c>, то есть выводится из сборки, и новый тип
/// попадает под проверку сам.
///
/// Дорогой проход (собрать по одному экземпляру КАЖДОГО типа настоящей
/// фабрикой) делается ОДИН раз на весь класс и кладётся в
/// <see cref="_sweep"/>; тесты ниже читают уже снятые показания, как это делает
/// <c>SpecificationCoverageGuardTests</c>. Из сцены наружу не уходит ни одного
/// объекта — только строки и числа, поэтому порядок тестов ничего не решает.
///
/// Нарушение задаётся ПРЯМО (<c>ApplyMaterial(element, isValid: false)</c>), а
/// не выстраиванием наезда: вопрос сторожа — «получил ли тон объект, про
/// который сказано, что он нарушает», и он обязан звучать для типов, которым
/// валидатор наезда не выпишет вовсе (светильник — <c>ElementKind.Decor</c>).
/// Что настоящий наезд доезжает до тона, проверяет
/// <see cref="RealOverlap_EndToEnd_TintsThePart"/> — противоположный вход к
/// прямому вызову.
///
/// Требует настоящий Unity (реальные <c>GameObject</c>/<c>MeshRenderer</c>),
/// поэтому в <c>mutation-test.ps1 -TestsOnly</c> не входит.</summary>
public class ViolationTintSweepTests
{
    private sealed class Reading
    {
        public string Type = "";
        public int Watched;
        public readonly List<string> Untinted = new List<string>();
        public readonly List<string> Forgotten = new List<string>();
        public readonly List<string> Flattened = new List<string>();
    }

    private static List<Reading>? _sweep;

    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _globals = ProjectLoadStateGuard.Capture();
        ElementHighlighter.ViolationTintVisible = true;
    }

    [TearDown]
    public void TearDown()
    {
        EveryElementType.ClearScene();
        ValidityTint.Clear();
        MaterialManager.ClearCache();
        _globals?.Restore();
        LogAssert.ignoreFailingMessages = false;
    }

    /// <summary>Тот самый один дорогой проход. Показания снимаются с
    /// <c>sharedMaterial</c>: чтение <c>renderer.material</c> — мутация
    /// (<c>CONVENTIONS.md</c> → «Reading `renderer.material` is a MUTATION»), и
    /// она подменила бы материал копией следующему сторожу.</summary>
    private static List<Reading> Sweep()
    {
        if (_sweep != null) return _sweep;

        var readings = new List<Reading>();
        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            ValidityTint.Clear();

            var element = EveryElementType.Spawn(type, "Тон" + type.Name);
            var reading = new Reading { Type = type.Name };
            readings.Add(reading);

            var body = ElementRenderers.BodyOf(element);
            var own = new List<Material>();
            var watched = new List<MeshRenderer>();
            foreach (var renderer in body)
            {
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null) continue;
                if (!HasColour(material)) continue;
                watched.Add(renderer);
                own.Add(material);
            }
            reading.Watched = watched.Count;
            if (watched.Count == 0) continue;

            ElementHighlighter.ApplyMaterial(element, isValid: false);

            for (int i = 0; i < watched.Count; i++)
            {
                string where = ElementRenderers.PathOf(element, watched[i]);
                var now = watched[i].sharedMaterial;
                var ownColour = ValidityTint.BaseColorOf(own[i]);

                if (now == null || ReferenceEquals(now, own[i]))
                {
                    reading.Untinted.Add(where + " " + Describe(ownColour)
                        + " — материал не тронут вовсе");
                    continue;
                }

                if (!Visibly(ownColour, ValidityTint.BaseColorOf(now))
                    && FarFrom(ownColour, ValidityTint.ViolationColor))
                {
                    reading.Untinted.Add(where + " " + Describe(ownColour)
                        + " — материал другой, а ЦВЕТ тот же: пользователь не увидит ничего");
                    continue;
                }

                if (!ReferenceEquals(ValidityTint.OwnOf(now), own[i]))
                    reading.Forgotten.Add(where);

                var tinted = ValidityTint.BaseColorOf(now);
                if (FarFrom(ownColour, ValidityTint.ViolationColor)
                    && !Visibly(tinted, ValidityTint.ViolationColor))
                    reading.Flattened.Add(where + " " + Describe(ownColour)
                        + " → " + Describe(tinted));
            }
        }

        EveryElementType.ClearScene();
        ValidityTint.Clear();
        _sweep = readings;
        return _sweep;
    }

    [Test]
    public void EveryDeclaredElementType_InViolation_GetsTinted()
    {
        var offenders = new List<string>();
        foreach (var reading in Sweep())
            if (reading.Untinted.Count > 0)
                offenders.Add(reading.Type + ": без тона остались "
                    + reading.Untinted.Count + " из " + reading.Watched + " — "
                    + string.Join(", ", reading.Untinted));

        Report("Любой объект с нарушением обязан быть затонирован — исключений нет.\n"
            + "Дефект не логируется и ничем не падает: пользователь просто не видит, что "
            + "объект нарушает правило. Так молчали подложка-план, светильник, стена, пол "
            + "и любая деталь на дефолтном декоре.\n", offenders);
    }

    [Test]
    public void EveryDeclaredElementType_KeepsItsOwnColourUnderTheTint_ItIsNotAFlatRepaint()
    {
        var offenders = new List<string>();
        foreach (var reading in Sweep())
            if (reading.Flattened.Count > 0)
                offenders.Add(reading.Type + ": цвет стал ровно красным на "
                    + reading.Flattened.Count + " из " + reading.Watched + " — "
                    + string.Join(", ", reading.Flattened));

        Report("Тон обязан ПОДМЕШИВАТЬСЯ к собственному цвету, а не заменять его.\n"
            + "Объект, ставший сплошным красным, теряет свой декор и свою форму: именно на "
            + "это жаловался пользователь. Мера подмеса — ValidityTint.ViolationStrength.\n",
            offenders);
    }

    [Test]
    public void EveryTint_RemembersTheMaterialItWasMadeFrom_SoTheViolationCanBeUndone()
    {
        var offenders = new List<string>();
        foreach (var reading in Sweep())
            if (reading.Forgotten.Count > 0)
                offenders.Add(reading.Type + ": источник забыт у "
                    + reading.Forgotten.Count + " из " + reading.Watched + " — "
                    + string.Join(", ", reading.Forgotten));

        Report("Снятое нарушение обязано вернуть КАЖДОМУ рендереру его материал, а вернуть "
            + "его можно только помня источник. Тон, забывший источник, остаётся на объекте "
            + "навсегда — либо, что хуже, тонируется повторно от самого себя и краснеет "
            + "до сплошного.\n", offenders);
    }

    /// <summary>Сторож самого сторожа: проход обязан находить, что смотреть.
    /// Тип без рендерера с читаемым цветом три теста выше пропускают молча, и
    /// если таких вдруг станет большинство — они позеленеют, ничего не
    /// проверив.</summary>
    [Test]
    public void TheSweep_WatchesAColouredRendererOnAlmostEveryType_OtherwiseItProvesNothing()
    {
        var readings = Sweep();
        var blind = new List<string>();
        foreach (var reading in readings)
            if (reading.Watched == 0) blind.Add(reading.Type);

        Assert.Greater(readings.Count, 20,
            "типов в сборке заметно меньше, чем было — рефлексия по сборке сузилась, "
            + "и перебор больше не перебор");
        Assert.Less(blind.Count, readings.Count / 4,
            "у слишком многих типов не нашлось ни одного рендерера с читаемым цветом: "
            + "проход смотрит в пустоту и позеленеет на любом коде. Молчат: "
            + string.Join(", ", blind));
    }

    /// <summary>Противоположный вход к прямому <c>ApplyMaterial(..., false)</c>:
    /// НАСТОЯЩИЙ наезд обязан доезжать до тона через валидатор. Без этого теста
    /// весь класс проверял бы только вторую половину пути.</summary>
    [Test]
    public void RealOverlap_EndToEnd_TintsThePart()
    {
        EveryElementType.ClearScene();
        var host = new GameObject("Highlighter");
        try
        {
            var highlighter = host.AddComponent<ElementHighlighter>();

            var a = Part(new Vector3Int(600, 18, 500), "НаездA");
            Part(new Vector3Int(600, 18, 500), "НаездB");
            var renderer = a.GetComponent<MeshRenderer>();
            var ownColour = ValidityTint.BaseColorOf(renderer.sharedMaterial);

            Assume.That(ConstraintValidator.Validate(PartRegistry.GetAll()).violations.Contains(a),
                Is.True, "вторая деталь стоит ровно на первой — без нарушения тест пуст");

            highlighter.RefreshHighlights();
            var tinted = ValidityTint.BaseColorOf(renderer.sharedMaterials[0]);

            Assert.IsTrue(Visibly(ownColour, tinted),
                "серая деталь на дефолтном декоре обязана покраснеть: " + Describe(ownColour)
                + " → " + Describe(tinted));
            Assert.IsTrue(Visibly(tinted, ValidityTint.ViolationColor),
                "и обязана остаться СВОЕЙ: серая деталь под тоном не равна красной "
                + Describe(ValidityTint.ViolationColor) + ", иначе это перекраска, а не тон. "
                + "Получилось " + Describe(tinted));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static KitchenElement Part(Vector3Int dims, string name)
    {
        var go = ElementFactory.CreatePart(dims, name, Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        element.DimensionsMM = dims;
        element.ApplyDimensions();
        return element;
    }

    private const float MinColorStep = 0.05f;

    /// <summary>Порог «своего цвета видно» умножен на запас: тон — половина
    /// пути к красному (<c>ViolationStrength</c>), поэтому объект, чей
    /// собственный цвет и так почти красный, сходится к красному законно, и
    /// спрашивать про него нельзя.</summary>
    private static bool FarFrom(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) > 0.3f
        || Mathf.Abs(a.g - b.g) > 0.3f
        || Mathf.Abs(a.b - b.b) > 0.3f;

    private static bool Visibly(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) > MinColorStep
        || Mathf.Abs(a.g - b.g) > MinColorStep
        || Mathf.Abs(a.b - b.b) > MinColorStep;

    private static bool HasColour(Material material) =>
        material.HasProperty("_BaseColor") || material.HasProperty("_Color");

    private static string Describe(Color c) =>
        "(" + c.r.ToString("0.00") + " " + c.g.ToString("0.00") + " " + c.b.ToString("0.00") + ")";

    private static void Report(string header, List<string> offenders)
    {
        if (offenders.Count == 0) return;
        Assert.Fail(header + offenders.Count + " тип(ов):\n  "
            + string.Join("\n  ", offenders));
    }
}
