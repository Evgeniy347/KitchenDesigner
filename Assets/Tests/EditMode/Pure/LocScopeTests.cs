using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary><c>Loc.Scope</c> даёт ПОТОКУ собственный локализатор: фикстуры чистого слоя
/// идут параллельно, а <c>Loc</c> глобален, и язык, выставленный одним тестом, виден чужому
/// посреди прогона. Область держит <c>[ThreadStatic]</c>-поле и возвращает прежнее
/// значение при <c>Dispose</c>.
///
/// Локализаторы здесь собраны вручную (одна строка «probe»), поэтому тесты не зависят от
/// файлов перевода, а различаются по ссылке: по языку нельзя, потому что все области в
/// тесте могут говорить на одном языке. Глобальный <c>Loc</c> эти тесты не меняют —
/// это делает <c>LocUseWhileScopedTests</c>, который не может идти параллельно.</summary>
public class LocScopeTests
{
    private const string Key = "probe";

    private static Localizer Speaking(string text) =>
        new Localizer(new[] { new StringTable("ru", new Dictionary<string, string> { [Key] = text }) }, "ru");

    [Test]
    public void Scope_RestoresTheOuterOnDispose()
    {
        var global = Loc.Current;
        var scoped = Speaking("в области");

        var scope = Loc.Scope(scoped);
        try
        {
            Assert.AreSame(scoped, Loc.Current, "внутри области текущий локализатор — её собственный");
            Assert.AreEqual("в области", Loc.T(Key), "и строки берутся из него");
            scope.Dispose();

            Assert.AreSame(global, Loc.Current,
                "после Dispose область обязана вернуть прежний (глобальный) локализатор: иначе язык "
                + "одного теста навсегда остаётся на потоке и ломает следующий, который на нём запустят");
        }
        finally
        {
            scope.Dispose();
        }
    }

    [Test]
    public void Scope_Nested_UnwindsInOrder()
    {
        var global = Loc.Current;
        var outer = Speaking("внешняя");
        var middle = Speaking("средняя");
        var inner = Speaking("внутренняя");

        var outerScope = Loc.Scope(outer);
        var middleScope = Loc.Scope(middle);
        var innerScope = Loc.Scope(inner);
        try
        {
            Assert.AreSame(inner, Loc.Current, "самая вложенная область побеждает");

            innerScope.Dispose();
            Assert.AreSame(middle, Loc.Current,
                "снятие внутренней области возвращает СРЕДНЮЮ, а не глобальный локализатор: "
                + "Dispose, который просто обнуляет поле, ломает вложенные области (setup фикстуры + тест)");

            middleScope.Dispose();
            Assert.AreSame(outer, Loc.Current, "затем внешнюю");

            outerScope.Dispose();
            Assert.AreSame(global, Loc.Current, "и только после последней — глобальный");
        }
        finally
        {
            innerScope.Dispose();
            middleScope.Dispose();
            outerScope.Dispose();
        }
    }

    [Test]
    public void Scope_DoesNotLeakToAnotherThread()
    {
        var global = Loc.Current;
        var scoped = Speaking("только моя");
        Localizer? seenByNeighbour = null;
        string? neighbourText = null;

        using (Loc.Scope(scoped))
        {
            var neighbour = new Thread(() =>
            {
                seenByNeighbour = Loc.Current;
                neighbourText = Loc.T(Key);
            });
            neighbour.Start();
            neighbour.Join();
            Assert.AreSame(scoped, Loc.Current, "а на своём потоке область по-прежнему действует");
        }

        Assert.AreSame(global, seenByNeighbour,
            "соседний поток видит глобальный локализатор, а не нашу область: фикстуры идут "
            + "параллельно, и общее поле показало бы чужому тесту наш язык посреди его прогона");
        Assert.AreNotEqual("только моя", neighbourText, "и строки у соседа не наши");
    }
}
