using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// Сторож наполнения подсказок «i» — по образцу <c>SpecificationCoverageGuardTests</c>
/// (docs/UI-GUIDELINES.md §13, docs/todo_evolution.md §1.3/§3.7 п.1). Наполнение и
/// покрытие — разные вопросы: <c>HintTextGuardTests</c> спрашивает «у заявленного ключа
/// есть текст», этот сторож — «у КАЖДОГО числового поля/дропдауна `*FieldsEditor` и у
/// КАЖДОГО ключа `SettingKeys` заявлен ключ подсказки вообще» (правило из §1.3: «Подсказка
/// обязательна у каждого числового поля и дропдауна `*FieldsEditor` и у каждого ключа
/// `SettingKeys`; исключение — только с причиной»).
///
/// Скан работает по исходникам (<see cref="HintCoverageScan"/>), а не по построенной
/// сцене: `Rows.NumberField(...)` / `_rows.AddToggle(...)` без `hint:` текстуально
/// отличимы от того же вызова с `hint:`, и это дешевле, чем строить ~40 типов элементов
/// и все вкладки настроек ради того же ответа.
///
/// Признанный долг, как и у <c>PipePanelChoiceUndoGuardTests</c>, живёт в
/// <see cref="KnownGaps"/> — закрытом и УБЫВАЮЩЕМ списке с причиной у каждой записи;
/// <see cref="EveryKnownGap_IsStillAGap"/> краснеет, когда запись почищена, а из списка
/// не убрана.
/// </summary>
public class HintCoverageGuardTests
{
    /// <summary>Признанный долг. Каждая запись — «i», которую эта сессия не наполнила, с
    /// причиной. Список обязан УБЫВАТЬ: следующая порция наполнения (по вкладке/панели,
    /// один коммит на порцию — docs/todo_evolution.md §1.3) вычёркивает записи отсюда, а
    /// не добавляет новые вокруг них.</summary>
    private static readonly Dictionary<string, string> KnownGaps = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BedFieldsEditor.cs → Rows.Dropdown(HeadboardLabel…)"] = NextPortion,
        ["CooktopCutoutFieldsEditor.cs → Rows.NumberField(Глубина выреза…)"] = NextPortion,
        ["DrawerBoxFieldsEditor.cs → Rows.Dropdown(Цвет…)"] = NextPortion,
        ["DrawerBoxFieldsEditor.cs → Rows.NamedDropdown(CtxDrawerUpperLen…)"] = NextPortion,
        ["LightFieldsEditor.cs → Bind(Верхний радиус…)"] = NextPortion,
        ["LightFieldsEditor.cs → Bind(Радиус при 100 %…)"] = NextPortion,
        ["PillarFieldsEditor.cs → Rows.NumberField(Диаметр…)"] = NextPortion,
        ["PipeFieldsEditor.cs → ReadOnlyField(Внутренний Ø…)"] = NextPortion,
        ["PipeFieldsEditor.cs → ReadOnlyField(Наружный Ø…)"] = NextPortion,
        ["PipeFieldsEditor.cs → ReadOnlyField(Толщина стенки…)"] = NextPortion,
        ["PipeFieldsEditor.cs → Rows.NumberField(label…)"] = NextPortion,
        ["PipeFittingFieldsEditor.cs → ReadOnlyField(Диаметр 2…)"] = NextPortion,
        ["PipeFittingFieldsEditor.cs → ReadOnlyField(Диаметр 3…)"] = NextPortion,
        ["RadialFieldsEditor.cs → Rows.NumberField(Радиус угла…)"] = NextPortion,
        ["ScrewLegFieldsEditor.cs → Rows.NumberField(Ø основания…)"] = NextPortion,
        ["ShowerColumnFieldsEditor.cs → Rows.NumberField(ColumnHeightLabel…)"] = NextPortion,
        ["ShowerColumnFieldsEditor.cs → Rows.NumberField(HandDiameterLabel…)"] = NextPortion,
        ["ShowerColumnFieldsEditor.cs → Rows.NumberField(HeadDiameterLabel…)"] = NextPortion,
        ["ShowerColumnFieldsEditor.cs → Rows.NumberField(HeadThicknessLabel…)"] = NextPortion,
        ["ShowerColumnFieldsEditor.cs → Rows.NumberField(HoseLengthLabel…)"] = NextPortion,
        ["ShowerColumnFieldsEditor.cs → Rows.NumberField(RiserDiameterLabel…)"] = NextPortion,
        ["WallDeviceFieldsEditor.cs → Rows.NumberField(PlateHeightLabel…)"] = NextPortion,
        ["WallDeviceFieldsEditor.cs → Rows.NumberField(PlateWidthLabel…)"] = NextPortion,
        ["WallOpeningFieldsEditor.cs → Rows.Dropdown(Стекло…)"] = NextPortion,


