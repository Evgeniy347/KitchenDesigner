using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Сторож обещания «правка в окне свойств = один шаг отмены» для тех строк,
/// которые <c>ContextMenuUndoTests</c> не видит вовсе.
///
/// Тот тест перебирает <c>TMP_InputField</c> панели и жмёт «Применить»: такие поля
/// отменяются даром, потому что <c>ContextMenuUI.Apply</c> оборачивает всё сразу в
/// <c>BeginCapture/EndCapture</c> и добавляет диф <c>SetPropertiesCommand</c>. Выпадающие
/// списки и переключатели работают ИНАЧЕ: их обратный вызов меняет элемент немедленно, вне
/// <c>Apply</c>, и к моменту «Применить» снимок «до» уже содержит НОВОЕ значение — диф пуст,
/// в стек не ложится ничего, и отмена молча не делает ничего. Ровно так и потерялся
/// «Условный проход» трубы: строка работала, отмена — нет.
///
/// Поэтому здесь перебираются не поля, а РЕАЛЬНЫЕ выпадающие списки и переключатели панели,
/// найденные в её иерархии, и КАЖДЫЙ объявленный тип элемента (<c>EveryElementType</c> —
/// тот же список типов и тот же настоящий спавн, что у общего прохода
/// <c>ElementSurfaceSweep</c>; своего перебора типов здесь не заводится). Новый тип элемента
/// и новая строка выбора попадают под проверку сами, без правки этого файла.
///
/// Перебор один на весь класс: спавн настоящей фабрикой и построение панели — единственное,
/// что здесь дорого, а сами вопросы стоят микросекунды (`agents/TEST-DESIGN.md` → «Дорогой
/// перебор всех типов делается ОДИН раз»).
///
/// Проверяется на каждом управляющем элементе:
///   1. выбор вообще что-то поменял (иначе проверять нечего — строка ничего не правит,
///      как списки свёрнутой секции текстур: они лишь готовят «Добавить»);
///   2. в стек лёг РОВНО ОДИН шаг;
///   3. «Отменить» вернуло состояние, «Повторить» — вернуло правку.
///
/// «Состояние» здесь шире набора <c>[Undoable]</c>-свойств: декор помечен
/// <c>[NotUndoable]</c> и живёт в <c>MaterialId</c>, а выбор детали на порту вообще не меняет
/// свойств хозяина — он ДОБАВЛЯЕТ элемент в сцену. Без этих двух слагаемых сторож молча
/// пропускал бы обе строки, то есть не мог бы провалиться там, где как раз и надо.
///
/// Диаметры фитинга под сторожа не попадают, и это правильно: они ВЫВОДИМЫЕ, их строки
/// нередактируемые, единственный писатель — <c>PipeFittingSizeLink</c>.
///
/// Строки из <see cref="KnownGaps"/> перебор ПРОПУСКАЕТ — это признанный долг, а не тишина:
/// каждая названа поимённо вместе с владельцем, и <see cref="EveryKnownGap_IsStillAGap"/>
/// краснеет, когда долг закрыли, а из списка не убрали. Всякая ДРУГАЯ строка выбора обязана
/// класть команду сама — список не растёт молча.
/// </summary>
public class PipePanelChoiceUndoGuardTests
{
    private GameObject? _root;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;
    private bool _blockOnViolationBefore;
    private bool _ignoreLogsBefore;

