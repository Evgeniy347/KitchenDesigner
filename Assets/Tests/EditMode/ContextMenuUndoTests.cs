using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Сквозная проверка обещания «правка в окне свойств = один шаг отмены».
///
/// Тест ходит не по списку полей, выписанному руками, а по РЕАЛЬНЫМ полям
/// панели: они находятся отражением. Появилась новая строка в окне свойств —
/// она сразу под проверкой, и забыть про откат нельзя.
///
/// Проверяются три вещи для каждого поля каждого типа элемента:
///   1. правка вообще что-то поменяла (иначе проверка ничего не стоит);
///   2. в стек лёг РОВНО ОДИН шаг (а не три и не ноль);
///   3. «Отменить» вернуло состояние поэлементно, «Повторить» — вернуло правку.
/// </summary>
public class ContextMenuUndoTests
{
    private GameObject? _root;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private ProjectLoadStateGuard? _guard;

    // Поля позиции/поворота правит та же кнопка «Применить», но их состояние
    // живёт в трансформе, а не в свойствах — сверяем отдельно (см. StateOf).
    private static readonly string[] TransformFields =
        { "_x", "_y", "_z", "_rx", "_ry", "_rz" };

    private bool _blockOnViolationBefore;

    [SetUp]
    public void SetUp()
    {
        _guard = ProjectLoadStateGuard.Capture();
        // Одинокая деталь в пустой сцене — сама по себе нарушение (нет опоры),
        // а с включённой блокировкой применение молча откатывает размер и
        // позицию и НИЧЕГО не кладёт в стек. Проверяем механику отмены, а не
        // валидацию, поэтому блокировку снимаем.
        _blockOnViolationBefore = KitchenSettings.Instance.BlockOnViolation;
        KitchenSettings.Instance.BlockOnViolation = false;
        _root = new GameObject("UndoTestRoot");
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
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            UnityEngine.Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
    }

    // ── доступ к приватным потрохам панели ──────────────────────────────

