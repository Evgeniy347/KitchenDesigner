using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

// docs/UI-GUIDELINES.md D4: каждая пара «текст/контур — фон» из таблицы токенов держит свой
// порог WCAG 2.1 — 4,5:1 для текста, 3:1 для границы контрола и фокуса (1.4.11). JSON-голдены
// цвета не видят (там type/text/position), поэтому у палитры свой сторож: до переделки белый на
// Accent давал 3,4:1, «Ошибка» HighlightError на окне — 4,0:1, рамка поля к окну — 1,1:1, и
// ни один тест этого не замечал.
public class UiContrastTests
{
    private readonly struct Pair
    {
        public readonly string Name;
        public readonly Color Fore;
        public readonly Color Back;
        public readonly float Min;

        public Pair(string name, Color fore, Color back, float min)
        {
            Name = name;
            Fore = fore;
            Back = back;
            Min = min;
        }
    }

    private const float Text = WcagContrast.TextAA;
    private const float Stroke = WcagContrast.NonTextAA;

    private static IEnumerable<Pair> TokenPairs()
    {
        yield return new Pair("Text / Panel", UIStyle.Text, UIStyle.Panel, Text);
        yield return new Pair("TextSecondary / Panel", UIStyle.TextSecondary, UIStyle.Panel, Text);
        yield return new Pair("TextDisabled / Panel", UIStyle.TextDisabled, UIStyle.Panel, Text);
        yield return new Pair("Text / NavBg", UIStyle.Text, UIStyle.NavBg, Text);
        yield return new Pair("TextSecondary / NavBg", UIStyle.TextSecondary, UIStyle.NavBg, Text);
        yield return new Pair("Text / Surface", UIStyle.Text, UIStyle.Surface, Text);
        yield return new Pair("TextSecondary / Surface", UIStyle.TextSecondary, UIStyle.Surface, Text);
        yield return new Pair("Text / SurfaceHover", UIStyle.Text, UIStyle.SurfaceHover, Text);
        yield return new Pair("Text / SurfaceActive", UIStyle.Text, UIStyle.SurfaceActive, Text);
        yield return new Pair("TextDisabled / SurfaceInactive (выключенная кнопка: WCAG 1.4.3 освобождает неактивные контролы, но §9 требует читаемости — 3:1)",
            UIStyle.TextDisabled, UIStyle.SurfaceInactive, Stroke);
        yield return new Pair("Text / Field", UIStyle.Text, UIStyle.Field, Text);
        yield return new Pair("TextSecondary / Field", UIStyle.TextSecondary, UIStyle.Field, Text);
        yield return new Pair("TextOnAccent / Accent", UIStyle.TextOnAccent, UIStyle.Accent, Text);
        yield return new Pair("TextOnAccent / AccentHover", UIStyle.TextOnAccent, UIStyle.AccentHover, Text);
        yield return new Pair("TextOnAccent / Danger", UIStyle.TextOnAccent, UIStyle.Danger, Text);
        yield return new Pair("AccentText / Panel", UIStyle.AccentText, UIStyle.Panel, Text);
        yield return new Pair("AccentText / AccentSubtle", UIStyle.AccentText, UIStyle.AccentSubtle, Text);
        yield return new Pair("Text / AccentSubtle", UIStyle.Text, UIStyle.AccentSubtle, Text);
        yield return new Pair("DangerText / Panel", UIStyle.DangerText, UIStyle.Panel, Text);
        yield return new Pair("TextError / Panel", UIStyle.TextError, UIStyle.Panel, Text);
        yield return new Pair("TextError / NavBg", UIStyle.TextError, UIStyle.NavBg, Text);
        yield return new Pair("TextWarning / Panel", UIStyle.TextWarning, UIStyle.Panel, Text);
        yield return new Pair("TextSuccess / Panel", UIStyle.TextSuccess, UIStyle.Panel, Text);
        yield return new Pair("Text / RowHover", UIStyle.Text, UIStyle.RowHover, Text);
        yield return new Pair("Text / RowSelected", UIStyle.Text, UIStyle.RowSelected, Text);
        yield return new Pair("FieldStroke / Field", UIStyle.FieldStroke, UIStyle.Field, Stroke);
        yield return new Pair("FieldStroke / Panel", UIStyle.FieldStroke, UIStyle.Panel, Stroke);
        yield return new Pair("FocusRing / Panel", UIStyle.FocusRing, UIStyle.Panel, Stroke);
        yield return new Pair("FocusRing / Field", UIStyle.FocusRing, UIStyle.Field, Stroke);
        yield return new Pair("SelectionBar / Panel", UIStyle.SelectionBar, UIStyle.Panel, Stroke);
    }

    [Test]
    public void EveryTokenPairOfTheD4Table_MeetsItsWcagThreshold()
    {
        var failing = TokenPairs()
            .Select(p => (p, ratio: WcagContrast.Ratio(p.Fore, p.Back)))
            .Where(x => x.ratio < x.p.Min)
            .Select(x => $"{x.p.Name}: {x.ratio:0.00}:1 < {x.p.Min:0.0}:1")
            .ToList();

        Assert.IsEmpty(failing,
            "Пара токенов ниже порога WCAG 2.1 (текст 4,5:1, граница контрола и фокус 3:1). "
            + "Подними контраст в UIStyle, а не опускай порог — таблица D4 и есть договорённость:\n"
            + string.Join("\n", failing));
    }

    [Test]
    public void Windows_AreOpaque()
    {
        Assert.AreEqual(1f, UIStyle.Panel.a, 1e-4f,
            "Окно непрозрачно (D4): Panel α 0,92 пропускал сцену, и контраст текста зависел от "
            + "того, что лежит под окном — светлая столешница съедала вторичный текст");
        Assert.AreEqual(1f, UIStyle.NavBg.a, 1e-4f);
    }

    [Test]
    public void ThePairTable_CoversEveryForegroundTextToken()
    {
        var foregrounds = TokenPairs().Select(p => p.Fore).ToList();
        foreach (var (name, token) in new[]
                 {
                     ("Text", UIStyle.Text), ("TextSecondary", UIStyle.TextSecondary),
                     ("TextDisabled", UIStyle.TextDisabled), ("TextError", UIStyle.TextError),
                     ("TextWarning", UIStyle.TextWarning), ("TextSuccess", UIStyle.TextSuccess),
                     ("AccentText", UIStyle.AccentText), ("DangerText", UIStyle.DangerText),
                     ("TextOnAccent", UIStyle.TextOnAccent), ("FieldStroke", UIStyle.FieldStroke),
                     ("FocusRing", UIStyle.FocusRing),
                 })
            CollectionAssert.Contains(foregrounds, token,
                name + " не проверен ни на одном фоне: новый текстовый токен без пары — это "
                + "непроверенный контраст");
    }

    [Test]
    public void TheGuard_CatchesTheOldAccentPair()
    {
        var oldAccent = new Color(0.30f, 0.50f, 0.75f, 1f);
        Assert.Less(WcagContrast.Ratio(UIStyle.Text, oldAccent), Text,
            "сторож обязан краснеть на прежней паре «текст на Accent» (3,4:1) — иначе он "
            + "ничего не меряет");
    }
}