    /// <summary>Холст и панель строятся ОДИН раз на класс: сборка контекстного меню —
    /// ~0,31 с, и пять сборок это полторы секунды прогона EditMode за панель, которую
    /// продукт собирает единожды и дальше только переоткрывает
    /// (<see cref="ContextMenuLayoutTests"/>). Через <c>Open</c> здесь проходит каждый
    /// тест, а <c>Open</c> и есть полный сброс видимого состояния панели.
    ///
    /// На потестовом холсте не осталось ничего потестового: <c>_root</c> нёс только
    /// холст и саму панель. Своего <c>SelectionManager</c> класс не заводит.</summary>
    [OneTimeSetUp]
    public void BuildTheCanvasAndPanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _root = new GameObject("PipeChoiceUndoRoot");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();
        BuildMenu();
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        _menu = null;
        _root = null;
    }

    /// <summary>Панель переживает тест — значит потестовое состояние сбрасывается здесь.
    /// <c>ForgetLastApplyFrame</c> — окно склейки правок: в EditMode
    /// <c>Time.frameCount</c> стоит на месте, и окно, взведённое предыдущим тестом,
    /// съело бы первую правку следующего. <c>DisarmAll</c> снимает взвод кнопок
    /// удаления, а фокус — потому что <c>RefreshUnfocused</c> МОЛЧА пропускает
    /// сфокусированное поле, а <c>EventSystem</c> в EditMode один на весь прогон.</summary>
    [SetUp]
    public void SetUp()
    {
        // Строка декора строит материалы, а сборка мусора материалов в EditMode шумит
        // «leaked objects» — тот же приём, что в MaterialPreviewTests.
        _ignoreLogsBefore = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        _guard = ProjectLoadStateGuard.Capture();
        // Одинокая труба в пустой сцене — сама по себе нарушение (нет опоры), а с включённой
        // блокировкой применение молча откатывается и в стек не ложится ничего. Проверяем
        // механику отмены, а не валидацию.
        _blockOnViolationBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
        CommandStack.Clear();
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.SetSelectedGameObject(null);
    }

    /// <summary><c>Close()</c> обязан идти ДО <c>ClearScene</c>: он обнуляет <c>_target</c>
    /// панели, иначе живая панель уехала бы в следующий тест с уничтоженной деталью в
    /// руках.</summary>
    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        KitchenSettings.Instance.BlockOnViolation = _blockOnViolationBefore;
        _guard?.Restore();
        ClearScene();
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        LogAssert.ignoreFailingMessages = _ignoreLogsBefore;
    }

    /// <summary>Выбор на порту СОЗДАЁТ соседа — он тоже подопытный, и убрать надо и его.
    /// Между типами сцена чистится целиком: все подопытные встают в одну точку, а две детали
    /// в одной точке трубопровода считаются соединёнными, и следующий тип получал бы уже
    /// занятый порт.</summary>
    private void ClearScene()
    {
        foreach (var element in PartRegistry.GetAll())
            if (element != null) _spawned.Add(element.gameObject);
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            UnityEngine.Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        PartRegistry.Clear();
    }

    /// <summary>Признанный долг: строки выбора, которые состояние МЕНЯЮТ, а команду не
    /// кладут, и чинятся не здесь. Имя узла — ключ, значение — кто владелец и что не так.
    /// Список закрытый и убывающий: <see cref="EveryKnownGap_IsStillAGap"/> краснеет, когда
    /// строку починили, а из списка забыли убрать, а всякая НЕназванная строка обязана
    /// класть команду сама.</summary>
    private static readonly Dictionary<string, string> KnownGaps =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Типы берутся у общего перебора <c>EveryElementType</c>, а не выписаны руками
    /// и не выведены своим отражением: новый тип элемента попадает под сторожа сам, и второго
    /// списка типов в наборе не заводится.</summary>
    private static IEnumerable<Type> SweptTypes() => EveryElementType.Declared();

    /// <summary>Подопытный адресуется ОБЪЕКТОМ сцены, а компонент перечитывается после
    /// каждого шага: строка «Тип» пересобирает элемент — уничтожает компонент и вешает на
    /// тот же <c>GameObject</c> другой, — поэтому сохранённая ссылка на <c>KitchenElement</c>
    /// после неё мертва. Ровно так же адресует деталь и сама <c>ConvertElementCommand</c>.</summary>
    private static KitchenElement? Live(GameObject go) =>
        go != null ? go.GetComponent<KitchenElement>() : null;

    private KitchenElement Spawn(Type type)
    {
        var el = EveryElementType.Spawn(type, "P_" + type.Name);
        _spawned.Add(el.gameObject);
        return el;
    }

    /// <summary>Единственный, кто зовёт <c>Build</c>: [OneTimeSetUp] и путь «Open упал» в
    /// <see cref="EveryChoiceRowOfThePropertiesPanel_IsUndoableInOneStep"/>. Ставит панель
    /// в <c>_menu</c>, чтобы [OneTimeTearDown] снял ту, что жива сейчас.</summary>
    private ContextMenuUI BuildMenu()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_root!.transform);
        return _menu!;
    }

    private sealed class State
    {
        public List<object?> Props = new List<object?>();
        public string? MaterialId;
        public Vector3Int Dims;
        public Vector3 Pos;
        public Quaternion Rot;
        public int SceneCount;
        public string Text = "";
    }

    private static State StateOf(KitchenElement el)
    {
        var bag = UndoableProperties.Capture(el);
        var s = new State
        {
            MaterialId = el.MaterialId,
            Dims = el.DimensionsMM,
            Pos = el.transform.position,
            Rot = el.transform.rotation,
            SceneCount = PartRegistry.GetAll().Count,
        };
        var parts = new List<string>();
        for (int i = 0; i < bag.Count; i++)
        {
            s.Props.Add(bag.ValueAt(i));
            parts.Add($"{bag.PropertyAt(i).Name}={bag.ValueAt(i)}");
        }
        parts.Add($"декор={s.MaterialId}");
        parts.Add($"габарит={s.Dims}");
        parts.Add($"деталей в сцене={s.SceneCount}");
        s.Text = string.Join(", ", parts);
        return s;
    }

    private static bool Same(State a, State b)
    {
        if (a.Props.Count != b.Props.Count) return false;
        for (int i = 0; i < a.Props.Count; i++)
            if (!Equals(a.Props[i], b.Props[i])) return false;
        return a.MaterialId == b.MaterialId
               && a.Dims == b.Dims
               && a.SceneCount == b.SceneCount
               && (a.Pos - b.Pos).sqrMagnitude < 1e-8f
               && Quaternion.Angle(a.Rot, b.Rot) < 0.01f;
    }

    /// <summary>Управляющие элементы панели, до которых у пользователя есть руки: видимые и
    /// доступные. Внутренности самого списка (его шаблон строки — тоже <c>Toggle</c>) не
    /// считаются: их пользователь не переключает.</summary>
    private List<Component> ChoiceControls()
    {
        var found = new List<Component>();
        var panel = _root!.transform;

        foreach (var dropdown in panel.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if (dropdown == null || !dropdown.gameObject.activeInHierarchy) continue;
            if (!dropdown.IsInteractable()) continue;
            found.Add(dropdown);
        }

        foreach (var toggle in panel.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle == null || !toggle.gameObject.activeInHierarchy) continue;
            if (!toggle.IsInteractable()) continue;
            if (toggle.GetComponentInParent<TMP_Dropdown>() != null) continue;
            found.Add(toggle);
        }

        return found.OrderBy(c => c.gameObject.name, StringComparer.Ordinal).ToList();
    }

    private static TMP_Dropdown? DropdownNamed(GameObject root, string name)
    {
        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
            if (dropdown != null && dropdown.gameObject.name == name) return dropdown;
        return null;
    }

    private static int NextValue(TMP_Dropdown dd) => (dd.value + 1) % Math.Max(1, dd.options.Count);

    private static string Describe(Component control) =>
        control is TMP_Dropdown dd
            ? $"список «{control.gameObject.name}» ({dd.value} → {NextValue(dd)})"
            : $"переключатель «{control.gameObject.name}»";

    /// <summary>Дёргает управляющий элемент так, как это делает пользователь: через значение,
    /// чтобы сработал <c>onValueChanged</c>. Возвращает false, если дёргать нечего.</summary>
    private static bool Pick(Component control)
    {
        if (control is TMP_Dropdown dd)
        {
            if (dd.options.Count < 2) return false;
            dd.value = NextValue(dd);
            return true;
        }

        if (control is Toggle toggle)
        {
            toggle.isOn = !toggle.isOn;
            return true;
        }

        return false;
    }

    [Test]
    public void EveryChoiceRowOfThePropertiesPanel_IsUndoableInOneStep()
    {
        var failures = new List<string>();
        int checkedControls = 0;
        var ctx = _menu!;

        foreach (var type in SweptTypes())
        {
            GameObject subject;
            try
            {
                var spawned = Spawn(type);
                subject = spawned.gameObject;
                ctx.Open(spawned);
            }
            catch (Exception e)
            {
                failures.Add($"{type.Name}: окно свойств не открылось "
                             + $"({e.GetType().Name}: {e.Message})");
                UnityEngine.Object.DestroyImmediate(ctx.gameObject);
                ctx = BuildMenu();
                ClearScene();
                continue;
            }

            foreach (var control in ChoiceControls())
            {
                if (control == null || !control.gameObject.activeInHierarchy) continue;
                var live = Live(subject);
                if (live == null) break;
                if (KnownGaps.ContainsKey(control.gameObject.name)) continue;

                CommandStack.Clear();
                var before = StateOf(live);
                var what = Describe(control);
                bool picked;
                try
                {
                    picked = Pick(control);
                }
                catch (Exception e)
                {
                    failures.Add($"{type.Name}: {what} — выбор бросил исключение "
                                 + $"({e.GetType().Name}: {e.Message})");
                    while (CommandStack.CanUndo) CommandStack.Undo();
                    continue;
                }

                if (!picked) continue;

                var swapped = Live(subject);
                if (swapped == null)
                {
                    failures.Add($"{type.Name}: {what} — строка УНИЧТОЖИЛА подопытный объект "
                                 + "сцены целиком. Пересобрать КОМПОНЕНТ на том же объекте "
                                 + "можно (так работает строка «Тип»), уничтожить объект — "
                                 + "нельзя: после этого нечего ни отменять, ни проверять");
                    break;
                }

                var after = StateOf(swapped);
                // Строка ничего не правит (списки свёрнутой секции текстур лишь готовят
                // «Добавить») — стеречь тут нечего.
                if (Same(before, after)) continue;

                checkedControls++;

                if (CommandStack.UndoCount != 1)
                {
                    failures.Add($"{type.Name}: {what} — шагов отмены "
                                 + $"{CommandStack.UndoCount}, а должен быть 1\n"
                                 + $"    было:  {before.Text}\n    стало: {after.Text}");
                    while (CommandStack.CanUndo) CommandStack.Undo();
                    continue;
                }

                CommandStack.Undo();
                var undone = StateOf(Live(subject)!);
                if (!Same(before, undone))
                    failures.Add($"{type.Name}: {what} — отмена не вернула состояние\n"
                                 + $"    было:  {before.Text}\n    стало: {undone.Text}");

                CommandStack.Redo();
                var redone = StateOf(Live(subject)!);
                if (!Same(after, redone))
                    failures.Add($"{type.Name}: {what} — повтор не вернул правку\n"
                                 + $"    ждали: {after.Text}\n    вышло: {redone.Text}");

                CommandStack.Undo();
            }

            ctx.Close();
            CommandStack.Clear();
            ClearScene();
        }

        Assert.IsEmpty(failures,
            "\nСтрока выбора в окне свойств обязана сама положить команду в CommandStack: "
            + "её обратный вызов срабатывает ВНЕ ContextMenuUI.Apply, и диф "
            + "SetPropertiesCommand её уже не поймает — готовый помощник ChoiceRowUndo.Commit, "
            + "образцы в PipeFieldsEditor.OnSizeSelected и BedFieldsEditor.Commit.\n"
            + string.Join("\n", failures));
        Assert.Greater(checkedControls, 1,
            $"проверено всего {checkedControls} строк выбора — панель не открылась или списки "
            + "не нашлись, и тогда этот тест не может провалиться и потому бесполезен");
    }

    /// <summary>Список признанного долга обязан УБЫВАТЬ. Строку починили или убрали из
    /// панели, а из <see cref="KnownGaps"/> не вычеркнули — сводный перебор молча перестаёт
    /// её стеречь, и следующая поломка уже никого не разбудит.</summary>
    [Test]
    public void EveryKnownGap_IsStillAGap()
    {
        var stale = new List<string>();

        foreach (var gap in KnownGaps)
        {
            var ctx = _menu!;
            var el = Spawn(typeof(KitchenElement));
            ctx.Open(el);

            Component? control = ChoiceControls()
                .FirstOrDefault(c => c.gameObject.name == gap.Key);
            if (control is null)
            {
                stale.Add($"{gap.Key}: строки с таким именем в панели детали больше нет — "
                          + $"вычеркни её из KnownGaps ({gap.Value})");
            }
            else
            {
                CommandStack.Clear();
                var before = StateOf(el);
                if (!Pick(control))
                {
                    stale.Add($"{gap.Key}: строку нечем дёрнуть, значит она уже ничего не "
                              + $"меняет и долгом не является ({gap.Value})");
                }
                else if (el != null && !Same(before, StateOf(el)) && CommandStack.UndoCount == 1)
                {
                    stale.Add($"{gap.Key}: строка ПОЧИНЕНА — кладёт ровно один шаг отмены. "
                              + $"Убери её из KnownGaps, чтобы сводный перебор снова её "
                              + $"стерёг ({gap.Value})");
                }
            }

            while (CommandStack.CanUndo) CommandStack.Undo();
            ctx.Close();
            UnityEngine.Object.DestroyImmediate(ctx.gameObject);
            CommandStack.Clear();
            ClearScene();
        }

        Assert.IsEmpty(stale,
            "\nKnownGaps — закрытый и убывающий список признанного долга, а не свалка "
            + "исключений: пока имя лежит в нём, сводный перебор эту строку ПРОПУСКАЕТ.\n"
            + string.Join("\n", stale));
    }

    /// <summary>Строка «Тип» делает ДВА разных дела: у детали она пересобирает элемент
    /// (<c>ConvertElementCommand</c>, круг «конверсия → отмена → повтор» разобран попарно в
    /// <c>ElementTypeConversionUndoTests</c>), а у ящика — просто меняет систему. Обе половины
    /// обязаны отменяться; сводный перебор видит их обе, а эта — половина ящика — названа
    /// поимённо, чтобы регрессия читалась именем.</summary>
    [Test]
    public void DrawerSystemChoice_IsUndoneInOneStep()
    {
        var ctx = _menu!;
        var drawer = (DrawerElement)Spawn(typeof(DrawerElement));
        ctx.Open(drawer);
        CommandStack.Clear();

        var dropdown = DropdownNamed(_root!, "CtxType");
        Assert.NotNull(dropdown, "строка «Тип» обязана быть в панели ящика");
        Assume.That(dropdown!.options.Count, Is.GreaterThan(1),
            "в списке систем ящика обязано быть из чего выбирать");

        var was = drawer.System;
        dropdown.value = NextValue(dropdown);

        Assert.AreNotEqual(was, drawer.System, "выбор системы обязан примениться сразу");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "смена системы ящика — ровно один шаг отмены");

        var picked = drawer.System;
        CommandStack.Undo();
        Assert.AreEqual(was, drawer.System, "«Отменить» обязано вернуть прежнюю систему");

        CommandStack.Redo();
        Assert.AreEqual(picked, drawer.System, "«Повторить» обязано вернуть выбор");
    }

    /// <summary>Отдельно — тот самый путь из отчёта пользователя, чтобы регрессия читалась
    /// именем, а не строкой в списке отказов сводного перебора.</summary>
    [Test]
    public void PipeNominalBoreChoice_IsUndoneInOneStep()
    {
        var ctx = _menu!;
        var pipe = (PipeElement)Spawn(typeof(PipeElement));
        ctx.Open(pipe);
        CommandStack.Clear();

        var dropdown = DropdownNamed(_root!, "CtxPipeSize");
        Assert.NotNull(dropdown, "строка «Условный проход» обязана быть в панели трубы");
        Assume.That(dropdown!.options.Count, Is.GreaterThan(1),
            "в списке проходов обязано быть из чего выбирать");

        var was = pipe.SizeId;
        dropdown.value = NextValue(dropdown);

        Assert.AreNotEqual(was, pipe.SizeId, "выбор обязан примениться сразу");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "выбор условного прохода — ровно один шаг отмены");

        var picked = pipe.SizeId;
        CommandStack.Undo();
        Assert.AreEqual(was, pipe.SizeId, "«Отменить» обязано вернуть прежний проход");

        CommandStack.Redo();
        Assert.AreEqual(picked, pipe.SizeId, "«Повторить» обязано вернуть выбор");
    }
}