    private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    private static IEnumerable<(string name, TMP_InputField field)> InputFields(ContextMenuUI ctx) =>
        typeof(ContextMenuUI)
            .GetFields(Priv)
            .Where(f => f.FieldType == typeof(TMP_InputField))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => (f.Name, (TMP_InputField?)f.GetValue(ctx)))
            .Where(p => p.Item2 != null)
            .Select(p => (p.Name, p.Item2!));

    private static void InvokeApply(ContextMenuUI ctx) =>
        typeof(ContextMenuUI).GetMethod("Apply", Priv)!.Invoke(ctx, null);

    // ── состояние элемента для сравнения до/после ───────────────────────

    private sealed class State
    {
        public List<object?> Props = new List<object?>();
        public Vector3 Pos;
        public Quaternion Rot;
        public string Text = "";
    }

    private static State StateOf(KitchenElement el)
    {
        var bag = UndoableProperties.Capture(el);
        var s = new State { Pos = el.transform.position, Rot = el.transform.rotation };
        var parts = new List<string>();
        for (int i = 0; i < bag.Count; i++)
        {
            s.Props.Add(bag.ValueAt(i));
            parts.Add($"{bag.PropertyAt(i).Name}={bag.ValueAt(i)}");
        }
        parts.Add($"pos={s.Pos:F4}");
        parts.Add($"rot={s.Rot.eulerAngles:F2}");
        s.Text = string.Join(", ", parts);
        return s;
    }

    private static bool Same(State a, State b)
    {
        if (a.Props.Count != b.Props.Count) return false;
        for (int i = 0; i < a.Props.Count; i++)
            if (!Equals(a.Props[i], b.Props[i])) return false;
        return (a.Pos - b.Pos).sqrMagnitude < 1e-8f && Quaternion.Angle(a.Rot, b.Rot) < 0.01f;
    }

    // ── подопытные ──────────────────────────────────────────────────────

    private static IEnumerable<Type> ElementTypes() =>
        typeof(KitchenElement).Assembly.GetTypes()
            .Where(t => typeof(KitchenElement).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private KitchenElement Spawn(Type type)
    {
        var go = new GameObject("U_" + type.Name);
        _spawned.Add(go);
        var el = (KitchenElement)go.AddComponent(type);
        el.PartName = "U_" + type.Name;
        el.DimensionsMM = new Vector3Int(600, 700, 18);
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

    // TMP_InputField хранит служебный zero-width space — он ломает разбор числа.
    private static string Clean(string s) => s.Replace("\u200b", "").Trim();

    /// <summary>Новое значение для поля: число +40, иначе пропуск. 40 мм заведомо
    /// больше шага клампов, но не выбивает за разумные пределы.</summary>
    private static string? BumpedText(TMP_InputField field)
    {
        var text = Clean(field.text);
        if (text.Length == 0) return null;
        if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            return null;
        var bumped = value + 40f;
        return bumped.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    // ── тесты ───────────────────────────────────────────────────────────

    /// <summary>Конкретный случай из отчёта: глубина варочной 550 → 500 и «Отменить».</summary>
    [Test]
    public void Cooktop_DepthEdit_IsUndoneInOneStep()
    {
        var ctx = BuildMenu();
        var go = new GameObject("Cooktop");
        _spawned.Add(go);
        var cooktop = go.AddComponent<CooktopElement>();
        cooktop.PartName = "Varochnaya";
        cooktop.DimensionsMM = new Vector3Int(
            CooktopElement.DEFAULT_WIDTH_MM, CooktopElement.DEFAULT_HEIGHT_MM, 550);
        PartRegistry.Register(cooktop);

        ctx.Open(cooktop);
        CommandStack.Clear();

        var depth = (TMP_InputField)typeof(ContextMenuUI).GetField("_d", Priv)!.GetValue(ctx)!;
        Assert.AreEqual("550", Clean(depth.text), "поле «Глубина» должно показывать текущую глубину");

        depth.text = "500";
        InvokeApply(ctx);

        Assert.AreEqual(500, cooktop.DimensionsMM.z, "правка обязана примениться");
        Assert.AreEqual(1, CommandStack.UndoCount, "одна правка — ровно один шаг отмены");

        CommandStack.Undo();
        Assert.AreEqual(550, cooktop.DimensionsMM.z, "«Отменить» обязано вернуть 550");

        CommandStack.Redo();
        Assert.AreEqual(500, cooktop.DimensionsMM.z, "«Повторить» обязано вернуть 500");
    }

    /// <summary>Каждое числовое поле панели у каждого типа элемента.</summary>
    [Test]
    public void EveryPanelField_EditIsUndoable()
    {
        var failures = new List<string>();
        int checkedFields = 0;

        foreach (var type in ElementTypes())
        {
            KitchenElement el;
            ContextMenuUI ctx;
            try
            {
                ctx = BuildMenu();
                el = Spawn(type);
                ctx.Open(el);
            }
            catch (Exception e)
            {
                failures.Add($"{type.Name}: окно свойств не открылось ({e.GetType().Name}: {e.Message})");
                continue;
            }

            foreach (var (name, field) in InputFields(ctx))
            {
                if (name == "_name") continue;                       // имя правится отдельным тестом
                if (!field.gameObject.activeInHierarchy) continue;   // строка скрыта для этого типа
                if (TransformFields.Contains(name)) continue;        // проверяются в PositionField_EditIsUndoable

                var bumped = BumpedText(field);
                if (bumped == null) continue;

                CommandStack.Clear();
                var before = StateOf(el);
                var originalText = Clean(field.text);

                field.text = bumped;
                try { InvokeApply(ctx); }
                catch (Exception e)
                {
                    failures.Add($"{type.Name}.{name}: применение упало ({e.GetType().Name}: {e.Message})");
                    continue;
                }

                var after = StateOf(el);
                if (Same(before, after))
                    continue;   // значение склампилось обратно — правки не было, проверять нечего

                checkedFields++;

                if (CommandStack.UndoCount != 1)
                    failures.Add($"{type.Name}.{name} ({originalText} → {bumped}): "
                                 + $"шагов отмены {CommandStack.UndoCount}, а должен быть 1");

                if (!CommandStack.CanUndo)
                {
                    failures.Add($"{type.Name}.{name} ({originalText} → {bumped}): "
                                 + "правка применилась, но отменить её нечем");
                    continue;
                }

                CommandStack.Undo();
                var undone = StateOf(el);
                if (!Same(before, undone))
                    failures.Add($"{type.Name}.{name} ({originalText} → {bumped}): отмена не вернула состояние\n"
                                 + $"    было:  {before.Text}\n    стало: {undone.Text}");

                CommandStack.Redo();
                var redone = StateOf(el);
                if (!Same(after, redone))
                    failures.Add($"{type.Name}.{name} ({originalText} → {bumped}): повтор не вернул правку\n"
                                 + $"    ждали: {after.Text}\n    вышло: {redone.Text}");

                CommandStack.Undo();   // вернуть элемент к исходному для следующего поля
            }
        }

        Assert.IsEmpty(failures, "\n" + string.Join("\n", failures));
        Assert.Greater(checkedFields, 10,
            $"проверено всего {checkedFields} полей — панель не открылась или поля не нашлись, "
            + "тест не может провалиться и потому бесполезен");
    }

    [Test]
    public void PositionField_EditIsUndoable()
    {
        var ctx = BuildMenu();
        var el = Spawn(typeof(KitchenElement));
        ctx.Open(el);
        CommandStack.Clear();

        el.transform.position = Vector3.zero;
        ctx.Open(el);   // перечитать поля под обнулённую позицию
        CommandStack.Clear();

        var before = StateOf(el);
        var x = (TMP_InputField)typeof(ContextMenuUI).GetField("_x", Priv)!.GetValue(ctx)!;
        x.text = "250";
        InvokeApply(ctx);

        Assert.AreEqual(0.25f, el.transform.position.x, 1e-4f,
            $"250 мм должны примениться (в поле было «{Clean(x.text)}»)");
        Assert.IsFalse(Same(before, StateOf(el)), "позиция обязана измениться");
        Assert.AreEqual(1, CommandStack.UndoCount, "одна правка — один шаг отмены");

        CommandStack.Undo();
        Assert.IsTrue(Same(before, StateOf(el)), "«Отменить» обязано вернуть позицию");
    }

    [Test]
    public void RenameField_EditIsUndoable()
    {
        var ctx = BuildMenu();
        var el = Spawn(typeof(KitchenElement));
        ctx.Open(el);
        CommandStack.Clear();

        var nameField = (TMP_InputField)typeof(ContextMenuUI).GetField("_name", Priv)!.GetValue(ctx)!;
        // Имя транслитерируется на входе (DrawerLinks.Rename), поэтому
        // сравниваем с латиницей — иначе тест проверял бы транслитерацию.
        nameField.text = "Renamed_Board";
        InvokeApply(ctx);

        Assert.AreEqual("Renamed_Board", Clean(el.PartName), "переименование обязано примениться");
        Assert.AreEqual(1, CommandStack.UndoCount, "одна правка — один шаг отмены");

        CommandStack.Undo();
        Assert.AreEqual("U_KitchenElement", el.PartName, "«Отменить» обязано вернуть имя");
    }
}
