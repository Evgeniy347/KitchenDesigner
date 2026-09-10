using System.Collections.Generic;
using System.Linq;
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
/// типов здесь не выписан руками: он выводится из сборки, и новый тип попадает
/// под проверку сам.
///
/// Дорогой проход (собрать по одному экземпляру КАЖДОГО типа настоящей
/// фабрикой) делается ОДИН раз на весь прогон EditMode и живёт в общем
/// <see cref="ElementSurfaceSweep"/> — вместе с тремя соседними наборами,
/// которые задают свои вопросы тому же экземпляру. Тесты ниже читают уже снятые
/// показания. Из сцены наружу не уходит ни одного объекта — только строки и
/// числа, поэтому порядок тестов ничего не решает.
///
/// Нарушение задаётся ПРЯМО (<c>ApplyMaterial(element, isValid: false)</c>), а
/// не выстраиванием наезда: вопрос сторожа — «получил ли тон объект, про
/// который сказано, что он нарушает», и он обязан звучать для типов, которым
/// валидатор наезда не выпишет вовсе (светильник — <c>ElementKind.Decor</c>).
/// Что настоящий наезд доезжает до тона, проверяет
/// <see cref="RealOverlap_EndToEnd_TintsThePart"/> — противоположный вход к
/// прямому вызову, и он остаётся со своей собственной сценой на две детали.
///
/// Требует настоящий Unity (реальные <c>GameObject</c>/<c>MeshRenderer</c>),
/// поэтому в <c>mutation-test.ps1 -TestsOnly</c> не входит.</summary>
public class ViolationTintSweepTests
{
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

    [Test]
    public void EveryDeclaredElementType_InViolation_GetsTinted()
    {
        var offenders = new List<string>();
        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.TintUntinted.Count > 0)
                offenders.Add(row.Name + ": без тона остались "
                    + row.TintUntinted.Count + " из " + row.TintWatched + " — "
                    + string.Join(", ", row.TintUntinted));

        Report("Любой объект с нарушением обязан быть затонирован — исключений нет.\n"
            + "Дефект не логируется и ничем не падает: пользователь просто не видит, что "
            + "объект нарушает правило. Так молчали подложка-план, светильник, стена, пол "
            + "и любая деталь на дефолтном декоре.\n", offenders);
    }

    [Test]
    public void EveryDeclaredElementType_KeepsItsOwnColourUnderTheTint_ItIsNotAFlatRepaint()
    {
        var offenders = new List<string>();
        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.TintFlattened.Count > 0)
                offenders.Add(row.Name + ": цвет стал ровно красным на "
                    + row.TintFlattened.Count + " из " + row.TintWatched + " — "
                    + string.Join(", ", row.TintFlattened));

        Report("Тон обязан ПОДМЕШИВАТЬСЯ к собственному цвету, а не заменять его.\n"
            + "Объект, ставший сплошным красным, теряет свой декор и свою форму: именно на "
            + "это жаловался пользователь. Мера подмеса — ValidityTint.ViolationStrength.\n",
            offenders);
    }

    [Test]
    public void EveryTint_RemembersTheMaterialItWasMadeFrom_SoTheViolationCanBeUndone()
    {
        var offenders = new List<string>();
        foreach (var row in ElementSurfaceSweep.Rows)
            if (row.TintForgotten.Count > 0)
                offenders.Add(row.Name + ": источник забыт у "
                    + row.TintForgotten.Count + " из " + row.TintWatched + " — "
                    + string.Join(", ", row.TintForgotten));

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
        var rows = ElementSurfaceSweep.Rows;
        var blind = rows.Where(r => r.TintWatched == 0).Select(r => r.Name).ToList();

        Assert.Greater(rows.Count, 20,
            "типов в сборке заметно меньше, чем было — рефлексия по сборке сузилась, "
            + "и перебор больше не перебор");
        Assert.Less(blind.Count, rows.Count / 4,
            "у слишком многих типов не нашлось ни одного рендерера с читаемым цветом: "
            + "проход смотрит в пустоту и позеленеет на любом коде. Молчат: "
            + string.Join(", ", blind));
    }

    /// <summary>Противоположный вход к прямому <c>ApplyMaterial(..., false)</c>:
    /// НАСТОЯЩИЙ наезд обязан доезжать до тона через валидатор. Без этого теста
    /// весь класс проверял бы только вторую половину пути. Сцена своя и
    /// крошечная — две детали, — поэтому в общий проход она не идёт.</summary>
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

            Assert.IsTrue(ElementSurfaceSweep.Visibly(ownColour, tinted),
                "серая деталь на дефолтном декоре обязана покраснеть: "
                + ElementSurfaceSweep.Describe(ownColour)
                + " → " + ElementSurfaceSweep.Describe(tinted));
            Assert.IsTrue(ElementSurfaceSweep.Visibly(tinted, ValidityTint.ViolationColor),
                "и обязана остаться СВОЕЙ: серая деталь под тоном не равна красной "
                + ElementSurfaceSweep.Describe(ValidityTint.ViolationColor)
                + ", иначе это перекраска, а не тон. "
                + "Получилось " + ElementSurfaceSweep.Describe(tinted));
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

    private static void Report(string header, List<string> offenders)
    {
        if (offenders.Count == 0) return;
        Assert.Fail(header + offenders.Count + " тип(ов):\n  "
            + string.Join("\n  ", offenders));
    }
}
