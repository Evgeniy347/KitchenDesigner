using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Сторож против «вылезли за пределы окна и даже не знаем об этом».
///
/// Дважды одно и то же: каталог сайдбара перерос корень задолго до того, как
/// это заметили, и окно настроек уехало вниз, когда фоторежим добавил 37
/// параметров. Оба раза размещение строк было ВЕРНЫМ — врала высота, и не было
/// никого, кто бы их сравнил. Этот класс и есть тот, кто сравнивает.
///
/// Правило одно, областей две. Для каждого окна берём его собственный
/// прямоугольник и прямоугольник содержимого каждой области прокрутки внутри;
/// в каждой из них самый нижний узел обязан лежать выше нижнего края. Из этого
/// сами собой ловятся обе болезни: содержимое без прокрутки, вылезшее за
/// панель, и прокрутка, чья высота содержимого меньше того, что в неё положили
/// (ровно случай сайдбара — строки на месте, доскроллить до них нельзя).
///
/// Считает сторож той же <see cref="ContentExtent.Measure"/>, что и рабочая
/// <c>WindowBody.Fit</c>: разойтись с тем, что делает приложение, ему негде.
///
/// Проверяем ТОЛЬКО нижний край: жалоба пользователя про него, а слева и справа
/// панели штатно выпускают полосы прокрутки наружу (SpecificationPanelUI).</summary>
public class WindowOverflowGuardTests
{
    private const float ProbePanelW = 600f;
    private const float ProbePanelH = 900f;
    private const float ProbeTopInset = 122f;
    private const float ProbeBottomInset = 82f;
    private const float ProbeSidePad = 20f;
    private const float ProbeFirstRowY = ProbePanelH * 0.5f - 138f;
    private const int RowsThatOverflowTheSettingsWindow = 27;