        ["SettingKeys.photo_quality"] =
            "«Качество» — кнопка-циклер (BuildPresetRow), а не строка _rows.AddXxx; у неё нет "
            + "своего rowKey, на который можно повесить Hint — нужен отдельный механизм, вне "
            + "этой порции",
        ["SettingKeys.snap_verbose_log"] =
            "SnapSystem.VerboseLog — отладочный флаг без строки в какой-либо панели настроек; "
            + "решение, заводить ли для него UI вообще, не входит в эту порцию",
    };

    /// <summary>Наполнение идёт порциями по панели, один коммит на порцию
    /// (docs/todo_evolution.md §1.3) — эта запись ещё не наполнена в ТЕКУЩЕЙ порции.</summary>
    private const string NextPortion = "наполнение этой панели не входит в текущую порцию — следующая порция";

    [Test]
    public void TheScan_FindsControlsInARealFile()
    {
        Assert.That(HintCoverageScan.FieldsEditorFiles().Count, Is.GreaterThanOrEqualTo(20),
            "скан не нашёл *FieldsEditor.cs в Assets/Scripts/Core/UI — проверки ниже "
            + "зеленели бы вхолостую");
        Assert.That(HintCoverageScan.SettingKeysDeclared().Count, Is.GreaterThanOrEqualTo(40),
            "скан не разобрал SettingKeys.All — формат SettingKey.Flag/Number в исходнике "
            + "разошёлся с регулярным выражением скана");
    }

    [Test]
    public void EveryNumberFieldOrDropdown_HasAHint_OrIsAKnownGap()
    {
        var gaps = HintCoverageScan.FieldsEditorGaps();
        var unnamed = gaps.Where(g => !KnownGaps.ContainsKey(g.Id)).ToList();

        Assert.IsEmpty(unnamed,
            "числовое поле или дропдаун *FieldsEditor без hint, и это НЕ признанный долг "
            + "(KnownGaps) — либо добавьте hint: \"ключ\" (текст — в HintText.cs, "
            + "docs/UI-GUIDELINES.md §13), либо занесите строку в KnownGaps с причиной:\n"
            + string.Join("\n", unnamed.Select(g => g.Detail)));
    }

    [Test]
    public void EveryKeyInSettingKeys_HasAHint_OrIsAKnownGap()
    {
        var gaps = HintCoverageScan.SettingKeysGaps();
        var unnamed = gaps.Where(g => !KnownGaps.ContainsKey(g.Id)).ToList();

        Assert.IsEmpty(unnamed,
            "ключ SettingKeys без hint в панели настроек, и это НЕ признанный долг "
            + "(KnownGaps) — либо добавьте Hint(rowKey, hint: \"ключ\") рядом со строкой, "
            + "либо занесите ключ в KnownGaps с причиной:\n"
            + string.Join("\n", unnamed.Select(g => g.Detail)));
    }

    /// <summary>Список долга обязан УБЫВАТЬ: запись, чью «i» уже наполнили, сюда больше не
    /// попадает от скана, и её надо вычеркнуть — иначе следующая поломка того же места
    /// молча спрячется за чужим оправданием.</summary>
    [Test]
    public void EveryKnownGap_IsStillAGap()
    {
        var current = HintCoverageScan.FieldsEditorGaps()
            .Concat(HintCoverageScan.SettingKeysGaps())
            .Select(g => g.Id)
            .ToHashSet(StringComparer.Ordinal);

        var stale = KnownGaps.Keys.Where(k => !current.Contains(k)).ToList();

        Assert.IsEmpty(stale,
            "запись KnownGaps больше не встречается в скане — «i» либо наполнили (тогда "
            + "просто вычеркните запись), либо контрол/ключ переименовали или убрали "
            + "(тогда запись мертва). Устарели: " + string.Join(", ", stale));
    }

    [Test]
    public void EveryKnownGap_HasAReason()
    {
        var empty = KnownGaps.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();
        Assert.IsEmpty(empty, "запись KnownGaps без причины ничем не отличается от тишины: "
            + string.Join(", ", empty));
    }
}
