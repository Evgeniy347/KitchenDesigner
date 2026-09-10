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
/// Сторож обещания «правка в окне свойств трубопровода = один шаг отмены» для тех строк,
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
/// найденные в её иерархии, и типы элементов трубопровода, найденные отражением. Новый вид
/// фитинга и новая строка выбора попадают под проверку сами, без правки этого файла.
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
/// </summary>
public class PipePanelChoiceUndoGuardTests
{
    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;
    private bool _blockOnViolationBefore;
    private bool _ignoreLogsBefore;

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
        UIFactory.EnsureEventSystem();
        _root = new GameObject("PipeChoiceUndoRoot");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        KitchenSettings.Instance.BlockOnViolation = _blockOnViolationBefore;
        _guard?.Restore();
        ClearScene();
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
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

    /// <summary>Виды трубопровода берутся из сборки, а не выписаны руками: новый класс
    /// фитинга попадает под сторожа сам.</summary>
    private static IEnumerable<Type> PlumbingTypes() =>
        typeof(KitchenElement).Assembly.GetTypes()
            .Where(t => !t.IsAbstract
                        && (typeof(PipeFittingElement).IsAssignableFrom(t)
                            || typeof(PipeElement).IsAssignableFrom(t)))
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private KitchenElement Spawn(Type type)
    {
        var go = new GameObject("P_" + type.Name);
        _spawned.Add(go);
        var el = (KitchenElement)go.AddComponent(type);
        el.PartName = "P_" + type.Name;
        PartRegistry.Register(el);
        return el;
    }

    private ContextMenuUI BuildMenu()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();
        ctx.Build(_root!.transform);
        return ctx;
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
    public void EveryChoiceRowOfThePlumbingPanel_IsUndoableInOneStep()
    {
        var failures = new List<string>();
        int checkedControls = 0;
        var ctx = BuildMenu();

        foreach (var type in PlumbingTypes())
        {
            KitchenElement el;
            try
            {
                el = Spawn(type);
                ctx.Open(el);
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
                if (el == null) break;

                CommandStack.Clear();
                var before = StateOf(el);
                var what = Describe(control);
                if (!Pick(control)) continue;

                var after = StateOf(el);
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
                var undone = StateOf(el);
                if (!Same(before, undone))
                    failures.Add($"{type.Name}: {what} — отмена не вернула состояние\n"
                                 + $"    было:  {before.Text}\n    стало: {undone.Text}");

                CommandStack.Redo();
                var redone = StateOf(el);
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
            + "SetPropertiesCommand её уже не поймает — образец в "
            + "PipeFieldsEditor.OnSizeSelected и BedFieldsEditor.Commit.\n"
            + string.Join("\n", failures));
        Assert.Greater(checkedControls, 1,
            $"проверено всего {checkedControls} строк выбора — панель не открылась или списки "
            + "не нашлись, и тогда этот тест не может провалиться и потому бесполезен");
    }

    /// <summary>Отдельно — тот самый путь из отчёта пользователя, чтобы регрессия читалась
    /// именем, а не строкой в списке отказов сводного перебора.</summary>
    [Test]
    public void PipeNominalBoreChoice_IsUndoneInOneStep()
    {
        var ctx = BuildMenu();
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
