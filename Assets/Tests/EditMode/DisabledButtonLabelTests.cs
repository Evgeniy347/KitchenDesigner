using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Стражи правила «выключенная кнопка гаснет ЦЕЛИКОМ — и фон, и
/// подпись» (docs/UI-GUIDELINES.md §9). Unity красит своим ColorBlock только
/// фон кнопки; до текста и до иконки он не достаёт, и яркая подпись на мёртвой
/// кнопке читается как «сюда можно нажать» — та же болезнь, что была у строк
/// ввода.
///
/// Правило записано МЕХАНИЗМОМ, а не перечнем кнопок: подписи гасит UIButton,
/// который вешает UIFactory, поэтому сторож перебирает ВСЕ кнопки собранной
/// панели по всем типам элементов и выключает каждую сам. Кнопка, добавленная
/// завтра, попадёт под проверку без единой правки здесь; что фабрику никто не
/// обходит, отдельно стережёт UiButtonFactoryTests.</summary>
public class DisabledButtonLabelTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;

    private readonly List<GameObject> _probes = new List<GameObject>();

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню —
    /// 0,31 с, и четыре сборки это 1,3 с прогона EditMode при бюджете 170.
    /// Боевой сценарий — это и есть ОДНА панель, переоткрываемая через
    /// <c>Open</c>; полный разбор того, что <c>Open</c> сбрасывает, — в сводке
    /// <see cref="ContextMenuLayoutTests"/>. Оба сторожа-перебора открывают
    /// панель на каждом типе элемента сами и сами же зовут <c>Close</c>, а два
    /// теста фабрики панель не читают вовсе: они собирают СВОЮ кнопку на том же
    /// холсте и спрашивают её подпись, поэтому общая панель им безразлична.
    /// Кнопки-пробы уносятся в <c>[TearDown]</c> — холст теперь переживает тест,
    /// и оставленная проба досталась бы следующему.</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    /// <summary>Панель переживает тест — значит потестовое состояние вокруг неё
    /// возвращается на место здесь: фокус (<c>RefreshUnfocused</c> не трогает
    /// сфокусированное поле) и окно склейки правок (в EditMode
    /// <c>Time.frameCount</c> стоит на месте).</summary>
    [SetUp]
    public void Setup()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
    }

    /// <summary>Панель закрывается ДО уничтожения элементов сцены: <c>Close</c>
    /// обнуляет <c>_target</c>, иначе панель осталась бы с уничтоженной деталью
    /// в руках, а взведённая кнопка «Удалить» — взведённой на следующий
    /// тест.</summary>
    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        foreach (var go in _probes)
            if (go != null) Object.DestroyImmediate(go);
        _probes.Clear();
        EveryElementType.ClearScene();
    }

    [Test]
    public void FactoryButton_TurnedOff_DimsItsCaption_AndBrightensItBack()
    {
        var button = UIFactory.CreateButton("Проба", _canvas!.transform, "Нажми",
            Vector2.zero, new Vector2(100, 32), null);
        _probes.Add(button.gameObject);
        var caption = button.GetComponentInChildren<TMP_Text>();
        var bright = caption.color;

        Assume.That(bright, Is.Not.EqualTo(UIStyle.TextDisabled),
            "включённая кнопка обязана начинать с яркой подписи, иначе проверка ничего не значит");

        button.interactable = false;
        Assert.AreEqual(UIStyle.TextDisabled, caption.color,
            "выключенная кнопка обязана погасить подпись: ColorBlock красит только фон, "
            + "и человек по яркому тексту продолжает жать в мёртвую кнопку");

        button.interactable = true;
        Assert.AreEqual(bright, caption.color,
            "и вернуть её обратно: гашение, из которого нет пути назад, оставит кнопку "
            + "выглядящей сломанной после первого же выключения (парный контроль)");
    }

    [Test]
    public void FactoryIconButton_TurnedOff_DimsItsIcon()
    {
        var button = UIFactory.CreateIconButton("ПробаИконка", _canvas!.transform,
            IconFactory.Undo, Vector2.zero, new Vector2(32, 32), () => { });
        _probes.Add(button.gameObject);
        var icon = button.transform.Find("ПробаИконка_Icon")!.GetComponent<Image>();
        var bright = icon.color;

        Assume.That(bright, Is.Not.EqualTo(UIStyle.TextDisabled),
            "включённая кнопка обязана начинать с яркой иконкой");

        button.interactable = false;
        Assert.AreEqual(UIStyle.TextDisabled, icon.color,
            "у кнопки тулбара подпись — это иконка: «отменить» без отмены обязано выглядеть "
            + "недоступным так же, как текстовая кнопка");

        button.interactable = true;
        Assert.AreEqual(bright, icon.color, "и вернуться, когда отменять снова есть что");
    }

    [Test]
    public void EveryElementType_EveryButtonInThePanel_DimsItsCaptionWhenTurnedOff()
    {
        var offenders = new List<string>();
        int checkedButtons = 0;

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);

            foreach (var button in Panel().GetComponentsInChildren<Button>(true))
            {
                var caption = button.GetComponentInChildren<TMP_Text>(true);
                if (caption == null) continue;
                bool was = button.interactable;

                button.interactable = false;
                checkedButtons++;
                if (caption.color != UIStyle.TextDisabled)
                    offenders.Add($"{type.Name}: {button.name} — «{caption.text}»");
                button.interactable = was;
            }

            _menu!.Close();
            EveryElementType.ClearScene();
        }

        Assert.Greater(checkedButtons, 0,
            "сторож обязан найти в панели кнопки с подписью: пустой перебор зеленеет, "
            + "ничего не проверив");
        Assert.IsEmpty(offenders,
            "Правило: выключенная кнопка гаснет целиком — и фон, и подпись "
            + "(docs/UI-GUIDELINES.md §9). Красить подпись руками не надо: это делает "
            + "UIButton, который вешает UIFactory.CreateButton / CreateIconButton. Кнопка "
            + "попала сюда — значит её собрали мимо фабрики, и её подпись останется яркой "
            + "у нерабочей кнопки. Собирайте кнопку фабрикой. Нарушители: "
            + string.Join(" | ", offenders));
    }

    [Test]
    public void EveryElementType_EveryWorkingButton_KeepsItsCaptionBright()
    {
        var offenders = new List<string>();

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);

            foreach (var button in Panel().GetComponentsInChildren<Button>(false))
            {
                if (!button.interactable) continue;
                var caption = button.GetComponentInChildren<TMP_Text>(true);
                if (caption == null) continue;
                if (caption.color != UIStyle.TextDisabled) continue;
                offenders.Add($"{type.Name}: {button.name} — «{caption.text}»");
            }

            _menu!.Close();
            EveryElementType.ClearScene();
        }

        Assert.IsEmpty(offenders,
            "Парный контроль: если гаснет и рабочая кнопка, «погашено» перестаёт что-либо "
            + "значить — человек не отличит нерабочую кнопку от рабочей. Гасить обязано "
            + "ровно выключение. Кнопки, погашенные зря: " + string.Join(" | ", offenders));
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;
}