    private GameObject? _canvasGo;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_canvasGo != null) UnityEngine.Object.DestroyImmediate(_canvasGo);
        _canvasGo = null;
        ProjectWindows.Clear();
        yield return null;
    }

    private static MethodInfo? BuildMethod(Type type)
    {
        var method = type.GetMethod("Build", new[] { typeof(Transform) });
        return method != null && method.ReturnType == typeof(void) ? method : null;
    }

    private static List<Type> AllWindowTypes() =>
        typeof(IProjectWindow).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t)
                && typeof(IProjectWindow).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();

    private Transform NewCanvas()
    {
        _canvasGo = UIFactory.CreateCanvas("GuardCanvas").gameObject;
        return _canvasGo.transform;
    }

    private static void CollectOverflow(string window, RectTransform rect, List<string> offenders)
    {
        Check(window + " / само окно", rect, offenders);

        foreach (var scroll in rect.GetComponentsInChildren<ScrollRect>(true))
        {
            var content = scroll.content;
            if (content == null || !content.gameObject.activeInHierarchy) continue;
            Check($"{window} / содержимое области «{scroll.name}»", content, offenders);
        }
    }

    private static void Check(string where, RectTransform region, List<string> offenders)
    {
        var spans = new List<ContentSpan>();
        RectSpans.Collect(region, spans);
        var fit = ContentExtent.Measure(spans, 0f, region.rect.height);
        if (fit.Overflows) offenders.Add($"{where}: {fit}");
    }

    private static RectTransform ProbePanel(Transform canvas, int rows, bool scrolling)
    {
        var panel = UIFactory.CreatePanel("GuardProbe", canvas, Vector2.zero,
            new Vector2(ProbePanelW, ProbePanelH));
        UIFactory.AnchorCenter(panel.rectTransform);
        panel.rectTransform.anchoredPosition = Vector2.zero;

        Transform host = panel.transform;
        WindowBody? body = null;
        if (scrolling)
        {
            body = WindowBody.Create(panel.rectTransform, ProbeTopInset, ProbeBottomInset,
                ProbeSidePad);
            var page = new GameObject("ProbePage");
            page.transform.SetParent(body.Content, false);
            page.transform.localPosition = new Vector3(0f, body.PanelOriginY, 0f);
            host = page.transform;
        }

        float y = ProbeFirstRowY;
        for (int i = 0; i < rows; i++)
        {
            SettingsRowFactory.CreateRow("ProbeRow_" + i, host, y);
            y -= SettingsRowFactory.RowStep;
        }

        body?.Fit();
        return panel.rectTransform;
    }

    [UnityTest]
    public IEnumerator Guard_FixedHeightPanelStuffedWithRows_GoesRed()
    {
        var canvas = NewCanvas();
        var panel = ProbePanel(canvas, RowsThatOverflowTheSettingsWindow, scrolling: false);
        yield return null;

        var offenders = new List<string>();
        CollectOverflow("проба", panel, offenders);

        Assert.IsNotEmpty(offenders,
            "27 строк по 38 px в панели 900 px — это ровно то, во что превратилось окно "
            + "настроек, и сторож ОБЯЗАН на этом краснеть. Если здесь пусто, то зелёный "
            + "прогон соседнего теста ничего не значит: сторож просто ничего не видит");
        StringAssert.Contains("ProbeRow_26", offenders[0],
            "и обязан назвать самый нижний узел: без имени красный сторож придётся "
            + "расследовать вручную по дереву из сотен RectTransform");
    }

    [UnityTest]
    public IEnumerator Guard_SameRowsInsideAScrollArea_StaysGreen()
    {
        var canvas = NewCanvas();
        var panel = ProbePanel(canvas, RowsThatOverflowTheSettingsWindow, scrolling: true);
        yield return null;

        var offenders = new List<string>();
        CollectOverflow("проба", panel, offenders);

        Assert.IsEmpty(offenders,
            "те же 27 строк, положенные в WindowBody, обязаны стать зелёными — иначе "
            + "сторож красный всегда и его выключат. Пара с предыдущим тестом: они "
            + "различаются ровно одним — наличием области прокрутки:\n"
            + string.Join("\n", offenders));
    }

    [UnityTest]
    public IEnumerator Guard_ScrollAreaWhoseContentHeightWasNeverSet_GoesRed()
    {
        var canvas = NewCanvas();
        var panel = UIFactory.CreatePanel("GuardProbe", canvas, Vector2.zero,
            new Vector2(ProbePanelW, ProbePanelH));
        UIFactory.AnchorCenter(panel.rectTransform);
        var body = WindowBody.Create(panel.rectTransform, ProbeTopInset, ProbeBottomInset,
            ProbeSidePad);
        SettingsRowFactory.CreateRow("Unreachable", body.Content, -200f);
        yield return null;

        var offenders = new List<string>();
        CollectOverflow("проба", panel.rectTransform, offenders);

        Assert.IsNotEmpty(offenders,
            "область прокрутки есть, а высоту содержимого никто не выставил — строки "
            + "на месте, доскроллить до них нельзя. Это болезнь каталога сайдбара, и "
            + "сторож обязан ловить её отдельно от «прокрутки нет вообще»");
    }

    [UnityTest]
    public IEnumerator EveryProjectWindow_KeepsItsContentAboveItsBottomEdge()
    {
        var canvas = NewCanvas();
        var windows = new List<(string name, IProjectWindow window)>();

        foreach (var type in AllWindowTypes())
        {
            var build = BuildMethod(type);
            if (build == null) continue;
            var window = (IProjectWindow)_canvasGo!.AddComponent(type);
            build.Invoke(window, new object[] { canvas });
            window.SetVisible(true);
            windows.Add((type.Name, window));
        }

        yield return null;
        yield return null;

        var offenders = new List<string>();
        foreach (var (name, window) in windows)
        {
            var rect = window.WindowRect;
            if (rect != null) CollectOverflow(name, rect, offenders);
        }

        Assert.IsEmpty(offenders,
            "содержимое окна ушло ниже видимой области, и никто бы об этом не узнал — "
            + "именно так дважды случилось: каталог сайдбара и вкладки настроек. Лечится "
            + "переводом окна на WindowBody.Create + Fit(), а не увеличением высоты панели: "
            + "следующие пять параметров всё равно вылезут:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void WindowScan_SeesEveryProjectWindow_AndCanBuildEachOfThem()
    {
        var all = AllWindowTypes();
        var buildable = all.Where(t => BuildMethod(t) != null).ToList();

        Assert.GreaterOrEqual(all.Count, 6,
            "скан по IProjectWindow не нашёл окон — значит он смотрит не в ту сборку, "
            + "и зелёный сторож не проверяет НИЧЕГО. Так уже было с грепом по неверному пути");
        CollectionAssert.Contains(all, typeof(SettingsPanelUI),
            "окно настроек обязано попадать в обход: именно на нём жалоба и началась");
        CollectionAssert.AreEquivalent(all, buildable,
            "новое окно обязано строиться одним вызовом Build(Transform) — иначе сторож "
            + "молча его пропустит, и следующее переполнение снова пройдёт незамеченным. "
            + "Не подходит подпись — правьте сторожа вместе с окном, а не в обход: "
            + string.Join(", ", all.Except(buildable).Select(t => t.Name)));
    }
}
